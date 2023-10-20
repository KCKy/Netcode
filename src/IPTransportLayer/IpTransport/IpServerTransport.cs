using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.HighPerformance;
using Core.Extensions;
using Core.Transport;
using Core.Utility;
using Serilog;
using Serilog.Core;

namespace DefaultTransport.IpTransport;

class ConnectedClient
{
    public readonly SafeTcpClientTransceiver Layer;
    public readonly QueueMessages<Memory<byte>> TcpMessages;
    public IPEndPoint UdpTarget;
    public readonly CancellationTokenSource Cancellation;
    public readonly long Id;

    public ConnectedClient(SafeTcpClientTransceiver layer, QueueMessages<Memory<byte>> messages, IPEndPoint target,
        CancellationTokenSource cancellation, long id)
    {
        Layer = layer;
        TcpMessages = messages;
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
        tcpBroadcast_ = new(new(idToConnection_), tcpBroadcastMessages_);
    }

    public event Action<long, Memory<byte>>? OnReliableMessage;
    public event Action<long, Memory<byte>>? OnUnreliableMessage;
    public event Action<long>? OnClientJoin;
    public event Action<long>? OnClientFinish;
    
    readonly ConcurrentDictionary<long, ConnectedClient> idToConnection_ = new();
    readonly BagMessages<(Memory<byte> payload, long? id)> udpMessages_ = new();
    readonly CancellationTokenSource cancellationSource_ = new();

    readonly QueueMessages<Memory<byte>> tcpBroadcastMessages_ = new();
    Sender<TcpMultiTransceiver, QueueMessages<Memory<byte>>, Memory<byte>> tcpBroadcast_;

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

    async Task RunClientAsync(TcpClient client, long id, CancellationToken cancellation)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint target || client.TryGetStream() is not { } stream)
        {
            client.Dispose();
            return;
        }

        SafeTcpClientTransceiver layer = new(stream);

        ConnectedClient record = new(layer, new(), target, new(), id);
        AddClient(record);

        await SendInitIdAsync(stream, id, cancellation);

        Receiver<SafeTcpClientTransceiver, Memory<byte>> receiver = new(layer);
        Sender<SafeTcpClientTransceiver, QueueMessages<Memory<byte>>, Memory<byte>> sender = new(layer, record.TcpMessages);
        
        long idCapture = id;
        receiver.OnMessage += m => OnReliableMessage?.Invoke(idCapture, m);

        OnClientJoin?.Invoke(id);

        Task receive = receiver.RunAsync(cancellation);
        Task send = sender.RunAsync(cancellation);

        Task first = await Task.WhenAny(receive, send);

        try
        {
            await first;
        }
        catch (OtherSideEndedException) { }
        finally
        {
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
            RunClientAsync(client, id, cancellation).AssureNoFault();
        }
    }

    public async Task RunAsync()
    {
        CancellationToken cancellation = cancellationSource_.Token;

        TcpListener tpcListener = new(local_);
        tpcListener.Start();

        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.Bind(local_);

        logger_.Debug("Began server at {Local}.", local_);

        Receiver<UdpServerTransceiver, (Memory<byte>, long)> udpReceiver = new(new(udp, idToConnection_));
        Sender<UdpServerTransceiver, BagMessages<(Memory<byte>, long?)>, (Memory<byte>, long?)> udpSender = new(new(udp, idToConnection_), udpMessages_);

        udpReceiver.OnMessage += HandleUdpReceive;
        
        Task tcpManageTask = ManageConnectionsAsync(tpcListener, cancellation);
        Task udpReceiveTask = udpReceiver.RunAsync(cancellation);
        Task udpSendTask = udpSender.RunAsync(cancellation);
        Task tcpBroadcastTask = tcpBroadcast_.RunAsync(cancellation);

        Task first = await Task.WhenAny(tcpManageTask, udpReceiveTask, udpSendTask, tcpBroadcastTask);

        tpcListener.Stop();
        await first;

        void HandleUdpReceive((Memory<byte> message, long id) value)
        {
            OnUnreliableMessage?.Invoke(value.id, value.message);
        }
    }
    
    public void Terminate() => cancellationSource_.Cancel();

    public void SendReliable(Memory<byte> message)
    {
        tcpBroadcastMessages_.Post(message);
    }

    public void SendReliable(Memory<byte> message, long id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            client.TcpMessages.Post(message);
    }

    public void SendUnreliable(Memory<byte> message)
    {
        udpMessages_.Post((message, null));
    }

    public void SendUnreliable(Memory<byte> message, long id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            udpMessages_.Post((message, client.Id));
    }

    public void Terminate(long id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            client.Cancellation.Cancel();
    }
}
