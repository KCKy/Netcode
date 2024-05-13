using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GameNewt.Timing;
using Kcky.GameNewt.Providers;
using Kcky.GameNewt.Timing;
using Kcky.GameNewt.Transport;
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
    readonly StateHolder<TClientInput, TServerInput, TGameState> authStateHolder_; // Mutex used to stop RW/WR/WW conflicts.
    readonly PredictManager<TClientInput, TServerInput, TGameState> predictManager_;
    readonly CancellationTokenSource clientCancellation_ = new();
    readonly object stateMutex_ = new(); // Assures atomicity of start and termination operations.
    long tickStartStamp_;

    readonly ILogger logger_;

    readonly IClientDispatcher dispatcher_;

    enum ClientState
    {
        NotStarted,
        Started,
        Initiated,
        Terminated
    }

    ClientState clientState_ = ClientState.NotStarted;

    readonly SynchronizedClock clock_;

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
        init => predictManager_.ClientInputPredictor = value;
    }

    /// <summary>
    /// Server input predictor. If none is provided <see cref="DefaultServerInputPredictor{TServerInput,TGameState}"/> is used.
    /// </summary>
    public IServerInputPredictor<TServerInput, TGameState> ServerInputPredictor
    {
        init => predictManager_.ServerInputPredictor = value;
    }

    Action updateAction_ = () => throw new InvalidOperationException("The client uses its own thread. Update is not supported. To use update disable useOwnThread in the constructor.");
    IClock GetTimingClock(bool useOwnThread = false)
    {
        if (useOwnThread)
            return new ThreadClock();
        
        Clock clock = new();
        updateAction_ = () => clock.Update();
        return clock;
    }

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
        updateAction_.Invoke();
        return predictManager_.State;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="dispatcher">Sender to use for sending messages the server.</param>
    /// <param name="useOwnThread">Whether the client should use its own thread for updating.</param>
    /// <param name="loggerFactory">Optional logger factory to use for logging client events.</param>
    public Client(IClientDispatcher dispatcher, bool useOwnThread = true, ILoggerFactory? loggerFactory = null)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        logger_ = loggerFactory.CreateLogger<Client<TClientInput, TServerInput, TGameState>>();

        dispatcher_ = dispatcher;

        authStateHolder_ = new(loggerFactory);

        predictManager_ = new (authStateHolder_, dispatcher, loggerFactory);

        IClock internalClock = GetTimingClock(useOwnThread);

        clock_ = new(internalClock, loggerFactory)
        {
            TargetTps = TGameState.DesiredTickRate
        };

        SetHandlers();
    }

    void SetDelayHandler(long frame, double delay)
    {
        logger_.LogTrace("The client has received delay info for frame {Frame} with value {Delay}.", frame, delay);
        clock_.SetDelayHandler(frame, delay - TargetDelta);
    }

    void SetHandlers()
    {
        dispatcher_.OnAuthoritativeInput += AddAuthoritativeInput;
        dispatcher_.OnInitialize += Initialize;
        dispatcher_.OnSetDelay += SetDelayHandler;
        clock_.OnTick += predictManager_.Tick;
    }

    void UnsetHandlers()
    {
        dispatcher_.OnAuthoritativeInput -= AddAuthoritativeInput;
        dispatcher_.OnInitialize -= Initialize;
        dispatcher_.OnSetDelay -= SetDelayHandler;
        clock_.OnTick -= predictManager_.Tick;
    }

    /// <inheritdoc/>
    public int Id { get; private set; } = int.MaxValue;

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
    public long PredictFrame => predictManager_.Frame;

    /// <inheritdoc/>
    public double TargetDelta { get; init; } = 0.05;

    /// <inheritdoc/>
    public double CurrentTps => clock_.CurrentTps;

    /// <inheritdoc/>
    public double TargetTps => clock_.TargetTps;

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

        logger_.LogDebug("The client has started.");

        await clientCancellation_.Token;
    }

    /// <inheritdoc/>
    public void Terminate()
    {
        lock (stateMutex_)
        {
            if (clientState_ == ClientState.Terminated)
                return;

            clientState_ = ClientState.Terminated;
            logger_.LogDebug("The client has been signalled to terminate.");

            UnsetHandlers();
            predictManager_.Stop();
            clientCancellation_.Cancel();
        }
    }

    void AssertChecksum(long frame, ReadOnlySpan<byte> serializedInput, long? checksum)
    {
        if (!UseChecksum || checksum is not { } check)
            return;
        
        long actual = authStateHolder_.GetChecksum();

        if (actual != check)
        {
            var serialized = authStateHolder_.GetSerialized();
            Memory<byte> serializedInputCopy = new byte[serializedInput.Length];
            serializedInput.CopyTo(serializedInputCopy.Span);

            logger_.LogCritical("The client has detected a desync from the server for frame {Frame} ({ActualSum} != {ExpectedSum})! The diverged state: {SerializedState} has resulted from input {SerializedInput}", frame, actual, check, serialized, serializedInputCopy);
            ArrayPool<byte>.Shared.Return(serialized);
            throw new InvalidOperationException("The auth state has diverged from the server.");
        }

        logger_.LogTrace("The client authoritative state for frame {Frame} has been verified.", frame);
    }

    bool stateInitiated_ = false; // Used to assure invalid auth state updates don't happen before initiation.

    void Update(UpdateInput<TClientInput, TServerInput> input, ReadOnlySpan<byte> serializedInput, long? checksum, long inputFrame)
    {
        lock (authStateHolder_)
        {
            long frame = authStateHolder_.Frame + 1; 

            if (!stateInitiated_ || frame < inputFrame)
            {
                logger_.LogWarning("Given old undesired input of frame {Frame} for auth update {Index}. Skipping.", inputFrame, frame);
                return;
            }
            
            if (frame > inputFrame)
            {
                logger_.LogCritical("Given newer input of frame {Frame} before receiving for auth update {Index}.", inputFrame, frame);
                throw new ArgumentException("Given invalid frame for auth update.", nameof(inputFrame));
            }

            authStateHolder_.Update(input);
            AssertChecksum(frame, serializedInput, checksum);

            if (TraceState)
            {
                Memory<byte> trace = authStateHolder_.GetSerialized();
                logger_.LogInformation("Finished client authoritative state update for {Frame} resulting in state: {SerializedState}", frame, trace);
                ArrayPool<byte>.Shared.Return(trace);
            }
            
            displayer_.AddAuthoritative(frame, authStateHolder_.State);
            predictManager_.InformAuthInput(serializedInput, frame, input);
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
        var inputSpan = serializedInput.Span;
        var input = MemoryPackSerializer.Deserialize<UpdateInput<TClientInput, TServerInput>>(inputSpan);

        Update(input, inputSpan, checksum, frame);
            
        ArrayPool<byte>.Shared.Return(serializedInput);
        logger_.LogTrace("The client has completed the tick for frame {Frame}.", frame);

        if (traceTime)
        {
            TimeSpan tickTime = Stopwatch.GetElapsedTime(tickStartStamp_);
            logger_.LogTrace("Client tick for frame {Frame} took {Time}.", frame, tickTime);
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
            displayer_.AddAuthoritative(frame, authState);
            stateInitiated_ = true;
        }
    }

    void InitializePredictState(long frame, TGameState predictState)
    {
        predictManager_.Init(frame, predictState);
    }

    void Initialize(int id, long frame, Memory<byte> serializedState)
    {
        lock (stateMutex_)
        {
            if (clientState_ != ClientState.Started)
                return;

            clientState_ = ClientState.Initiated;

            logger_.LogDebug("Client received id {Id}", id);
            logger_.LogDebug("Received init state for {Frame} with {Serialized}.", frame, serializedState);

            (TGameState authState, TGameState predictState) = DeserializeStates(serializedState);

            InitializeAuthState(frame, authState);
            InitializePredictState(frame, predictState);

            Id = id;
            predictManager_.LocalId = id;
            displayer_.Init(id);

            clock_.Initialize(frame);
            clock_.RunAsync(clientCancellation_.Token).AssureNoFault();
        }
    }

    void AddAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        logger_.LogTrace("Received auth input for frame {Frame} with checksum {CheckSum}.", frame, checksum);
        Authorize(input, checksum, frame);
    }
}
