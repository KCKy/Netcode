using System;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Transport;

/// <summary>
/// The server transport layer of the framework. Provides abstraction over management of connected clients and receiving and sending client messages.
/// Reliable messages shall always be delivered in the order they were sent. Unreliable messages may not be received or received out of the sending order.
/// Each client connection is assigned a unique ID, which is valid for the whole duration of the server's runtime.
/// </summary>
/// <remarks>
/// This structure is thread safe. It is safe to call methods of this interface concurrently (although sending of reliable messages could be a race condition for the sending order).
/// Calling methods on a terminated server is safe and will do nothing.
/// </remarks>
public interface IServerTransport : IServerInTransport, IServerOutTransport
{
    /// <summary>
    /// Terminate the transport instance.
    /// All transport operation shall stop soon.
    /// </summary>
    void Terminate();

    /// <summary>
    /// Start the transport instance.
    /// The transport will listen for new clients and start sending/receiving binary messages.
    /// </summary>
    /// <returns>Task representing the transport lifetime.</returns>
    Task RunAsync();
}

/// <summary>
/// A message from a client to the server.
/// </summary>
/// <param name="id">The id of the client the message is from.</param>
/// <param name="packet">Move of the received packet (pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>

public delegate void ServerMessageEvent(int id, Memory<byte> packet);

/// <summary>
/// The receiving part of <see cref="IServerTransport"/>.
/// </summary>
public interface IServerInTransport
{
    /// <summary>
    /// Event which is invoked when a reliable message from a client is received.
    /// </summary>
    event ServerMessageEvent OnReliableMessage;

    /// <summary>
    /// Event which is invoked when an unreliable message from a client is received.
    /// </summary>
    event ServerMessageEvent OnUnreliableMessage;

    /// <summary>
    /// Event which is invoked when a new client connects to the server. The id of the connection is passed.
    /// </summary>
    event Action<int> OnClientJoin;

    /// <summary>
    /// Event which is invoked when a new client disconnects from the server due to any reason. The id of the just ended connection is passed.
    /// </summary>
    event Action<int> OnClientFinish;
}

/// <summary>
/// The sending part of <see cref="IServerTransport"/>.
/// </summary>
public interface IServerOutTransport
{
    /// <summary>
    /// The amount of bytes of unused leading space reliable packets should have.
    /// </summary>
    /// <remarks>
    /// They will be filled with the transport header.
    /// </remarks>
    int ReliableMessageHeader { get; }

    /// <summary>
    /// Send a reliable message to all the currently connected clients. 
    /// </summary>
    /// <param name="message">Move of the packet to be sent (pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>
    /// <remarks>The packet should have leading space as defined by <see cref="ReliableMessageHeader"/>.</remarks>
    void SendReliable(Memory<byte> message);

    /// <summary>
    /// Send a reliable message to a specific connected client. 
    /// </summary>
    /// <param name="message">Move of the packet to be sent (pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>
    /// <remarks>The packet should have leading space as defined by <see cref="ReliableMessageHeader"/>.</remarks>
    /// <param name="id">The id of the client to send the message to. For an ID of a disconnected client the method will do nothing.</param>
    void SendReliable(Memory<byte> message, int id);

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
    /// Sends an unreliable message to all the currently connected clients. Some of the clients may not receive the message.
    /// </summary>
    /// <param name="message">Move of the packet to be sent (pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>
    /// <remarks>The packet should have leading space as defined by <see cref="UnreliableMessageHeader"/>.</remarks>
    void SendUnreliable(Memory<byte> message);

    /// <summary>
    /// Sends an unreliable message to a specific connected client. They may not receive the message.
    /// </summary>
    /// <param name="message">Move of the packet to be sent (pooled from <see cref="T:ArrayPool{byte}.Shared"/>)</param>
    /// <remarks>The packet should have leading space as defined by <see cref="UnreliableMessageHeader"/>.</remarks>
    /// <param name="id">The id of the client to send the message to. For an ID of a disconnected client the method will do nothing.</param>
    void SendUnreliable(Memory<byte> message, int id);

    /// <summary>
    /// Terminate the connection of a given client. They will be forcefully disconnected.
    /// </summary>
    /// <param name="id">The id of the client to disconnect.</param>
    void Kick(int id);
}
