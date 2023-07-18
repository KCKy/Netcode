using System;
using System.Threading.Tasks;

namespace FrameworkTest;

public interface IServerSession : IDisposable
{
    Task StartAsync();
    void TerminatePlayer(long playerId);
    void SignalMissedInput(long playerId, long frame, long currentFrame);
    void SignalEarlyInput(long playerId, long frame, long currentFrame);
    void InitiatePlayer(long playerId, long frame, ReadOnlyMemory<byte> currentGameState);
    void SendInputToPlayer(long playerId, long frame, ReadOnlyMemory<byte> gameInput);

    event Action<long> OnClientConnect;
    event Action<long> OnClientDisconnect;
    event Action<long, long, ReadOnlyMemory<byte>> OnClientInput;
}
