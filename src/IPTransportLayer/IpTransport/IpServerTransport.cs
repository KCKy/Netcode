using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using Core.Extensions;
using Core.Transport;

namespace DefaultTransport.IpTransport;

public enum ServerFinishReason
{
    Unknown = 0,
    Terminated,
    Fault,
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

    record ConnectedClient(QueueMessages<Memory<byte>> TcpMessages, IPEndPoint UdpTarget, CancellationTokenSource Cancellation, long Id, IPEndPoint Point);

    readonly ConcurrentDictionary<IPEndPoint, ConnectedClient> pointToConnection_ = new();
    readonly ConcurrentDictionary<long, ConnectedClient> idToConnection_ = new();
    readonly BagMessages<(Memory<byte> payload, IPEndPoint target)> udpMessages_ = new();

    readonly CancellationTokenSource cancellationSource_ = new();

    void RemoveClient(long id)
    {
        if (!idToConnection_.TryRemove(id, out ConnectedClient? client))
            return;

        pointToConnection_.TryRemove(client.Point, out _);
    }

    void AddClient(long id, ConnectedClient client)
    {
        if (!idToConnection_.TryAdd(id, client) || !pointToConnection_.TryAdd(client.Point, client))
            throw new InvalidOperationException("Client is already present.");
    }

    async Task RunClientAsync(Transceiver<Tcp, QueueMessages<Memory<byte>>, Memory<byte>, Memory<byte>> tcp, long id, CancellationToken cancellation)
    {
        try
        {
            await tcp.Run(cancellation);
        }
        catch (OtherSideEndedException) { }
        finally
        {
            OnClientFinish?.Invoke(id);
            RemoveClient(id);
        }
    }

    async Task ManageConnectionsAsync(TcpListener listener, CancellationToken cancellation)
    {
        long id = 0;

        while (true)
        {
            id++;
            
            TcpClient client = await listener.AcceptTcpClientAsync(cancellation);

            if (client.Client.RemoteEndPoint is not IPEndPoint target)
            {
                client.Dispose();
                continue;
            }

            ConnectedClient record = new(new(), target, new(), id, target);
            AddClient(id, record);

            Transceiver<Tcp, QueueMessages<Memory<byte>>, Memory<byte>, Memory<byte>> tcp = new(new(client), record.TcpMessages);

            long idCapture = id;
            tcp.OnMessage += m => OnReliableMessage?.Invoke(idCapture, m);

            RunClientAsync(tcp, id, cancellation).AssureNoFault();

            OnClientJoin?.Invoke(id);
        }
    }

    Transceiver<UdpMultiTarget, BagMessages<(Memory<byte>, IPEndPoint)>, (Memory<byte>, IPEndPoint), (Memory<byte>, IPEndPoint)> ConstructUdpTransceiver()
    {
        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.Bind(local_);

        Transceiver<UdpMultiTarget, BagMessages<(Memory<byte>, IPEndPoint)>, (Memory<byte>, IPEndPoint), (Memory<byte>, IPEndPoint)>
            transceiver = new(new(udp), udpMessages_);

        transceiver.OnMessage += HandleUnreliableMessage;

        return transceiver;
    }

    public async Task RunAsync()
    {
        CancellationToken cancellation = cancellationSource_.Token;

        TcpListener tpcListener = new(local_);
        tpcListener.Start();

        var udp = ConstructUdpTransceiver();
        
        Task tcpTask = ManageConnectionsAsync(tpcListener, cancellation);
        Task udpTask = udp.Run(cancellation);

        Task first = await Task.WhenAny(tcpTask, udpTask);

        tpcListener.Stop();
        await first;
    }
    
    void HandleUnreliableMessage((Memory<byte> message, IPEndPoint point) value)
    {
        if (pointToConnection_.TryGetValue(value.point, out ConnectedClient? client))
            OnUnreliableMessage?.Invoke(client.Id, value.message);
    }

    public void Terminate() => cancellationSource_.Cancel();

    public void SendReliable(Memory<byte> message)
    {
        foreach ((long _, ConnectedClient client) in idToConnection_)
            client.TcpMessages.Post(message);
    }

    public void SendReliable(Memory<byte> message, long id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            client.TcpMessages.Post(message);
    }

    public void SendUnreliable(Memory<byte> message)
    {
        foreach ((IPEndPoint point, _) in pointToConnection_)
            udpMessages_.Post((message, point));
    }

    public void SendUnreliable(Memory<byte> message, long id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            udpMessages_.Post((message, client.Point));
    }

    public void Terminate(long id)
    {
        if (idToConnection_.TryGetValue(id, out ConnectedClient? client))
            client.Cancellation.Cancel();
    }
}
