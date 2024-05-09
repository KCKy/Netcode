using System;

namespace Kcky.GameNewt.Transport;

/// <summary>
/// The client transport layer of the framework. Provides abstraction of sending and receiving reliable and unreliable messages to/from the server.
/// Reliable messages shall always be delivered in the order they were sent. Unreliable messages may not be received or received out of the sending order.
/// </summary>
/// <remarks>
/// This structure is thread safe. It is safe to call methods of this interface concurrently (although sending of reliable messages could be a race condition for the sending order).
/// Calling methods on a terminated client is safe and will do nothing.
/// </remarks>
public interface IClientTransport : IClientInTransport, IClientOutTransport { }

/// <summary>
/// A message from the server to the client.
/// </summary>
/// <param name="packet">Move of the received packet (shall be pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>

public delegate void ClientMessageEvent(Memory<byte> packet);

/// <summary>
/// The receiving part of <see cref="IClientTransport"/>.
/// </summary>
public interface IClientInTransport
{
    /// <summary>
    /// Event which is invoked when a reliable message from the server is received.
    /// </summary>
    event ClientMessageEvent OnReliableMessage;

    /// <summary>
    /// Event which is invoked when an unreliable message from the server is received.
    /// </summary>
    event ClientMessageEvent OnUnreliableMessage;
}

/// <summary>
/// The sending part of <see cref="IClientTransport"/>.
/// </summary>
public interface IClientOutTransport
{
    /// <summary>
    /// The amount of bytes of unused leading space reliable packets should have.
    /// </summary>
    /// <remarks>
    /// They will be filled with the transport header.
    /// </remarks>
    int ReliableMessageHeader { get; }
    
    /// <summary>
    /// Send a reliable message to the server. 
    /// </summary>
    /// <param name="message">Move of the packet to be sent (shall be pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>
    /// <remarks>The packet should have leading unused space as defined by <see cref="ReliableMessageHeader"/>.</remarks>
    void SendReliable(Memory<byte> message);

    /// <summary>
    /// The amount of bytes of unused leading space unreliable packets should have.
    /// </summary>
    /// <remarks>
    /// They will be filled with the transport header.
    /// </remarks>
    int UnreliableMessageHeader { get; }

    /// <summary>
    /// The maximum size that an unreliable packet may have to be surely deliverable.
    /// </summary>
    int UnreliableMessageMaxLength { get; }

    /// <summary>
    /// Sends an unreliable message to the server. The server may not receive this message due to packet loss.
    /// </summary>
    /// <param name="message">Move of the packet to be sent (shall be pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>
    /// <remarks>The packet should have leading unused space as defined by <see cref="UnreliableMessageHeader"/>.</remarks>
    void SendUnreliable(Memory<byte> message);
}
