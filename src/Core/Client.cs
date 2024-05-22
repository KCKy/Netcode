﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GameNewt.Timing;
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
    readonly StateHolder<TClientInput, TServerInput, TGameState, AuthoritativeStateType> authStateHolder_; // Mutex used to stop RW/WR/WW conflicts.
    readonly CancellationTokenSource clientCancellation_ = new();
    readonly object stateMutex_ = new(); // Assures atomicity of start and termination operations.
    readonly SynchronizedClock clock_;
    readonly ILogger logger_;
    readonly IClientDispatcher dispatcher_;
    readonly ILoggerFactory loggerFactory_;

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

    public event HandleNewAuthoritativeStateDelegate<TGameState>? OnNewAuthoritativeState;
    public event HandleNewPredictiveStateDelegate<TGameState>? OnNewPredictiveState;
    public event HandleClientInitializeDelegate? OnInitialize;
    public ProvideClientInputDelegate<TClientInput> ClientInputProvider { private get; init; } = static () => new();
    public PredictClientInputDelegate<TClientInput> ClientInputPredictor { private get; init; } = static (ref TClientInput i) => {};
    public PredictServerInputDelegate<TServerInput, TGameState> ServerInputPredictor { private get; init; } = static (ref TServerInput i, TGameState state) => {};



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
        return predictManager_?.State;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="dispatcher">Sender to use for sending messages the server.</param>
    /// <param name="useOwnThread">Whether the client should use its own thread for updating.</param>
    /// <param name="loggerFactory">Optional logger factory to use for logging client events.</param>
    public Client(IClientDispatcher dispatcher, bool useOwnThread = true, ILoggerFactory? loggerFactory = null)
    {
        loggerFactory_ = loggerFactory ?? NullLoggerFactory.Instance;
        logger_ = loggerFactory_.CreateLogger<Client<TClientInput, TServerInput, TGameState>>();
        dispatcher_ = dispatcher;
        authStateHolder_ = new(loggerFactory_);

        IClock internalClock = GetTimingClock(useOwnThread);
        clock_ = new(internalClock, loggerFactory_)
        {
            TargetTps = TGameState.DesiredTickRate
        };

        SetHandlers();
    }

    void NewPredictiveStateHandler(long frame, TGameState state) => OnNewPredictiveState?.Invoke(frame, state);

    void SetDelayHandler(long frame, double delay)
    {
        logger_.LogTrace("The client has received delay info for frame {Frame} with value {Delay}.", frame, delay);
        clock_.SetDelay(frame, delay - TargetDelta);
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

        predictManager_ = new(authStateHolder_, dispatcher_, loggerFactory_, NewPredictiveStateHandler, ServerInputPredictor, ClientInputPredictor, ClientInputProvider);
        clock_.OnTick += predictManager_.Tick;

        logger_.LogDebug("The client has started.");
        await Task.Delay(Timeout.Infinite, clientCancellation_.Token);
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
            PredictManager.Stop();
            clientCancellation_.Cancel();
        }
    }

    void AssertChecksum(long frame, ReadOnlyMemory<byte> serializedInput, long? checksum)
    {
        if (!UseChecksum || checksum is not { } check)
            return;
        
        long actual = authStateHolder_.GetChecksum();

        if (actual != check)
        {
            var serialized = authStateHolder_.GetSerialized();

            logger_.LogCritical("The client has detected a desync from the server for frame {Frame} ({ActualSum} != {ExpectedSum})! The diverged state: {SerializedState} has resulted from input {SerializedInput}", frame, actual, check, serialized, serializedInput);
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
                logger_.LogInformation("Finished client authoritative state update with input {SerializedInput} for {Frame} resulting in state: {SerializedState}", serializedInput, frame, trace);
                ArrayPool<byte>.Shared.Return(trace);
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

            logger_.LogDebug("Client received id {Id}", id);
            logger_.LogDebug("Received init state for {Frame} with {Serialized}.", frame, serializedState);

            (TGameState authState, TGameState predictState) = DeserializeStates(serializedState);

            InitializeAuthState(frame, authState);

            Id = id;
            PredictManager.Init(frame, predictState, id);
            OnInitialize?.Invoke(id);

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
