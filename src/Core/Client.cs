using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Kcky.GameNewt.Providers;
using Kcky.GameNewt.Timing;
using Kcky.GameNewt.Transport;
using Kcky.GameNewt.Utility;
using MemoryPack;
using Serilog;
using Kcky.Useful;

namespace Kcky.GameNewt.Client;

/// <summary>
/// Provides common API of the client usable across games.
/// </summary>
public interface IClient
{
    /// <summary>
    /// The id of the client.
    /// </summary>
    long Id { get; }

    /// <summary>
    /// 
    /// </summary>
    bool TraceState { get; set; }

    /// <summary>
    /// Whether to check if checksums provided by the server match with the states on the client side (server needs to have checksum enabled as well).
    /// </summary>
    bool UseChecksum { get; set; }

    /// <summary>
    /// Whether to trace time, how much took to update each frame, in the log.
    /// </summary>
    bool TraceFrameTime { get; init; }
    
    /// <summary>
    /// The latest authoritative frame the client has computed.
    /// </summary>
    long AuthFrame { get; }

    /// <summary>
    /// The latest predict frame the client has computed i.e. the frame the clients perception of the game is at.
    /// </summary>
    long PredictFrame { get; }
    
    /// <summary>
    /// Number of seconds, how much the client should be ahead from the server (ignoring latency).
    /// </summary>
    double TargetDelta { get; }
    
    /// <summary>
    /// Returns the predict loop TPS, could be slightly off from the server loop to catch up/slow down.
    /// </summary>
    double CurrentTps { get; }

    /// <summary>
    /// Current delay from the server (ignoring latency), the client will modify <see cref="CurrentTps"/> so this value would approach <see cref="TargetDelta"/>.
    /// </summary>
    double CurrentDelta { get; }

    /// <summary>
    /// The target TPS of the client clock, the clock should as closely match this TPS while spacing the ticks evenly.
    /// </summary>
    public double TargetTps { get; }

    /// <summary>
    /// Begin the client. Starts the update controller for predict, input collection, and server communication.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the client has already been started or terminated.</exception>
    /// <returns>Task representing the client's runtime. When the client crashes the task will be faulted. If the client is stopped it will be cancelled.</returns>
    Task RunAsync();
    
    /// <summary>
    /// Stops the client. Predict updates will stop.
    /// </summary>
    void Terminate();
}

/// <summary>
/// The main client class. Takes care of collecting the clients inputs.
/// runs predict state based on predicted input ahead of auth input, receives auth input updates,
/// updates the local auth states, replaces predicted state in cases of mispredictions.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public sealed class Client<TClientInput, TServerInput, TGameState> : IClient
    where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()

{
    readonly StateHolder<TClientInput, TServerInput, TGameState> authStateHolder_ = new(); // Mutex used to stop RW/WR/WW conflicts.
    readonly PredictManager<TClientInput, TServerInput, TGameState> predictManager_;
    readonly CancellationTokenSource clockCancellation_ = new();
    readonly object terminationMutex_ = new(); // Assures atomicity of start and termination operations.
    readonly UpdateTimer timer_ = new();

    readonly ILogger logger_ = Log.ForContext<Client<TClientInput, TServerInput, TGameState>>();

    readonly DelayCalculator<TGameState, TClientInput, TServerInput> delayCalculator_;

    readonly IClientDispatcher dispatcher_;

    bool started_ = false;
    bool terminated_ = false;
    bool initiated_ = false;
    bool identified_ = false;

    /// <summary>
    /// The time to smooth changes in predict simulation speed over.
    /// Higher value yields less drastic changes, but the client may take time to recover from a lag spike.
    /// </summary>
    public double SmoothingTime
    {
        get => speedController_.SmoothingTime;
        set => speedController_.SmoothingTime = value;
    }

    readonly IDisplayer<TGameState> displayer_ = new DefaultDisplayer<TGameState>();

    /// <summary>
    /// Displayer to display the auth state and predict state.
    /// </summary>
    public IDisplayer<TGameState> Displayer
    {
        init
        {
            displayer_ = value;
            predictManager_.Displayer = value;
        }
    }

    /// <summary>
    /// Client input provider. If none is provided <see cref="DefaultClientInputProvider{TClientInput}"/> is used.
    /// </summary>
    public IClientInputProvider<TClientInput> ClientInputProvider
    {
        init => predictManager_.ClientInputProvider = value;
    }

    /// <summary>
    /// Client input predictor. If none is provided <see cref="DefaultClientInputPredictor{TClientInput}"/> is used.
    /// </summary>
    public IClientInputPredictor<TClientInput> ClientInputPredictor
    {
        init => predictManager_.InputPredictor = value;
    }

    /// <summary>
    /// Server input predictor. If none is provided <see cref="DefaultServerInputPredictor{TServerInput,TGameState}"/> is used.
    /// </summary>
    public IServerInputPredictor<TServerInput, TGameState> ServerInputPredictor
    {
        init => predictManager_.ServerInputPredictor = value;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="dispatcher">Sender to use for sending messages the server.</param>
    public Client(IClientDispatcher dispatcher)
    {
        dispatcher_ = dispatcher;

        speedController_ = new SpeedController()
        {
            TargetTps = TGameState.DesiredTickRate,
            TargetNeighborhood = 1 / TGameState.DesiredTickRate,
        };

        delayCalculator_ = new DelayCalculator<TGameState, TClientInput, TServerInput>()
        {
            UsedSpeedController = speedController_
        };

        predictManager_ = new PredictManager<TClientInput, TServerInput, TGameState>
        {
            Displayer = displayer_,
            InputPredictor = new DefaultClientInputPredictor<TClientInput>(),
            ServerInputPredictor = new DefaultServerInputPredictor<TServerInput, TGameState>(),
            ClientInputProvider = new DefaultClientInputProvider<TClientInput>(),
            AuthState = authStateHolder_,
            Sender = dispatcher_,
            DelayCalculator = delayCalculator_
        };

        SetHandlers();
    }

    void SetHandlers()
    {
        dispatcher_.OnAddAuthInput += AddAuthoritativeInput;
        dispatcher_.OnInitialize += Initialize;
        dispatcher_.OnSetDelay += delayCalculator_.SetDelay;
        dispatcher_.OnStart += Start;
        delayCalculator_.OnDelayChanged += UpdateDelay;
    }

    void UnsetHandlers()
    {
        dispatcher_.OnAddAuthInput -= AddAuthoritativeInput;
        dispatcher_.OnInitialize -= Initialize;
        dispatcher_.OnSetDelay -= delayCalculator_.SetDelay;
        dispatcher_.OnStart -= Start;
        delayCalculator_.OnDelayChanged -= UpdateDelay;
    }

    void UpdateDelay(double delay) => speedController_.CurrentDelta = delay;

    /// <inheritdoc/>
    public long Id { get; private set; } = long.MaxValue;

    /// <inheritdoc/>
    public bool TraceState { get; set; }

    /// <inheritdoc/>
    public bool UseChecksum { get; set; }

    /// <inheritdoc/>
    public bool TraceFrameTime { get; init; }

    /// <inheritdoc/>
    public long AuthFrame
    {
        get
        {
            lock (authStateHolder_)
                return authStateHolder_.Frame;
        }
    }

    /// <inheritdoc/>
    public long PredictFrame => predictManager_.Frame;

    /// <inheritdoc/>
    public double TargetDelta => speedController_.TargetDelta;

    /// <inheritdoc/>
    public double CurrentTps => speedController_.CurrentTps;

    /// <inheritdoc/>
    public double CurrentDelta => speedController_.CurrentDelta;

    /// <inheritdoc/>
    public double TargetTps => speedController_.TargetTps;

    /// <inheritdoc/>
    public async Task RunAsync()
    {
        lock (terminationMutex_)
        {
            if (started_ || terminated_)
                return;

            started_ = true;
        }

        logger_.Information("The client is starting.");

        try
        {
            await speedController_.RunAsync(clockCancellation_.Token);
        }
        catch (OperationCanceledException) { }
    }


    /// <inheritdoc/>
    public void Terminate()
    {
        lock (terminationMutex_)
        {
            if (terminated_)
                return;

            logger_.Information("The client is being terminated.");

            terminated_ = true;

            dispatcher_.Disconnect();

            predictManager_.Stop();
            speedController_.OnTick -= predictManager_.Tick;
            clockCancellation_.Cancel();
            UnsetHandlers();
        }
    }

    void AssertChecksum(long? checksum)
    {
        if (!UseChecksum || checksum is not { } check)
            return;
        
        var serialized = Memory<byte>.Empty;
        if (TraceState)
            serialized = authStateHolder_.Serialize();

        long actual = authStateHolder_.GetChecksum();
        if (actual != check)
        {
            logger_.Fatal("Desync detected {ActualSum} != {ExpectedSum} - {State}.", actual, check, serialized);
            throw new InvalidOperationException("The auth state has diverged from the server.");
        }

        logger_.Verbose( "State has a correct checksum.");

        ArrayPool<byte>.Shared.Return(serialized);
    }

    bool stateInitiated_ = false; // Used to assure invalid auth state updates don't happen before initiation.
    readonly ISpeedController speedController_;

    void Update(UpdateInput<TClientInput, TServerInput> input, Span<byte> serializedInput, long? checksum, long inputFrame)
    {
        lock (authStateHolder_)
        {
            long frame = authStateHolder_.Frame + 1; 

            if (!stateInitiated_ || frame < inputFrame)
            {
                logger_.Warning("Given old undesired input of frame {Frame} for auth update {Index}. Skipping.", inputFrame, frame);
                return;
            }
            
            if (frame > inputFrame)
            {
                logger_.Fatal("Given newer input of frame {Frame} before receiving for auth update {Index}.", inputFrame, frame);
                throw new ArgumentException("Given invalid frame for auth update.", nameof(inputFrame));
            }

            authStateHolder_.Update(input);
            AssertChecksum(checksum);
            displayer_.AddAuthoritative(frame, authStateHolder_.State);
            logger_.Verbose("Authorized frame {Frame}", frame);
            predictManager_.InformAuthInput(serializedInput, frame, input);
        }

        delayCalculator_.Update();
    }

    void Authorize(Memory<byte> serializedInput, long? checksum, long frame)
    {
        if (TraceFrameTime)
            timer_.Start();

        var inputSpan = serializedInput.Span;
        var input = MemoryPackSerializer.Deserialize<UpdateInput<TClientInput, TServerInput>>(inputSpan);

        Update(input, inputSpan, checksum, frame);
            
        ArrayPool<byte>.Shared.Return(serializedInput);

        if (TraceFrameTime)
            timer_.End(frame);
    }

    void Start(long id)
    {
        lock (terminationMutex_)
        {
            if (identified_ || terminated_ || !started_)
                return;

            logger_.Debug("Client received id {Id}", id);

            identified_ = true;

            Id = id;
            predictManager_.LocalId = id;
            displayer_.Init(id);

            if (initiated_ && identified_)
                speedController_.OnTick += predictManager_.Tick;
        }
    }

    (TGameState authState, TGameState predictState) DeserializeStates(Memory<byte> serializedState)
    {
        var span = serializedState.Span;
        var authState = MemoryPackSerializer.Deserialize<TGameState>(span);
        var predictState = MemoryPackSerializer.Deserialize<TGameState>(span);
            
        if (authState is null || predictState is null)
        {
            logger_.Fatal("The state is invalid.");
            throw new ArgumentException("Received invalid init state.", nameof(serializedState));
        }
        
        ArrayPool<byte>.Shared.Return(serializedState);

        return (authState, predictState);
    }

    void InitializeAuthState(long frame, TGameState authState)
    {
        lock (authStateHolder_)
        {
            authStateHolder_.Frame = frame;
            authStateHolder_.State = authState;
            displayer_.AddAuthoritative(frame, authState);
            stateInitiated_ = true;
        }
    }

    void InitializePredictState(long frame, TGameState predictState)
    {
        predictManager_.Init(frame, predictState);
    }

    void Initialize(long frame, Memory<byte> serializedState)
    {
        lock (terminationMutex_)
        {
            if (initiated_ || terminated_ || !started_)
                return;

            initiated_ = true;

            logger_.Debug("Received init state for {Frame} with {Serialized}.", frame, serializedState);

            (TGameState authState, TGameState predictState) = DeserializeStates(serializedState);

            InitializeAuthState(frame, authState);
            InitializePredictState(frame, predictState);
            
            // Run
            if (initiated_ && identified_)
                speedController_.OnTick += predictManager_.Tick;
        }
    }

    void AddAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        logger_.Verbose("Received auth input for frame {Frame} with checksum {CheckSum}.", frame, checksum);
        Authorize(input, checksum, frame);
    }
}
