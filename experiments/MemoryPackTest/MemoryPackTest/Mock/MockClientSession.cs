using System;
using System.Threading.Tasks;

namespace FrameworkTest;

public class MockClientSession : IClientSession
{
    public Task Start()
    {
        if (serverApi_ is not null)
            throw new InvalidOperationException("The session is already started.");
        
        Console.WriteLine("Creating new client session.");

        Api api = new()
        {
            Terminate = () => OnTerminated(),
            MissedInput = (frame, currentFrame) => OnMissedInput?.Invoke(frame, currentFrame),
            EarlyInput = (frame, currentFrame) => OnEarlyInput?.Invoke(frame, currentFrame),
            Initiate = (frame, data) => OnInitiate?.Invoke(frame, data),
            GameInput = (frame, data) => OnGameInput?.Invoke(frame, data)
        };

        serverApi_ = serverSession_.ConnectMock(api);

        serverApi_?.Connect();

        return Task.CompletedTask;
    }

    readonly MockServerSession serverSession_;
    MockServerSession.Api? serverApi_;

    internal class Api
    {
        public required Action Terminate  { get; init; }
        public required Action<long, long> MissedInput { get; init; }
        public required Action<long, long> EarlyInput { get; init; }
        public required Action<long, ReadOnlyMemory<byte>> Initiate { get; init; }
        public required Action<long, ReadOnlyMemory<byte>> GameInput { get; init; }
    }

    public MockClientSession(MockServerSession serverSession)
    {
        serverSession_ = serverSession;

        OnTerminated = () =>
        {
            serverApi_ = null;
        };
    }
    
    public event Action OnTerminated;
    public event Action<long, long>? OnMissedInput;
    public event Action<long, long>? OnEarlyInput;
    public event Action<long, ReadOnlyMemory<byte>>? OnInitiate;
    public event Action<long, ReadOnlyMemory<byte>>? OnGameInput;

    public void Terminate()
    {
        serverApi_?.Disconnect();
        serverApi_ = null;
        OnTerminated();
    }

    public void SendInput(long frame, ReadOnlyMemory<byte> input) => serverApi_?.Input(frame, input);

    public void Dispose() => Terminate();
}
