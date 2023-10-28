using Core.DataStructures;
using Core.Providers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace Core.Server;

public sealed class Server<TClientInput, TServerInput, TGameState>
    where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
{
    readonly IStateHolder<TClientInput, TServerInput, TGameState> holder_;
    readonly IClientInputQueue<TClientInput> inputQueue_;
    readonly ILogger logger_ = Log.ForContext<Server<TClientInput, TServerInput, TGameState>>();
    readonly IClock clock_;
    readonly CancellationTokenSource clockCancellation_ = new();
    readonly IServerInputProvider<TServerInput, TGameState> inputProvider_;
    readonly IClientInputPredictor<TClientInput> inputPredictor_;
    readonly IDisplayer<TGameState> displayer_;
    readonly IServerSender sender_;
    readonly IServerReceiver receiver_;

    readonly object terminationMutex_ = new();
    readonly object tickMutex_ = new();

    bool started_ = false;
    bool terminated_ = false;
    bool updatingEnded_ = false;

    public Server(IServerSender sender, IServerReceiver receiver,
        IDisplayer<TGameState>? displayer = null,
        IServerInputProvider<TServerInput, TGameState>? serverProvider = null,
        IClientInputPredictor<TClientInput>? inputPredictor = null)
    {
        sender_ = sender;
        receiver_ = receiver;
        displayer_ = displayer ?? new DefaultDisplayer<TGameState>();
        inputProvider_ = serverProvider ?? new DefaultServerInputProvider<TServerInput, TGameState>();
        inputPredictor_ = inputPredictor ?? new DefaultClientInputPredictor<TClientInput>();
        inputQueue_ = new ClientInputQueue<TClientInput>(TGameState.DesiredTickRate, inputPredictor_);
        holder_ = new StateHolder<TClientInput, TServerInput, TGameState>();
        clock_ = new BasicClock();
        
        timer_ = new(logger_);
        SetHandlers();
    }

    internal Server(IServerSender sender, IServerReceiver receiver,
        IDisplayer<TGameState> displayer,
        IServerInputProvider<TServerInput, TGameState> serverProvider,
        IClientInputQueue<TClientInput> queue,
        IStateHolder<TClientInput, TServerInput, TGameState> holder,
        IClock clock, IClientInputPredictor<TClientInput> inputPredictor)
    {
        sender_ = sender;
        receiver_ = receiver;
        displayer_ = displayer;
        inputProvider_ = serverProvider;
        inputQueue_ = queue;
        holder_ = holder;
        clock_ = clock;
        inputPredictor_ = inputPredictor;

        timer_ = new(logger_);
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

    public bool TraceState { get; set; }
    public bool SendChecksum { get; set; }
    public bool TraceFrameTime { get; set; }

    public async Task RunAsync()
    {
        lock (terminationMutex_)
        {
            if (started_ || terminated_)
                return;

            started_ = true;
            clock_.TargetTPS = TGameState.DesiredTickRate;
            clock_.OnTick += Tick;
        }

        try
        {
            await clock_.RunAsync(clockCancellation_.Token);
        }
        catch (OperationCanceledException) { }
    }

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

    long Update()
    {
        // Gather input
        var clientInput = inputQueue_.ConstructAuthoritativeFrame();
        long frame = holder_.Frame + 1;

        TServerInput serverInput = inputProvider_.GetInput(holder_.State);
        UpdateInput<TClientInput, TServerInput> input = new(clientInput, serverInput);

        // Update
        UpdateOutput output;
        lock (holder_)
            output = holder_.Update(input);
        
        displayer_.AddAuthoritative(holder_.Frame, holder_.State);
        var trace = TraceState ? traceStateWriter_.MemoryPackSerialize(holder_.State) : Memory<byte>.Empty;
        long? checksum = SendChecksum ? holder_.GetChecksum() : null;

        // Trace
        if (!trace.IsEmpty)
            logger_.Verbose("Finished state update for {Frame} resulting with {State}", frame, trace);

        // Kick?
        if (output.ClientsToTerminate is { Length: > 0 } toTerminate)
            foreach (long client in toTerminate)
                sender_.Kick(client);

        // Send
        sender_.SendAuthoritativeInput(frame, checksum, input);

        // Stop?
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

        lock (holder_)
        {
            long frame = holder_.Frame;
            sender_.Initialize(id, frame, holder_.State);
            logger_.Debug("Initialized {Id} for {Frame}.", id, frame);
        }
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
