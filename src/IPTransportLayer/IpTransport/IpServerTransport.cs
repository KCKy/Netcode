using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Kcky.Useful;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kcky.GameNewt.Transport.Default;

/// <summary>
/// Information about a connected client.
/// </summary>
class ConnectedClient
{
    public readonly TcpClientTransceiver ClientTransceiver;
    public IPEndPoint UdpTarget;
    public readonly CancellationTokenSource Cancellation;
    public readonly int Id;

    public ConnectedClient(TcpClientTransceiver clientTransceiver, IPEndPoint target, CancellationTokenSource cancellation, int id)
    {
        ClientTransceiver = clientTransceiver;
        UdpTarget = target;
        Cancellation = cancellation;
        Id = id;
    }
}

/// <summary>
/// Implementation of the server transport over TCP/UDP. The server for <see cref="IpClientTransport"/>.
/// </summary>
/// <remarks>
/// The transport is unencrypted and prone to spoofing attacks.
/// The transport creates a TCP and UDP server socket on the same port.
/// </remarks>
public class IpServerTransport : IServerTransport
{
    readonly IPEndPoint local_;
    readonly ILoggerFactory loggerFactory_;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="local">The local endpoint the server shell bind to.</param>
    /// <param name="loggerFactory">Optional logger factory for logging debug info.</param>
    public IpServerTransport(IPEndPoint local, ILoggerFactory? loggerFactory = null)
    {
        loggerFactory_ = loggerFactory ?? NullLoggerFactory.Instance;
        logger_ = loggerFactory_.CreateLogger<IpClientTransport>();
        local_ = local;
    }

    /// <inheritdoc/>
    public event ServerMessageEvent? OnReliableMessage;

    void HandleUdpReceive((Memory<byte> message, int id) value) => OnUnreliableMessage?.Invoke(value.id, value.message);
    
    /// <inheritdoc/>
    public event ServerMessageEvent? OnUnreliableMessage;

    /// <inheritdoc/>
    public event Action<int>? OnClientJoin;

    /// <inheritdoc/>
    public event Action<int>? OnClientFinish;
    
    readonly ConcurrentDictionary<int, ConnectedClient> idToConnection_ = new();
    readonly BagMessages<(Memory<byte> payload, int? id)> udpMessages_ = new();
    readonly CancellationTokenSource cancellationSource_ = new();

    readonly QueueMessages<(Memory<byte>, int?)> tcpMessages_ = new();

    readonly ILogger logger_;
    
    void RemoveClient(int id)
    {
        if (!idToConnection_.TryRemove(id, out _))
            throw new InvalidOperationException("Client was not present.");

        OnClientFinish?.Invoke(id);
    }

    void AddClient(ConnectedClient client)
    {
        if (!idToConnection_.TryAdd(client.Id, client))
            throw new InvalidOperationException("Client is already present.");
    }

    /// <summary>
    /// The local port the server has bound to.
    /// </summary>
    public int Port { get; private set; } = 0;

    async Task RunClientAsync(TcpClient client, int id)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint target || client.TryGetStream() is not { } stream)
        {
            client.Dispose();
            return;
        }

        TcpClientTransceiver layer = new(stream, loggerFactory_);

        ConnectedClient record = new(layer, target, new(), id);
        AddClient(record);

        CancellationToken clientCancellation = record.Cancellation.Token;

        // Give client their identification id
        await SendInitIdAsync(stream, id, clientCancellation);

        Receiver<Memory<byte>> receiver = new(layer, loggerFactory_);
        
        int idCapture = id;
        receiver.OnMessage += m => OnReliableMessage?.Invoke(idCapture, m);

        OnClientJoin?.Invoke(id);

        try
        {
            await receiver.RunAsync(clientCancellation);
        }
        catch (OtherSideEndedException)
        {
            logger_.LogTrace("Client with id {Id} disconnected from server.", id);
        }
        finally
        {   
            logger_.LogTrace("Client with id {Id} was removed.", id);
            RemoveClient(id);
            client.Dispose();
        }
    }

    ValueTask SendInitIdAsync(NetworkStream stream, int id, CancellationToken cancellation)
    {
        Memory<byte> idRaw = new byte[sizeof(int)];
        Bits.Write(id, idRaw.Span);
        return stream.WriteAsync(idRaw, cancellation);
    }

    async Task ManageConnectionsAsync(TcpListener listener, CancellationToken cancellation)
    {
        for (int id = 1; ; id++)
        {
            TcpClient client = await listener.AcceptTcpClientAsync(cancellation);
            RunClientAsync(client, id).AssureNoFault(logger_);
            logger_.LogTrace("Client connected to server with id {Id}.", id);
        }
    }

    volatile int hasStarted_ = 0;

    /// <summary>
    /// Start the server.
    /// </summary>
    /// <remarks>
    /// This method is not thread safe and is expected to be run once.
    /// The task will return cancelled, if the connection was terminated server side, or with an exception if the underlying network socket fails.
    /// </remarks>
    /// <returns>Task representing the server lifetime.</returns>
    /// <exception cref="InvalidOperationException">If the local endpoint is not valid or the server has already been started once.</exception>
    public async Task RunAsync()
    {
        if (Interlocked.CompareExchange(ref hasStarted_, 1, 0) != 0)
            throw new InvalidOperationException("The server has already started.");

        CancellationToken cancellation = cancellationSource_.Token;

        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.Bind(local_);

        IPEndPoint endpoint = udp.LocalEndPoint as IPEndPoint ?? throw new InvalidOperationException();

        TcpListener tpcListener = new(endpoint);
        tpcListener.Start();

        Port = endpoint.Port;

        logger_.LogInformation("Began server at {Local} -> {Transformed}.", local_, endpoint);
        
        UdpServerTransceiver udpTransceiver = new(udp, idToConnection_, loggerFactory_);
        TcpServerTransceiver tcpServerTransceiver = new(idToConnection_, loggerFactory_);

        Receiver<(Memory<byte>, int)> udpReceiver = new(udpTransceiver, loggerFactory_);
        Sender<BagMessages<(Memory<byte>, int?)>, (Memory<byte>, int?)> udpMemorySender = new(udpTransceiver, udpMessages_, loggerFactory_);
        Sender<QueueMessages<(Memory<byte>, int?)>, (Memory<byte>, int?)> tcpMemorySender = new(tcpServerTransceiver,  tcpMessages_, loggerFactory_);

        udpReceiver.OnMessage += HandleUdpReceive;
        
        Task tcpManageTask = ManageConnectionsAsync(tpcListener, cancellation);
        Task udpReceiveTask = udpReceiver.RunAsync(cancellation);
        Task udpSendTask = udpMemorySender.RunAsync(cancellation);
        Task tcpSendTask = tcpMemorySender.RunAsync(cancellation);

        Task first = await Task.WhenAny(tcpManageTask, udpReceiveTask, udpSendTask, tcpSendTask);

        tpcListener.Stop();

        foreach (var client in idToConnection_)
            client.Value.Cancellation.Cancel();

        try
        {
            await first;
        }
        finally
        {
            udp.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Terminate() => cancellationSource_.Cancel();

    /// <inheritdoc/>
    public int ReliableMessageHeader => TcpServerTransceiver.HeaderSize;
    
    /// <inheritdoc/>
    public void SendReliable(Memory<byte> message) => tcpMessages_.Post((message, null));
    
    /// <inheritdoc/>
    public void SendReliable(Memory<byte> message, int id) => tcpMessages_.Post((message, id));

    /// <inheritdoc/>
    public int UnreliableMessageHeader => UdpServerTransceiver.HeaderSize;
    
    /// <inheritdoc/>
    public int UnreliableMessageMaxLength => 1500;
    
    /// <inheritdoc/>
    public void SendUnreliable(Memory<byte> message) => udpMessages_.Post((message, null));
    
    /// <inheritdoc/>
    public void SendUnreliable(Memory<byte> message, int id) => udpMessages_.Post((message, id));

    /// <inheritdoc/>
    public void Kick(int id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            client.Cancellation.Cancel();
    }
}
