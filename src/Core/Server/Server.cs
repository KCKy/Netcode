using Core.DataStructures;
using Core.Extensions;
using Core.Providers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;

namespace Core.Server;

public sealed class Server<TClientInput, TServerInput, TGameState> : IServerSession
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
    readonly IDisplayer<TGameState> displayer_;
    readonly IServerDispatcher dispatcher_;

    readonly object terminationMutex_ = new();
    readonly object tickMutex_ = new();

    bool started_ = false;
    bool terminated_ = false;
    bool updatingEnded_ = false;

    public Server(IServerDispatcher dispatcher,
        IDisplayer<TGameState>? displayer = null,
        IServerInputProvider<TServerInput, TGameState>? serverProvider = null)
    {
        dispatcher_ = dispatcher;
        displayer_ = displayer ?? new DefaultDisplayer<TGameState>();
        inputProvider_ = serverProvider ?? new DefaultServerInputProvider<TServerInput, TGameState>();
        inputQueue_ = new ClientInputQueue<TClientInput>(TGameState.DesiredTickRate);
        holder_ = new StateHolder<TClientInput, TServerInput, TGameState>();
        clock_ = new BasicClock();
        timer_ = new(logger_);
        inputQueue_.OnInputAuthored += dispatcher.InputAuthored;
    }

    internal Server(IServerDispatcher dispatcher,
        IDisplayer<TGameState> displayer,
        IServerInputProvider<TServerInput, TGameState> serverProvider,
        IClientInputQueue<TClientInput> queue,
        IStateHolder<TClientInput, TServerInput, TGameState> holder,
        IClock clock)
    {
        dispatcher_ = dispatcher;
        displayer_ = displayer;
        inputProvider_ = serverProvider;
        inputQueue_ = queue;
        holder_ = holder;
        clock_ = clock;

        timer_ = new(logger_);

        inputQueue_.OnInputAuthored += dispatcher.InputAuthored;
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
        }
    }

    readonly UpdateTimer timer_;

    PooledBufferWriter<byte> authInputWriter_ = new();
    PooledBufferWriter<byte> traceStateWriter_ = new();

    long Update()
    {
        // Gather input
        var clientInput = inputQueue_.ConstructAuthoritativeFrame();
        long frame = holder_.Frame + 1;

        TServerInput serverInput = inputProvider_.GetInput(holder_.State);
        UpdateInput<TClientInput, TServerInput> input = new(clientInput, serverInput);

        var serializedInput = authInputWriter_.MemoryPackSerializeS(input);

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
                dispatcher_.Kick(client);

        // Send
        dispatcher_.SendAuthoritativeInput(frame, serializedInput, checksum);

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

    void IServerSession.AddClient(long id)
    {
        inputQueue_.AddClient(id);
        
        Memory<byte> serializedState;
        long frame;

        lock (holder_)
        {
            serializedState = holder_.Serialize();
            frame = holder_.Frame;
        }

        logger_.Debug("Initialized {Id} for {Frame} with {State}", id, frame, TraceState ? serializedState : Array.Empty<byte>());

        dispatcher_.Initialize(id, frame, serializedState);
    }

    void IServerSession.AddInput(long id, long frame, Memory<byte> serializedInput)
    {
        var input = MemoryPackSerializer.Deserialize<TClientInput>(serializedInput.Span);

        if (input is null)
        {
            logger_.Debug("Got invalid {Input}.", serializedInput);
            goto end;
        }

        inputQueue_.AddInput(id, frame, input);

        end:
        serializedInput.ReturnToArrayPool();
    }

    void IServerSession.FinishClient(long id)
    {
        logger_.Debug("Client {Id} disconnected.", id);
        inputQueue_.RemoveClient(id);
    }
}
