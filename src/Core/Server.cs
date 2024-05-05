using System;
using System.Threading;
using System.Threading.Tasks;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Providers;
using Kcky.GameNewt.Timing;
using Kcky.GameNewt.Transport;
using Kcky.GameNewt.Utility;
using MemoryPack;
using Serilog;
using Kcky.Useful;

namespace Kcky.GameNewt.Server;

/// <summary>
/// Provides common API of the server usable across games.
/// </summary>
public interface IServer
{
    /// <summary>
    /// Whether to log all states in the log.
    /// </summary>
    bool TraceState { get; }

    /// <summary>
    /// Whether to do checksums of game states. The server will calculate the checksum and send it to each client.
    /// </summary>
    bool SendChecksum { get; }

    /// <summary>
    /// Whether to trace time, how much took to update each frame, in the log.
    /// </summary>
    bool TraceFrameTime { get; }

    /// <summary>
    /// Begin the server. Starts the update clock and handling clients.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the server has already been started or terminated.</exception>
    /// <returns>Task representing the servers runtime. When the server crashes the task will be faulted. If the server is stopped it will be cancelled.</returns>
    Task RunAsync();

    /// <summary>
    /// Stops the server. State updates will stop.
    /// </summary>
    void Terminate();
}

/// <summary>
/// Used to initialize the game state before the game begins.
/// May be undeterministic as it is called only for the server and the resulting state is replicated to clients.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <param name="state">Borrow of the game state to be modified.</param>
public delegate void InitStateDelegate<TClientInput, TServerInput, TGameState>(TGameState state)
    where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new();

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
    readonly ILogger logger_ = Log.ForContext<Server<TClientInput, TServerInput, TGameState>>();

    // Locking the holder to stop RW/WR conflicts and correct init behaviour (WW does not happen due to tickMutex)
    readonly StateHolder<TClientInput, TServerInput, TGameState> holder_ = new();

    readonly Clock clock_ = new();
    readonly CancellationTokenSource clockCancellation_ = new();

    readonly IServerInputProvider<TServerInput, TGameState> inputProvider_ = new DefaultServerInputProvider<TServerInput, TGameState>();
    readonly IDisplayer<TGameState> displayer_ = new DefaultDisplayer<TGameState>();
    readonly ClientInputQueue<TClientInput> inputQueue_;

    readonly IServerDispatcher dispatcher_;

    readonly object terminationMutex_ = new(); // Assures atomicity of start and termination operations.
    readonly object tickMutex_ = new(); // Assures tick updates are atomic.

    bool started_ = false;
    bool terminated_ = false;
    bool updatingEnded_ = false;

    /// <summary>
    /// Displayer, will shall be notified with the server state.
    /// </summary>
    public IDisplayer<TGameState> Displayer
    {
        init => displayer_ = value;
    }

    /// <summary>
    /// Server input provider which shall be used to get server inputs. If none is provided <see cref="DefaultServerInputProvider{TServerInput,TGameState}"/> is used.
    /// </summary>
    public IServerInputProvider<TServerInput, TGameState> ServerInputProvider
    {
        init => inputProvider_ = value;
    }

    /// <summary>
    /// Client input predictor. Used to predict client input if none is received in time. If none is provided <see cref="DefaultClientInputPredictor{TClientInput}"/> is used.
    /// </summary>
    public IClientInputPredictor<TClientInput> ClientInputPredictor
    {
        init => inputQueue_ = new ClientInputQueue<TClientInput>(TGameState.DesiredTickRate, value, dispatcher_.SetDelay);
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="dispatcher">Dispatcher to communicate with clients.</param>
    public Server(IServerDispatcher dispatcher)
    {
        dispatcher_ = dispatcher;
        inputQueue_ = new ClientInputQueue<TClientInput>(TGameState.DesiredTickRate, new DefaultClientInputPredictor<TClientInput>(), dispatcher_.SetDelay);
        SetHandlers();
    }

    void SetHandlers()
    {
        dispatcher_.OnAddClient += AddClient;
        dispatcher_.OnAddInput += AddInput;
        dispatcher_.OnRemoveClient += FinishClient;
    }

    void UnsetHandlers()
    {
        dispatcher_.OnAddClient -= AddClient;
        dispatcher_.OnAddInput -= AddInput;
        dispatcher_.OnRemoveClient -= FinishClient;
    }

    /// <inheritdoc/>
    public bool TraceState { get; set; }

    /// <inheritdoc/>
    public bool SendChecksum { get; set; }

    /// <inheritdoc/>
    public bool TraceFrameTime { get; init; }

    /// <summary>
    /// Invoked once when the server starts.
    /// May be used to modify the initial authoritative game state.
    /// </summary>
    public event InitStateDelegate<TClientInput, TServerInput, TGameState>? OnStateInit; 

    /// <inheritdoc/>
    public async Task RunAsync()
    {
        lock (terminationMutex_)
        {
            if (terminated_)
                throw new InvalidOperationException("The server has been terminated.");

            if (started_)
                throw new InvalidOperationException("The server has already been started.");

            started_ = true;
            clock_.TargetTps = TGameState.DesiredTickRate;
            clock_.OnTick += Tick;
        }

        lock (holder_)
            OnStateInit?.Invoke(holder_.State);

        await clock_.RunAsync(clockCancellation_.Token);
    }

    /// <inheritdoc/>
    public void Terminate()
    {
        lock (terminationMutex_)
        {
            if (terminated_)
                return;

            terminated_ = true;
            clock_.OnTick -= Tick;
            clockCancellation_.Cancel();
            authInputWriter_.Dispose();
            UnsetHandlers();
        }
    }

    readonly UpdateTimer timer_ = new();

    readonly PooledBufferWriter<byte> authInputWriter_ = new();
    readonly PooledBufferWriter<byte> traceStateWriter_ = new();

    UpdateInput<TClientInput, TServerInput> GatherInput()
    {
        var clientInput = inputQueue_.ConstructAuthoritativeFrame();
        TServerInput serverInput = inputProvider_.GetInput(holder_.State);
        return new(clientInput, serverInput);
    }

    void HandleKicking(long[]? clientsToKick)
    {
        if (clientsToKick is not { Length: > 0 } toTerminate)
            return;

        foreach (long client in toTerminate)
            dispatcher_.Kick(client);
    }

    (UpdateOutput output, Memory<byte> trace, long? checksum) StateUpdate(UpdateInput<TClientInput, TServerInput> input)
    {
        UpdateOutput output;
        lock (holder_)
            output = holder_.Update(input);
        
        // It is ok to release to lock earlier, as update is atomic and the state is modified only in the update (concurrent reading is allowed).

        displayer_.AddAuthoritative(holder_.Frame, holder_.State);
        var trace = TraceState ? traceStateWriter_.MemoryPackSerialize(holder_.State) : Memory<byte>.Empty;
        long? checksum = SendChecksum ? holder_.GetChecksum() : null;
        return (output, trace, checksum);
    }

    long Update()
    {
        var input = GatherInput();

        long frame = holder_.Frame + 1;

        // Update
        (UpdateOutput output, var trace, long? checksum) = StateUpdate(input);

        if (!trace.IsEmpty)
            logger_.Verbose("Finished state update for {Frame} resulting with {State}", frame, trace);

        HandleKicking(output.ClientsToTerminate);

        dispatcher_.SendAuthoritativeInput(frame, checksum, input);

        if (output.ShallStop)
        {
            updatingEnded_ = true;
            Terminate();
        }

        return frame;
    }

    void Tick()
    {
        lock (tickMutex_)
        {
            if (updatingEnded_)
                return;
            
            if (TraceFrameTime)
                timer_.Start();

            long frame = Update();

            if (TraceFrameTime)
                timer_.End(frame);
        }
    }

    void AddClient(long id)
    {
        inputQueue_.AddClient(id);
        long frame;

        // We need to synchronize the state and assure an update is not in progress.
        // The client won't miss next input's as the lock is not released until the client is initialized.
        lock (holder_)
        {
            frame = holder_.Frame;
            dispatcher_.Initialize(id, frame, holder_.State);
        }

        logger_.Debug("Initialized {Id} for {Frame}.", id, frame);
    }

    void AddInput(long id, long frame, ReadOnlySpan<byte> serializedInput)
    {
        var input = MemoryPackSerializer.Deserialize<TClientInput>(serializedInput);

        if (input is null)
        {
            logger_.Debug("Got invalid input.");
            return;
        }

        inputQueue_.AddInput(id, frame, input);
    }

    void FinishClient(long id)
    {
        logger_.Debug("Client {Id} disconnected.", id);
        inputQueue_.RemoveClient(id);
    }
}
