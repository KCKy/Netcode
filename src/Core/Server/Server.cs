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

    public void AddClient(long id)
    {
        throw new NotImplementedException();
    }

    public void AddInput(long id, long frame, Memory<byte> serializedInput)
    {
        var originalInput = ObjectPool<TPlayerInput>.Create();
        var input = originalInput;

        var inputSpan = serializedInput.Span;
        
        MemoryPackSerializer.Deserialize(inputSpan, ref input);

        if (!ReferenceEquals(originalInput, input))
            ObjectPool<TPlayerInput>.Destroy(originalInput);

        if (input is null)
        {
            logger_.Debug("Got invalid {Input}.", serializedInput);
            return;
        }

        InputQueue.AddInput(id, frame, input);
    }

    public void FinishClient(long id)
    {
        InputQueue.RemoveClient(id);
    }
}

public sealed class Server<TPlayerInput, TServerInput, TGameState, TUpdateInfo>
    where TGameState : class, IGameState<TPlayerInput, TServerInput>, new()
    where TPlayerInput : class, new()
    where TServerInput : class, new()
{
    public IServerInputProvider<TServerInput, TUpdateInfo> InputProvider { get; set; } = new DefaultServerInputProvider<TServerInput, TUpdateInfo>();
    public IDisplayer<TGameState> Displayer { get; set; } = new DefaultDisplayer<TGameState>();    
    public required IServerDispatcher Dispatcher { get; set; }

    readonly IClientInputQueue<TPlayerInput> inputQueue_ = new ClientInputQueue<TPlayerInput>();

    public IServerSession Session { get; }

    public Server()
    {
        Session = new ServerSession<TPlayerInput>()
        {
            InputQueue = inputQueue_
        };
    }

    public static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1d / TGameState.DesiredTickRate);

    public async Task RunAsync()
    {
        throw new NotImplementedException();
    }
}
