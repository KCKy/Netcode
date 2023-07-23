using System;
using System.Threading.Tasks;

namespace FrameworkTest;

public interface IClientSession : IDisposable
{
    Task<long> StartAsync();
    void Terminate();
    void SendInput(long frame, ReadOnlyMemory<byte> input);

    event Action OnTerminated;
    event Action<long, long> OnMissedInput;
    event Action<long, long> OnEarlyInput;
    event Action<long, ReadOnlyMemory<byte>> OnInitiate;
    event Action<long, ReadOnlyMemory<byte>> OnGameInput;
}
