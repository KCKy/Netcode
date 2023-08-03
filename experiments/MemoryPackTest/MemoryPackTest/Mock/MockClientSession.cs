using System;
using System.Threading.Tasks;
using FrameworkTest.Extensions;

namespace FrameworkTest;

class Api
{
    public required DelayDistribution Delay { get; set; }

    // Server
    public void Terminate() => Delay.RunDelayed(() => OnTerminated?.Invoke());
    public void MissedInput(long frame, long currentFrame) => Delay.RunDelayed(() => OnMissedInput?.Invoke(frame, currentFrame));
    public void EarlyInput(long frame, long currentFrame) => Delay.RunDelayed(() => OnEarlyInput?.Invoke(frame, currentFrame));
    public void Initiate(long frame, ReadOnlyMemory<byte> data) => Delay.RunDelayed(() => OnInitiate?.Invoke(frame, data));
    public void GameInput(long frame, ReadOnlyMemory<byte> data) => Delay.RunDelayed(() => OnGameInput?.Invoke(frame, data));

    public event Action? OnTerminated;
    public event Action<long, long>? OnMissedInput;
    public event Action<long, long>? OnEarlyInput;
    public event Action<long, ReadOnlyMemory<byte>>? OnInitiate;
    public event Action<long, ReadOnlyMemory<byte>>? OnGameInput;

    // Client
    public void Connect() => Delay.RunDelayed(() => OnConnect?.Invoke());
    public void Disconnect() => Delay.RunDelayed(() => OnDisconnect?.Invoke());
    public void Input(long frame, ReadOnlyMemory<byte> data) => Delay.RunDelayed(() => OnInput?.Invoke(frame, data));

    public event Action? OnConnect;
    public event Action? OnDisconnect;
    public event Action<long, ReadOnlyMemory<byte>>? OnInput;
}

public sealed class MockClientSession : IClientSession
{
    public Task<long> StartAsync()
    {
        if (api_ is not null)
            throw new InvalidOperationException("The session is already started.");
        
        Console.WriteLine("Creating new client session.");

        api_ = new Api()
        {
            Delay = delay_
        };

        api_.OnTerminated += () => OnTerminated?.Invoke();
        api_.OnMissedInput += (a, b) => OnMissedInput?.Invoke(a, b);
        api_.OnEarlyInput += (a, b) => OnEarlyInput?.Invoke(a, b);
        api_.OnInitiate += (a, b) => OnInitiate?.Invoke(a, b);
        api_.OnGameInput += (a, b) => OnGameInput?.Invoke(a, b);

        long id = serverSession_.ConnectMock(api_);
        
        api_.Connect();

        return Task.FromResult(id);
    }

    Api? api_;
    readonly MockServerSession serverSession_;
    readonly DelayDistribution delay_;
    
    public MockClientSession(MockServerSession serverSession, double meanDelay, double delayVariance)
    {
        serverSession_ = serverSession;
        delay_ = new()
        {
            ExpectedValue = meanDelay,
            Variance = delayVariance
        };
    }
    
    public void Terminate()
    {
        api_?.Disconnect();
        api_ = null;
    }

    public void SendInput(long frame, ReadOnlyMemory<byte> input) => api_?.Input(frame, input);
    
    public event Action? OnTerminated;
    public event Action<long, long>? OnMissedInput;
    public event Action<long, long>? OnEarlyInput;
    public event Action<long, ReadOnlyMemory<byte>>? OnInitiate;
    public event Action<long, ReadOnlyMemory<byte>>? OnGameInput;

    public void Dispose() => Terminate();
}
