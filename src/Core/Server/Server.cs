using Core.DataStructures;
using Core.Providers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;

namespace Core.Server;

internal class ServerSession<TPlayerInput> : IServerSession
    where TPlayerInput : class, new()
{
    readonly ILogger logger_ = Log.ForContext<ServerSession<TPlayerInput>>();

    public required IClientInputQueue<TPlayerInput> InputQueue { get; init; }
    public required IClientManager ClientManager { get; init; } 

    public void AddClient(long id)
    {
        lock (InputQueue)
            InputQueue.AddClient(id);
    }

    public void AddInput(long id, long frame, Memory<byte> serializedInput)
    {
        var input = MemoryPackSerializer.Deserialize<TPlayerInput>(serializedInput.Span);

        if (input is null)
        {
            logger_.Debug("Got invalid {Input}.", serializedInput);
            return;
        }

        lock (InputQueue)
            InputQueue.AddInput(id, frame, input);
    }

    public void FinishClient(long id)
    {
        InputQueue.RemoveClient(id);
    }
}

internal interface IClientManager
{
    void AddClient(long id);
    void RemoveClient(long id);
}

internal sealed class StateUpdate<TPlayerInput, TServerInput, TGameState, TUpdateInfo> : IClientManager
    where TGameState : class, IGameState<TPlayerInput, TServerInput>, new()
    where TPlayerInput : class, new()
    where TServerInput : class, new()
{
    public void AddClient(long id) => throw new NotImplementedException();
    public void Update() => throw new NotImplementedException();
    public void RemoveClient(long id) => throw new NotImplementedException();
}

public sealed class Server<TPlayerInput, TServerInput, TGameState, TUpdateInfo>
    where TGameState : class, IGameState<TPlayerInput, TServerInput>, new()
    where TPlayerInput : class, new()
    where TServerInput : class, new()
{
    public IServerInputProvider<TServerInput, TUpdateInfo> InputProvider { get; set; } = new DefaultServerInputProvider<TServerInput, TUpdateInfo>();
    public IDisplayer<TGameState> Displayer { get; set; } = new DefaultDisplayer<TGameState>();    
    
    public required IServerDispatcher Dispatcher { get; set; }
    public IServerSession Session { get; }
    
    readonly IClientInputQueue<TPlayerInput> inputQueue_;
    readonly IClientManager manager_;
    readonly IClock clock_;

    public Server()
    {
        inputQueue_ = new ClientInputQueue<TPlayerInput>();

        manager_ = new StateUpdate<TPlayerInput, TServerInput, TGameState, TUpdateInfo>();

        Session = new ServerSession<TPlayerInput>()
        {
            InputQueue = inputQueue_,
            ClientManager = manager_
        };

        clock_ = new BasicClock()
        {
            Period = FrameInterval
        };
    }

    internal Server(IClientInputQueue<TPlayerInput> queue, IClientManager manager, IServerSession session, IClock clock)
    {
        inputQueue_ = queue;
        manager_ = manager;
        Session = session;
        clock_ = clock;
    }

    public static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1d / TGameState.DesiredTickRate);

    public async Task RunAsync()
    {
        throw new NotImplementedException();
    }
}
