namespace Core.Transport;

public interface IServerTransport : IServerInTransport, IServerOutTransport
{ }

public interface IServerInTransport
{
    event Action<long, Memory<byte>> OnReliableMessage;
    event Action<long, Memory<byte>> OnUnreliableMessage;
    event Action<long> OnClientJoin;
    event Action<long> OnClientFinish;
}

public interface IServerOutTransport
{
    int ReliableMessageHeader { get; }
    void SendReliable(Memory<byte> message);
    void SendReliable(Memory<byte> message, long id);
    int UnreliableMessageHeader { get; }
    int UnreliableMessageMaxLength { get; }
    void SendUnreliable(Memory<byte> message);
    void SendUnreliable(Memory<byte> message, long id);
    void Terminate(long id);
}
