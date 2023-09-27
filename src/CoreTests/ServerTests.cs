using Core;
using Core.Providers;
using Core.Server;
using Core.Transport;

namespace CoreTests;
/*
class MockClientIn { }

class MockServerIn { }

class MockState : IGameState<MockClientIn, MockServerIn, MockInfo>
{
    public UpdateOutput Update(UpdateInput<MockClientIn, MockServerIn> updateInputs, ref MockInfo updateInfo)
    {
        throw new NotImplementedException();
    }

    public static double DesiredTickRate => 1f;
}

struct MockInfo { }

class MockClock : IClock
{
    public event Action? OnTick;
    public double TargetTPS { get; set; }

    public void Tick()
    {

    }

    readonly TaskCompletionSource runningSource_ = new();
    public Task Running;

    public MockClock()
    {
        Running = runningSource_.Task;
    }

    public Task RunAsync(CancellationToken cancelToken = new CancellationToken())
    {
        runningSource_.SetResult();
        return Task.Delay(-1, cancelToken);
    }
}

class MockDispatcher : IServerDispatcher
{
    public void Kick(long id)
    {
        throw new NotImplementedException();
    }

    public void Initialize(long id, long frame, Memory<byte> state)
    {
        throw new NotImplementedException();
    }

    public void SendAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        throw new NotImplementedException();
    }
}

public class ServerTests
{
    
}
*/
