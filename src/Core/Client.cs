using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GameNewt.Timing;
using Kcky.GameNewt.Dispatcher;
using Kcky.GameNewt.Timing;
using Kcky.GameNewt.Utility;
using MemoryPack;
using Kcky.Useful;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kcky.GameNewt.Client;

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
    readonly StateHolder<TClientInput, TServerInput, TGameState, AuthoritativeStateType> authStateHolder_; // Mutex used to stop RW/WR/WW conflicts.
    readonly CancellationTokenSource clockCancellation_ = new();
    readonly object stateMutex_ = new(); // Assures atomicity of start and termination operations.
    readonly SynchronizedClock syncClock_;
    readonly ILogger logger_;
    readonly IClientDispatcher dispatcher_;
    readonly ILoggerFactory loggerFactory_;
    readonly Clock clock_ = new();

    PredictManager<TClientInput, TServerInput, TGameState>? predictManager_;
    PredictManager<TClientInput, TServerInput, TGameState> PredictManager => predictManager_ ?? throw new InvalidOperationException("The client has not been started.");

    long tickStartStamp_;
    ClientState clientState_ = ClientState.NotStarted;

    enum ClientState
    {
        NotStarted,
        Started,
        Initiated,
        Terminated
    }

    /// <summary>
    /// Invoked when a new authoritative state is computed.
    /// </summary>
    public event HandleNewAuthoritativeStateDelegate<TGameState>? OnNewAuthoritativeState;

    /// <summary>
    /// Invoked when a new predictive state is computed.
    /// </summary>
    public event HandleNewPredictiveStateDelegate<TGameState>? OnNewPredictiveState;

    /// <summary>
    /// Invoked when the client is initialized, provides the acquired local id.
    /// </summary>
    public event HandleClientInitializeDelegate? OnInitialize;

    /// <summary>
    /// Method which provides input for the local player.
    /// </summary>
    public ProvideClientInputDelegate<TClientInput> ClientInputProvider { private get; init; } = static () => new();
    
    /// <summary>
    /// Optional method to predict client input from the previous client input.
    /// </summary>
    /// <remarks>
    /// By default, we predict the input to not change.
    /// </remarks>
    public PredictClientInputDelegate<TClientInput> ClientInputPredictor { private get; init; } = static (ref TClientInput i) => {};

    /// <summary>
    /// Optional method to predict server input from the previous server input and game state.
    /// </summary>
    /// <remarks>
    /// By default, we predict the input to not change.
    /// </remarks>
    public PredictServerInputDelegate<TServerInput, TGameState> ServerInputPredictor { private get; init; } = static (ref TServerInput i, TGameState state) => {};

    /// <summary>
    /// Update the client.
    /// Shall be called frequently to update the simulation.
    /// </summary>
    /// <returns>
    /// The prediction game state, which is valid until this method is called again.
    /// </returns>
    /// <remarks>
    /// The game state may not be available until the client is fully connected.
    /// </remarks>
    public TGameState? Update()
    {
        predictManager_?.CheckPredict();
        clock_.Update();
        return predictManager_?.State;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="dispatcher">Sender to use for sending messages the server.</param>
    /// <param name="loggerFactory">Optional logger factory to use for logging client events.</param>
    public Client(IClientDispatcher dispatcher, ILoggerFactory? loggerFactory = null)
    {
        loggerFactory_ = loggerFactory ?? NullLoggerFactory.Instance;
        logger_ = loggerFactory_.CreateLogger<Client<TClientInput, TServerInput, TGameState>>();
        dispatcher_ = dispatcher;
        authStateHolder_ = new(loggerFactory_);

        syncClock_ = new(clock_, loggerFactory_)
        {
            TargetTps = TGameState.DesiredTickRate
        };

        TargetDelta = 0.05f;

        SetHandlers();
    }

    void NewPredictiveStateHandler(long frame, TGameState state) => OnNewPredictiveState?.Invoke(frame, state);

    void SetDelayHandler(long frame, float delay)
    {
        logger_.LogTrace("The client has received delay info for frame {Frame} with value {Delay}.", frame, delay);
        syncClock_.SetDelay(frame, delay);
    }

    void SetHandlers()
    {
        dispatcher_.OnAuthoritativeInput += AddAuthoritativeInput;
        dispatcher_.OnInitialize += Initialize;
        dispatcher_.OnSetDelay += SetDelayHandler;
    }

    void UnsetHandlers()
    {
        dispatcher_.OnAuthoritativeInput -= AddAuthoritativeInput;
        dispatcher_.OnInitialize -= Initialize;
        dispatcher_.OnSetDelay -= SetDelayHandler;
    }
    
    /// <inheritdoc/>
    public int Id { get; private set; } = int.MinValue;

    /// <inheritdoc/>
    public bool TraceState { get; set; }

    /// <inheritdoc/>
    public bool UseChecksum { get; set; }

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
    public long PredictFrame => predictManager_?.Frame ?? long.MinValue;

    /// <inheritdoc/>
    public float TargetDelta
    {
        get => syncClock_.TargetDelta;
        init => syncClock_.TargetDelta = value;
    }

    /// <inheritdoc/>
    public float CurrentTps => syncClock_.CurrentTps;

    /// <inheritdoc/>
    public float TargetTps => syncClock_.TargetTps;

    /// <summary>
    /// To account for jitter the clock works over a window of latencies.
    /// This value determines the number of frames for this window.
    /// </summary>
    public int SamplingWindow
    {
        get => syncClock_.SamplingWindow;
        init => syncClock_.SamplingWindow = value;
    }

    /// <inheritdoc/>
    public async Task RunAsync()
    {
        lock (stateMutex_)
        {
            switch (clientState_)
            {
                case ClientState.Terminated:
                    throw new InvalidOperationException("The client has been terminated.");
                case ClientState.Started or ClientState.Initiated:
                    throw new InvalidOperationException("The client has already been started.");
            }

            clientState_ = ClientState.Started;
        }

        predictManager_ = new(authStateHolder_, dispatcher_, loggerFactory_, NewPredictiveStateHandler, ServerInputPredictor, ClientInputPredictor, ClientInputProvider);
        syncClock_.OnTick += predictManager_.Tick;

        Task task = dispatcher_.RunAsync();
        logger_.LogDebug("The client has started.");

        try
        {
            await task;
        }
        finally
        {
            TerminateInternal();
        }
    }

    void TerminateInternal()
    {
        lock (stateMutex_)
        {
            if (clientState_ == ClientState.Terminated)
                return;

            clientState_ = ClientState.Terminated;
            

            UnsetHandlers();
            PredictManager.Stop();
            clockCancellation_.Cancel();
            dispatcher_.Terminate();
        }
    }

    /// <inheritdoc/>
    public void Terminate()
    {
        logger_.LogDebug("The client has been signalled to terminate.");
        TerminateInternal();
    }

    void AssertChecksum(long frame, ReadOnlyMemory<byte> serializedInput, long? checksum)
    {
        if (!UseChecksum || checksum is not { } check)
            return;
        
        long actual = authStateHolder_.GetChecksum();

        if (actual != check)
        {
            Memory<byte> serialized = authStateHolder_.GetSerialized();
            string base64State = Convert.ToBase64String(authStateHolder_.GetSerialized().Span);
            string base64Input = Convert.ToBase64String(serializedInput.Span);

            logger_.LogCritical("The client has detected a desync from the server for frame {Frame} ({ActualSum} != {ExpectedSum})! The diverged state: {SerializedState} has resulted from input {SerializedInput}", frame, actual, check, base64State, base64Input);
            ArrayPool<byte>.Shared.Return(serialized);
            throw new InvalidOperationException("The auth state has diverged from the server.");
        }

        logger_.LogTrace("The client authoritative state for frame {Frame} has been verified.", frame);
    }

    bool stateInitiated_ = false; // Used to assure invalid auth state updates don't happen before initiation.

    void Update(UpdateInput<TClientInput, TServerInput> input, ReadOnlyMemory<byte> serializedInput, long? checksum, long inputFrame)
    {
        lock (authStateHolder_)
        {
            long frame = authStateHolder_.Frame + 1; 

            if (!stateInitiated_ || frame > inputFrame)
            {
                logger_.LogWarning("Given old undesired input of frame {Frame} for auth update {Index}. Skipping.", inputFrame, frame);
                return;
            }
            
            if (inputFrame > frame)
            {
                logger_.LogCritical("Given newer input of frame {Frame} before receiving for auth update {Index}.", inputFrame, frame);
                throw new ArgumentException("Given invalid frame for auth update.", nameof(inputFrame));
            }

            authStateHolder_.Update(input);
            AssertChecksum(frame, serializedInput, checksum);

            if (TraceState)
            {
                Memory<byte> serializedState = authStateHolder_.GetSerialized();
                string base64Input = Convert.ToBase64String(serializedInput.Span);
                string base64State = Convert.ToBase64String(serializedState.Span);
                logger_.LogInformation("Finished client authoritative state update with input {SerializedInput} for {Frame} resulting in state: {SerializedState}", base64Input, frame, base64State);
                ArrayPool<byte>.Shared.Return(serializedState);
            }
            
            OnNewAuthoritativeState?.Invoke(frame, authStateHolder_.State);
            PredictManager.InformAuthInput(serializedInput.Span, frame, input);
        }
    }

    void Authorize(Memory<byte> serializedInput, long? checksum, long frame)
    {
        bool traceTime = logger_.IsEnabled(LogLevel.Information);

        if (traceTime)
        {
            tickStartStamp_ = Stopwatch.GetTimestamp();
        }

        logger_.LogTrace("The client has begun the next tick for frame.");
        var input = MemoryPackSerializer.Deserialize<UpdateInput<TClientInput, TServerInput>>(serializedInput.Span);

        Update(input, serializedInput, checksum, frame);
            
        ArrayPool<byte>.Shared.Return(serializedInput);
        logger_.LogTrace("The client has completed the tick for frame {Frame}.", frame);

        if (traceTime)
        {
            TimeSpan tickTime = Stopwatch.GetElapsedTime(tickStartStamp_);
            logger_.LogInformation("Client tick for frame {Frame} took {Time}.", frame, tickTime);
        }
    }

    (TGameState authState, TGameState predictState) DeserializeStates(Memory<byte> serializedState)
    {
        var span = serializedState.Span;
        var authState = MemoryPackSerializer.Deserialize<TGameState>(span);
        var predictState = MemoryPackSerializer.Deserialize<TGameState>(span);
            
        if (authState is null || predictState is null)
        {
            logger_.LogCritical("Received invalid init state.");
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
            OnNewAuthoritativeState?.Invoke(frame, authState);
            stateInitiated_ = true;
        }
    }

    void Initialize(int id, long frame, Memory<byte> serializedState)
    {
        lock (stateMutex_)
        {
            if (clientState_ != ClientState.Started)
                return;

            clientState_ = ClientState.Initiated;

            logger_.LogDebug("Client received id {Id} and init state for {Frame}", id, frame);

            if (TraceState)
            {
                string base64State = Convert.ToBase64String(serializedState.Span);
                logger_.LogDebug("The init state is {Serialized}.", base64State);
            }

            (TGameState authState, TGameState predictState) = DeserializeStates(serializedState);

            InitializeAuthState(frame, authState);

            Id = id;
            PredictManager.Init(frame, predictState, id);
            OnInitialize?.Invoke(id);

            syncClock_.Initialize(frame);
            syncClock_.RunAsync(clockCancellation_.Token).AssureNoFault(logger_);
        }
    }

    void AddAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        logger_.LogTrace("Received auth input for frame {Frame} with checksum {CheckSum}.", frame, checksum);
        Authorize(input, checksum, frame);
    }
}
