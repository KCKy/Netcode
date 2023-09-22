using System.Diagnostics;
using Core.DataStructures;
using Core.Providers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;

namespace Core.Server;

public sealed class Server<TPlayerInput, TServerInput, TGameState, TUpdateInfo> : IServerSession
    where TGameState : class, IGameState<TPlayerInput, TServerInput, TUpdateInfo>, new()
    where TPlayerInput : class, new()
    where TServerInput : class, new()
    where TUpdateInfo : new()
{
    public IServerInputProvider<TServerInput, TUpdateInfo> InputProvider { get; set; } = new DefaultServerInputProvider<TServerInput, TUpdateInfo>();
    public IDisplayer<TGameState> Displayer { get; set; } = new DefaultDisplayer<TGameState>();    
    
    public required IServerDispatcher Dispatcher { get; init; }
    public IServerSession Session => this;

    public bool TraceState { get; set; }
    public bool SendChecksums { get; set; }
    
    readonly IClientInputQueue<TPlayerInput> inputQueue_;
    readonly IStateManager<TPlayerInput, TServerInput, TGameState, TUpdateInfo> manager_;
    readonly IClock clock_;
    readonly CancellationTokenSource clockCancellation_ = new();

    static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1d / TGameState.DesiredTickRate);

    void IServerSession.AddClient(long id)
    {
        inputQueue_.AddClient(id);
        
        Memory<byte> serializedState;
        long frame;

        lock (manager_)
        {
            serializedState = manager_.Serialize();
            frame = manager_.Frame;
        }

        logger_.Debug("Initialized {Id} for {Frame} with {State}", id, frame, TraceState ? serializedState : Array.Empty<byte>());

        Dispatcher.Initialize(id, frame, serializedState);
    }

    void IServerSession.AddInput(long id, long frame, Memory<byte> serializedInput)
    {
        var input = MemoryPackSerializer.Deserialize<TPlayerInput>(serializedInput.Span);

        if (input is null)
        {
            logger_.Debug("Got invalid {Input}.", serializedInput);
            return;
        }

        inputQueue_.AddInput(id, frame, input);
    }

    void IServerSession.FinishClient(long id)
    {
        inputQueue_.RemoveClient(id);
    }
    
    public Server()
    {
        inputQueue_ = new ClientInputQueue<TPlayerInput>();

        manager_ = new StateUpdate<TPlayerInput, TServerInput, TGameState, TUpdateInfo>();

        clock_ = new BasicClock()
        {
            Period = FrameInterval
        };
    }

    internal Server(IClientInputQueue<TPlayerInput> queue, IStateManager<TPlayerInput, TServerInput, TGameState, TUpdateInfo> manager, IClock clock)
    {
        inputQueue_ = queue;
        manager_ = manager;
        clock_ = clock;
    }

    readonly object tickMutex_ = new();
    bool updatingEnded_ = false;

    readonly ILogger logger_ = Log.ForContext<Server<TPlayerInput, TServerInput, TGameState, TUpdateInfo>>();

    TUpdateInfo updateInfo_ = new();

    (UpdateOutput output, byte[]? trace, long? checksum, long frame) AtomicUpdate(UpdateInput<TPlayerInput, TServerInput> input)
    {
        lock (manager_)
        {
            UpdateOutput output = manager_.Update(input, ref updateInfo_);
            long frame = manager_.Frame;

            Displayer.AddAuthoritative(manager_.Frame, manager_.State);

            byte[]? trace = TraceState ? MemoryPackSerializer.Serialize(manager_.State) : null;
            long? checksum = SendChecksums ? manager_.GetChecksum() : null;

            return (output, trace, checksum, frame);
        }
    }

    void Tick()
    {
        lock (tickMutex_)
        {
            if (updatingEnded_)
                return;

            var clientInput = inputQueue_.ConstructAuthoritativeFrame();
            TServerInput serverInput = InputProvider.GetInput(ref updateInfo_);

            UpdateInput<TPlayerInput, TServerInput> input = new(clientInput, serverInput);
            byte[] serializedInput = MemoryPackSerializer.Serialize(input);

            (UpdateOutput output, byte[]? trace, long? checksum, long frame) = AtomicUpdate(input);

            if (trace is not null)
                logger_.Verbose("Finished state update for {Frame} with {Input} resulting with {State}", frame, serializedInput, trace);

            if (output.ClientsToTerminate is { Length: > 0 } toTerminate)
                foreach (long client in toTerminate)
                    Dispatcher.Kick(client);

            Dispatcher.SendAuthoritativeInput(frame,serializedInput, checksum);

            if (output.ShallStop)
            {
                updatingEnded_ = true;
                Terminate();
            }
        }
    }

    readonly object terminationMutex_ = new();
    bool started_ = false;
    bool terminated_ = false;

    public async Task Start()
    {
        lock (terminationMutex_)
        {
            if (started_ || terminated_)
                return;

            started_ = true;
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
        }
    }
}
