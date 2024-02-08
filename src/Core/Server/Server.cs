using Core.DataStructures;
using Core.Providers;
using Core.Timing;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace Core.Server;

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
    // Locking the holder stop RW/WR conflicts and correct init behaviour (WW does not happen due to tickMutex)
    readonly IStateHolder<TClientInput, TServerInput, TGameState> holder_; 
    readonly IClientInputQueue<TClientInput> inputQueue_;
    readonly ILogger logger_ = Log.ForContext<Server<TClientInput, TServerInput, TGameState>>();
    readonly IClock clock_;
    readonly CancellationTokenSource clockCancellation_ = new();
    readonly IServerInputProvider<TServerInput, TGameState> inputProvider_;
    readonly IDisplayer<TGameState> displayer_;
    readonly IServerSender sender_;
    readonly IServerReceiver receiver_;

    readonly object terminationMutex_ = new(); // Assures atomicity of start and termination operations.
    readonly object tickMutex_ = new(); // Assures tick updates are atomic.

    bool started_ = false;
    bool terminated_ = false;
    bool updatingEnded_ = false;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="sender">Sender to use for sending messages to clients.</param>
    /// <param name="receiver">Receiver to use for receiving messages from clients.</param>
    /// <param name="displayer">Optional displayer to display the server state.</param>
    /// <param name="serverProvider">Optional server input provider. If none is provided <see cref="DefaultServerInputProvider{TServerInput,TGameState}"/> is used.</param>
    /// <param name="inputPredictor">Optional client input predictor. If none is provided <see cref="DefaultClientInputPredictor{TClientInput}"/> is used.</param>
    public Server(IServerSender sender, IServerReceiver receiver,
        IDisplayer<TGameState>? displayer = null,
        IServerInputProvider<TServerInput, TGameState>? serverProvider = null,
        IClientInputPredictor<TClientInput>? inputPredictor = null)
    {
        sender_ = sender;
        receiver_ = receiver;
        displayer_ = displayer ?? new DefaultDisplayer<TGameState>();
        inputProvider_ = serverProvider ?? new DefaultServerInputProvider<TServerInput, TGameState>();
        inputQueue_ = new ClientInputQueue<TClientInput>(TGameState.DesiredTickRate,
            inputPredictor ?? new DefaultClientInputPredictor<TClientInput>());
        holder_ = new StateHolder<TClientInput, TServerInput, TGameState>();
        clock_ = new Clock();
        timer_ = new();
        SetHandlers();
    }

    internal Server(IServerSender sender, IServerReceiver receiver,
        IDisplayer<TGameState> displayer,
        IServerInputProvider<TServerInput, TGameState> serverProvider,
        IClientInputQueue<TClientInput> queue,
        IStateHolder<TClientInput, TServerInput, TGameState> holder,
        IClock clock)
    {
        sender_ = sender;
        receiver_ = receiver;
        displayer_ = displayer;
        inputProvider_ = serverProvider;
        inputQueue_ = queue;
        holder_ = holder;
        clock_ = clock;
        timer_ = new();
        SetHandlers();
    }

    void SetHandlers()
    {
        inputQueue_.OnInputAuthored += sender_.InputAuthored;
        receiver_.OnAddClient += AddClient;
        receiver_.OnAddInput += AddInput;
        receiver_.OnRemoveClient += FinishClient;
    }

    void UnsetHandlers()
    {
        inputQueue_.OnInputAuthored -= sender_.InputAuthored;
        receiver_.OnAddClient -= AddClient;
        receiver_.OnAddInput -= AddInput;
        receiver_.OnRemoveClient -= FinishClient;
    }

    /// <inheritdoc/>
    public bool TraceState { get; set; }

    /// <inheritdoc/>
    public bool SendChecksum { get; set; }

    /// <inheritdoc/>
    public bool TraceFrameTime { get; init; }

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

    readonly UpdateTimer timer_;

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
            sender_.Kick(client);
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

        sender_.SendAuthoritativeInput(frame, checksum, input);

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
            sender_.Initialize(id, frame, holder_.State);
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
