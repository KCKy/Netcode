using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Core.DataStructures;
using Core.Transport;
using Core.Providers;
using Core.Timing;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace Core.Client;

/// <summary>
/// Provides common API of the client usable across games.
/// </summary>
public interface IClient
{
    /// <summary>
    /// <see cref="ISpeedController.CurrentTps"/> of the underlying predict loop speed controller.
    /// </summary>
    double CurrentTps { get; }
    
    /// <summary>
    /// <see cref="ISpeedController.CurrentDelta"/> of the underlying predict loop speed controller.
    /// </summary>
    double CurrentDelta { get; }
    
    /// <summary>
    /// <see cref="ISpeedController.TargetDelta"/> of the underlying predict loop speed controller.
    /// </summary>
    double TargetDelta { get; }
    
    /// <summary>
    /// <see cref="IClock.TargetTps"/> of the underlying predict loop speed controller.
    /// </summary>
    double TargetTps { get; }

    /// <summary>
    /// Amount of time in seconds specifying how much early inputs should be received by the server. This extra margin assures small delays don't result in input loss.
    /// </summary>
    double PredictDelayMargin { get; set; }

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
    readonly IStateHolder<TClientInput, TServerInput, TGameState> authStateHolder_; // Used to stop RW/WR/WW conflicts.
    readonly ILogger logger_ = Log.ForContext<Client<TClientInput, TServerInput, TGameState>>();
    readonly ISpeedController clock_;
    readonly IDisplayer<TGameState> displayer_;
    readonly IClientSender sender_;
    readonly IClientReceiver receiver_;
    readonly IPredictManager<TClientInput, TServerInput, TGameState> predictManager_;

    readonly CancellationTokenSource clockCancellation_ = new();
    readonly object terminationMutex_ = new(); // Assures atomicity of start and termination operations.
    readonly UpdateTimer timer_;

    bool started_ = false;
    bool terminated_ = false;
    bool initiated_ = false;
    bool identified_ = false;
    
    /// <inheritdoc/>
    public double CurrentTps => clock_.CurrentTps;
    
    /// <inheritdoc/>
    public double CurrentDelta => clock_.CurrentDelta;
    
    /// <inheritdoc/>
    public double TargetDelta => clock_.TargetDelta;
    
    /// <inheritdoc/>
    public double TargetTps => clock_.TargetTps;

    /// <inheritdoc/>
    public double PredictDelayMargin
    {
        get => clock_.TargetDelta;
        set => clock_.TargetDelta = value;
    }
    
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="sender">Sender to use for sending messages the server.</param>
    /// <param name="receiver">Receiver to use for receiving messages from the server.</param>
    /// <param name="displayer">Optional displayer to display the auth state and predict state.</param>
    /// <param name="inputProvider">Optional client input provider. If none is provided <see cref="DefaultClientInputProvider{TClientInput}"/> is used.</param>
    /// <param name="serverInputPredictor">Optional server input predictor. If none is provided <see cref="DefaultServerInputPredictor{TServerInput,TGameState}"/> is used.</param>
    /// <param name="clientInputPredictor">Optional client input predictor. If none is provided <see cref="DefaultClientInputPredictor{TClientInput}"/> is used.</param>
    public Client(IClientSender sender, IClientReceiver receiver,
        IDisplayer<TGameState>? displayer,
        IClientInputProvider<TClientInput>? inputProvider,
        IServerInputPredictor<TServerInput, TGameState>? serverInputPredictor = null,
        IClientInputPredictor<TClientInput>? clientInputPredictor = null)
    {
        sender_ = sender;
        receiver_ = receiver;
        displayer_ = displayer ?? new DefaultDisplayer<TGameState>();
        clock_ = new SpeedController()
        {
            TargetTps = TGameState.DesiredTickRate
        };

        authStateHolder_ = new StateHolder<TClientInput, TServerInput, TGameState>();

        predictManager_ = new PredictManager<TClientInput, TServerInput, TGameState>
        {
            ClientInputs = new LocalInputQueue<TClientInput>(),
            InputPredictor = clientInputPredictor ?? new DefaultClientInputPredictor<TClientInput>(),
            ServerInputPredictor = serverInputPredictor ?? new DefaultServerInputPredictor<TServerInput, TGameState>(),
            InputProvider = inputProvider ?? new DefaultClientInputProvider<TClientInput>(),
            AuthState = authStateHolder_,
            Sender = sender_,
            Displayer = displayer_
        };

        PredictDelayMargin = 0.15f;
        timer_ = new();
        SetHandlers();
    }

    void SetHandlers()
    {
        receiver_.OnAddAuthInput += AddAuthoritativeInput;
        receiver_.OnInitialize += Initialize;
        receiver_.OnSetDelay += SetDelay;
        receiver_.OnStart += Start;
    }

    void UnsetHandlers()
    {
        receiver_.OnAddAuthInput -= AddAuthoritativeInput;
        receiver_.OnInitialize -= Initialize;
        receiver_.OnSetDelay -= SetDelay;
        receiver_.OnStart -= Start;
    }
    
    internal Client(IClientSender sender, IClientReceiver receiver, IDisplayer<TGameState> displayer, ISpeedController controller, IStateHolder<TClientInput, TServerInput, TGameState> authStateHolder,
        IPredictManager<TClientInput, TServerInput, TGameState> predictManager)
    {
        sender_ = sender;
        receiver_ = receiver;
        displayer_ = displayer;
        clock_ = controller;
        authStateHolder_ = authStateHolder;
        predictManager_ = predictManager;
        PredictDelayMargin = 0.15f;
        timer_ = new();
        SetHandlers();
    }

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
            await clock_.RunAsync(clockCancellation_.Token);
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

            sender_.Disconnect();

            predictManager_.Stop();
            clock_.OnTick -= predictManager_.Tick;
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
                clock_.OnTick += predictManager_.Tick;
        }
    }

    void Initialize(long frame, Memory<byte> serializedState)
    {
        lock (terminationMutex_)
        {
            if (initiated_ || terminated_ || !started_)
                return;

            initiated_ = true;

            // Deserialize state
            var span = serializedState.Span;
            var authState = MemoryPackSerializer.Deserialize<TGameState>(span);
            var predictState = MemoryPackSerializer.Deserialize<TGameState>(span);
            
            if (authState is null || predictState is null)
            {
                logger_.Fatal("Received invalid init state {Serialized}.", serializedState);
                throw new ArgumentException("Received invalid init state.", nameof(serializedState));
            }

            logger_.Debug("Received init state for {Frame} with {Serialized}.", frame, serializedState);

            ArrayPool<byte>.Shared.Return(serializedState);

            // Init auth

            lock (authStateHolder_)
            {
                authStateHolder_.Frame = frame;
                authStateHolder_.State = authState;
                displayer_.AddAuthoritative(frame, authState);
                stateInitiated_ = true;
            }

            // Init predict
            predictManager_.Init(frame, predictState);
            
            // Run
            if (initiated_ && identified_)
                clock_.OnTick += predictManager_.Tick;
        }
    }

    void AddAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        logger_.Verbose("Received auth input for frame {Frame} with checksum {CheckSum}.", frame, checksum);
        Authorize(input, checksum, frame);
    }

    void SetDelay(double delay) => clock_.CurrentDelta = delay;
}
