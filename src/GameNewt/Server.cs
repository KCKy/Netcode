﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Dispatcher;
using Kcky.GameNewt.Timing;
using Kcky.GameNewt.Utility;
using MemoryPack;
using Kcky.Useful;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kcky.GameNewt.Server;

/// <summary>
/// The main server class. Takes care of collecting inputs of clients,
/// periodically updating the authoritative state based on collected input, manages clients, informs them about authoritative inputs,
/// and whether they send their inputs on time. Also supports optional checksums of the state.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public sealed class Server<TClientInput, TServerInput, TGameState> : IServer
    where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
{
    enum ServerState
    {
        NotStarted,
        Started,
        Terminated
    }

    // Locking the holder to stop RW/WR conflicts and correct init behaviour (WW does not happen due to tickMutex)
    readonly StateHolder<TClientInput, TServerInput, TGameState, ServerStateType> stateHolder_;

    ServerState serverState_ = ServerState.NotStarted;
    readonly ThreadClock clock_ = new();
    readonly CancellationTokenSource clockCancellation_ = new();

    bool updatingEnded_ = false;
    long tickStartStamp_;

    readonly ILogger logger_;
    readonly ClientInputQueue<TClientInput> inputQueue_;
    readonly IServerDispatcher dispatcher_;
    readonly PooledBufferWriter<byte> authInputWriter_ = new();
    readonly PooledBufferWriter<byte> traceStateWriter_ = new();
    readonly ProvideServerInputDelegate<TServerInput, TGameState> serverInputProvider_ = static _ => new();

    readonly object stateMutex_ = new(); // Assures atomicity of start and termination operations.
    readonly object tickMutex_ = new(); // Assures tick updates are atomic.

    /// <summary>
    /// Method which provides server input based on current state.
    /// </summary>
    public ProvideServerInputDelegate<TServerInput, TGameState>? ServerInputProvider
    {
        init
        {
            if (value is not null)
                serverInputProvider_ = value;
        }
    }

    /// <summary>
    /// Optional method to predict client input from the previous client input.
    /// </summary>
    /// <remarks>
    /// By default, we predict the input to not change.
    /// </remarks>
    public PredictClientInputDelegate<TClientInput>? ClientInputPredictor
    {
        init
        {
            if (value is not null)
                inputQueue_ = new(TGameState.DesiredTickRate, value, dispatcher_.SetDelay, loggerFactory_);
        }
    }

    readonly ILoggerFactory loggerFactory_;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="dispatcher">Dispatcher to communicate with clients.</param>
    /// <param name="loggerFactory">Optional logger factory to use for logging server events.</param>
    public Server(IServerDispatcher dispatcher, ILoggerFactory? loggerFactory = null)
    {
        loggerFactory_ = loggerFactory ?? NullLoggerFactory.Instance;
        stateHolder_ = new(loggerFactory_);

        logger_ = loggerFactory_.CreateLogger<Server<TClientInput, TServerInput, TGameState>>();
        dispatcher_ = dispatcher;
        inputQueue_ = new(TGameState.DesiredTickRate, static (ref TClientInput _) => { }, dispatcher_.SetDelay, loggerFactory_);
        SetHandlers();
    }

    void SetHandlers()
    {
        dispatcher_.OnAddClient += AddClient;
        dispatcher_.OnAddInput += AddInput;
        dispatcher_.OnRemoveClient += RemoveClient;
    }

    void UnsetHandlers()
    {
        dispatcher_.OnAddClient -= AddClient;
        dispatcher_.OnAddInput -= AddInput;
        dispatcher_.OnRemoveClient -= RemoveClient;
    }

    /// <inheritdoc/>
    public bool TraceState { get; set; }

    /// <inheritdoc/>
    public bool SendChecksum { get; set; }

    /// <summary>
    /// Invoked once when the server starts.
    /// May be used to modify the initial authoritative game state.
    /// </summary>
    public event InitializeStateDelegate<TGameState>? OnStateInit;

    /// <inheritdoc/>
    public async Task RunAsync()
    {
        lock (stateMutex_)
        {
            switch (serverState_)
            {
                case ServerState.Terminated:
                    throw new InvalidOperationException("The server has been terminated.");
                case ServerState.Started:
                    throw new InvalidOperationException("The server has already been started.");
            }

            serverState_ = ServerState.Started;
            clock_.TargetTps = TGameState.DesiredTickRate;
            clock_.OnTick += Tick;
        }

        lock (stateHolder_)
            OnStateInit?.Invoke(stateHolder_.State);

        clock_.RunAsync(clockCancellation_.Token).AssureNoFault(logger_);

        Task task = dispatcher_.RunAsync();

        logger_.LogDebug("The server has started.");

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
            if (serverState_ == ServerState.Terminated)
                return;

            clock_.OnTick -= Tick;
            clockCancellation_.Cancel();
            UnsetHandlers();
            dispatcher_.Terminate();
            authInputWriter_.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Terminate()
    {
        logger_.LogDebug("The server has been signalled to terminate from outside.");
        TerminateInternal();
    }

    UpdateInput<TClientInput, TServerInput> GatherInput()
    {
        var clientInput = inputQueue_.ConstructAuthoritativeFrame();
        TServerInput serverInput = serverInputProvider_(stateHolder_.State);
        return new(clientInput, serverInput);
    }

    void HandleKicking(int[]? clientsToKick)
    {
        if (clientsToKick is not { Length: > 0 } toTerminate)
            return;

        foreach (int client in toTerminate)
            dispatcher_.Kick(client);
    }

    (UpdateOutput output, long? checksum) StateUpdate(UpdateInput<TClientInput, TServerInput> input)
    {
        UpdateOutput output;
        lock (stateHolder_)
            output = stateHolder_.Update(input);
        
        // It is ok to release to lock earlier, as the update is atomic and the state is modified only in the update (concurrent reading is allowed).

        long? checksum = SendChecksum ? stateHolder_.GetChecksum() : null;

        if (TraceState && logger_.IsEnabled(LogLevel.Information))
        {
            var serializedState = traceStateWriter_.MemoryPackSerialize(stateHolder_.State);
            string base64State = Convert.ToBase64String(serializedState.Span);
            logger_.LogInformation("Finished server state update for {Frame} resulting in state: {SerializedState}", stateHolder_.Frame, base64State);
            ArrayPool<byte>.Shared.Return(serializedState);
        }
        
        return (output, checksum);
    }

    long Update()
    {
        var input = GatherInput();

        long frame = stateHolder_.Frame + 1;

        // Update
        (UpdateOutput output, long? checksum) = StateUpdate(input);

        HandleKicking(output.ClientsToTerminate);

        dispatcher_.SendAuthoritativeInput(frame, checksum, input);

        if (output.ShallStop)
        {
            updatingEnded_ = true;
            logger_.LogDebug("The server has been signalled to end from a game rules.");
            TerminateInternal();
        }

        return frame;
    }

    
    void Tick()
    {
        lock (tickMutex_)
        {
            if (updatingEnded_)
                return;

            bool traceTime = logger_.IsEnabled(LogLevel.Information);

            if (traceTime)
            {
                tickStartStamp_ = Stopwatch.GetTimestamp();
            }

            logger_.LogDebug("The server has begun the next tick for frame.");
            long frame = Update();
            logger_.LogDebug("The server has completed the tick for frame {Frame}.", frame);

            if (traceTime)
            {
                TimeSpan tickTime = Stopwatch.GetElapsedTime(tickStartStamp_);
                logger_.LogInformation("Server tick for frame {Frame} took {Time}.", frame, tickTime);
            }
        }
    }

    long InitializeClientAtomic(int id)
    {
        // We need to synchronize the state and assure an update is not in progress.
        // The client won't miss next input's as the lock is not released until the client is initialized.
        lock (stateHolder_)
        {
            long frame = stateHolder_.Frame;
            dispatcher_.Initialize(id, frame, stateHolder_.State);
            return frame;
        }
    }

    void AddClient(int id)
    {
        inputQueue_.AddClient(id);
        long frame = InitializeClientAtomic(id);
        logger_.LogDebug("Client with id {Id} has connected to the server and has been initiated for frame {Frame}.", id, frame);
    }

    void AddInput(int id, long frame, ReadOnlyMemory<byte> serializedInput)
    {
        var input = MemoryPackSerializer.Deserialize<TClientInput>(serializedInput.Span);

        if (input is null)
        {
            logger_.LogWarning("The server has received invalid input from client with id {Id} for frame {Frame}.", id, frame);
        }
        else
        {
            inputQueue_.AddInput(id, frame, input);
            logger_.LogTrace("The server has received valid input from client with id {Id} for frame {Frame}.", id, frame);
        }
    }
    
    void RemoveClient(int id)
    {
        inputQueue_.RemoveClient(id);
        logger_.LogDebug("Client with id {Id} has disconnected from the server.", id);
    }
}
