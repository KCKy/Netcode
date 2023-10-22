namespace Core.Transport;

// TODO: finish docs (thread safety)

public interface IClientTransport : IClientInTransport, IClientOutTransport { }

public interface IClientInTransport
{
    /// <summary>
    /// Event which is invoked when a reliable message from a server is received.
    /// </summary>
    event Action<Memory<byte>> OnReliableMessage;

    /// <summary>
    /// Event which is invoked when an unreliable message from a server is received.
    /// </summary>
    event Action<Memory<byte>> OnUnreliableMessage;
}

public interface IClientOutTransport
{
    void Terminate();

    int ReliableMessageHeader { get; }
    void SendReliable(Memory<byte> message);

    int UnreliableMessageHeader { get; }
    int UnreliableMessageMaxLength { get; }

    /// <summary>
    /// Sends a message to the server. The server may not receive this message due to packet loss.
    /// </summary>
    /// <param name="message"></param>
    void SendUnreliable(Memory<byte> message);
}
