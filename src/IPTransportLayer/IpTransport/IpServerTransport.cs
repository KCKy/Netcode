using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Core.Transport;
using Serilog;
using Useful;

namespace DefaultTransport.IpTransport;

class ConnectedClient
{
    public readonly TcpClientTransceiver ClientTransceiver;
    public IPEndPoint UdpTarget;
    public readonly CancellationTokenSource Cancellation;
    public readonly long Id;

    public ConnectedClient(TcpClientTransceiver clientTransceiver, IPEndPoint target,
        CancellationTokenSource cancellation, long id)
    {
        ClientTransceiver = clientTransceiver;
        UdpTarget = target;
        Cancellation = cancellation;
        Id = id;
    }
}

public class IpServerTransport : IServerTransport
{
    readonly IPEndPoint local_;

    public IpServerTransport(IPEndPoint local)
    {
        local_ = local;
    }

    public event Action<long, Memory<byte>>? OnReliableMessage;
    public event Action<long, Memory<byte>>? OnUnreliableMessage;
    public event Action<long>? OnClientJoin;
    public event Action<long>? OnClientFinish;
    
    readonly ConcurrentDictionary<long, ConnectedClient> idToConnection_ = new();
    readonly BagMessages<(Memory<byte> payload, long? id)> udpMessages_ = new();
    readonly CancellationTokenSource cancellationSource_ = new();

    readonly QueueMessages<(Memory<byte>, long?)> tcpMessages_ = new();

    readonly ILogger logger_ = Log.ForContext<IpServerTransport>();
    
    void RemoveClient(long id)
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

    public int Port { get; private set; } = 0;

    async Task RunClientAsync(TcpClient client, long id)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint target || client.TryGetStream() is not { } stream)
        {
            client.Dispose();
            return;
        }

        TcpClientTransceiver layer = new(stream);

        ConnectedClient record = new(layer, target, new(), id);
        AddClient(record);

        CancellationToken clientCancellation = record.Cancellation.Token;

        await SendInitIdAsync(stream, id, clientCancellation);

        Receiver<Memory<byte>> receiver = new(layer);
        
        long idCapture = id;
        receiver.OnMessage += m => OnReliableMessage?.Invoke(idCapture, m);

        OnClientJoin?.Invoke(id);

        try
        {
            await receiver.RunAsync(clientCancellation);
        }
        catch (OtherSideEndedException)
        {
            logger_.Verbose("Client with id {Id} disconnected from server.", id);
        }
        finally
        {
            logger_.Verbose("Client with id {Id} was removed.", id);
            RemoveClient(id);
            client.Dispose();
        }
    }

    ValueTask SendInitIdAsync(NetworkStream stream, long id, CancellationToken cancellation)
    {
        Memory<byte> idRaw = new byte[sizeof(long)];
        Bits.Write(id, idRaw.Span);
        return stream.WriteAsync(idRaw, cancellation);
    }

    async Task ManageConnectionsAsync(TcpListener listener, CancellationToken cancellation)
    {
        for (long id = 1; ; id++)
        {
            TcpClient client = await listener.AcceptTcpClientAsync(cancellation);
            RunClientAsync(client, id).AssureNoFault();
            logger_.Verbose("Client connected to server with id {Id}.", id);
        }
    }

    public async Task RunAsync()
    {
        CancellationToken cancellation = cancellationSource_.Token;

        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.Bind(local_);

        IPEndPoint endpoint = udp.LocalEndPoint as IPEndPoint ?? throw new InvalidOperationException();

        TcpListener tpcListener = new(endpoint);
        tpcListener.Start();

        Port = endpoint.Port;

        logger_.Debug("Began server at {Local} -> {Transformed}.", local_, endpoint);
        
        UdpServerTransceiver udpTransceiver = new(udp, idToConnection_);
        TcpServerTransceiver tcpServerTransceiver = new(idToConnection_);

        Receiver<(Memory<byte>, long)> udpReceiver = new(udpTransceiver);
        Sender<BagMessages<(Memory<byte>, long?)>, (Memory<byte>, long?)> udpMemorySender = new(udpTransceiver, udpMessages_);
        Sender<QueueMessages<(Memory<byte>, long?)>, (Memory<byte>, long?)> tcpMemorySender = new(tcpServerTransceiver,  tcpMessages_);

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
        
        void HandleUdpReceive((Memory<byte> message, long id) value)
        {
            OnUnreliableMessage?.Invoke(value.id, value.message);
        }
    }
    
    public void Terminate() => cancellationSource_.Cancel();

    public int ReliableMessageHeader => TcpServerTransceiver.HeaderSize;
    public void SendReliable(Memory<byte> message) => tcpMessages_.Post((message, null));
    public void SendReliable(Memory<byte> message, long id) => tcpMessages_.Post((message, id));

    public int UnreliableMessageHeader => UdpServerTransceiver.HeaderSize;
    public int UnreliableMessageMaxLength => 1500;
    public void SendUnreliable(Memory<byte> message) => udpMessages_.Post((message, null));
    public void SendUnreliable(Memory<byte> message, long id) => udpMessages_.Post((message, id));

    public void Terminate(long id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            client.Cancellation.Cancel();
    }
}
