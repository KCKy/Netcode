using System.Net;
using System.Net.Sockets;
using Core.Transport;

namespace DefaultTransport.IpTransport;

// TODO: add more logging

public sealed class IpClientTransport : IClientTransport
{
    readonly IPEndPoint target_;

    public int ConnectTimeoutMs { get; init; } = 500;

    readonly QueueMessages<Memory<byte>> tcpMessages_ = new();
    readonly BagMessages<Memory<byte>> udpMessages_ = new();

    readonly CancellationTokenSource cancellationSource_ = new();

    public IpClientTransport(IPEndPoint target)
    {
        target_ = target;
    }

    public void Cancel() => cancellationSource_.Cancel();

    (Transceiver<Tcp, QueueMessages<Memory<byte>>, Memory<byte>, Memory<byte>> tcpTransceiver, 
    Transceiver<UdpSingleTarget, BagMessages<Memory<byte>>, Memory<byte>, Memory<byte>> udpTransceiver)
    ConstructTransceivers(TcpClient tcpClient, Socket udpSocket, IPEndPoint localTarget)
    {
        Transceiver<Tcp, QueueMessages<Memory<byte>>, Memory<byte>, Memory<byte>> tcpTransceiver = new(new(tcpClient), tcpMessages_);
        Transceiver<UdpSingleTarget, BagMessages<Memory<byte>>, Memory<byte>, Memory<byte>> udpTransceiver = new(new(udpSocket, localTarget), udpMessages_);
        
        tcpTransceiver.OnMessage += InvokeReliableMessage;
        udpTransceiver.OnMessage += InvokeUnreliableMessage;

        return (tcpTransceiver, udpTransceiver);
    }

    Socket CreateUdpSocket()
    {
        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint udpPoint = new(IPAddress.Any, target_.Port);
        udp.Bind(udpPoint);
        return udp;
    }

    async ValueTask<(Transceiver<Tcp, QueueMessages<Memory<byte>>, Memory<byte>, Memory<byte>> tcpTransceiver, 
                     Transceiver<UdpSingleTarget, BagMessages<Memory<byte>>, Memory<byte>, Memory<byte>> udpTransceiver)>
        ConnectAsync(CancellationToken cancellation)
    {
        TcpClient tcp = new();
        Task connectTask = tcp.ConnectAsync(target_, cancellation).AsTask();

        Socket udp = CreateUdpSocket();
        
        await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs, cancellation));

        cancellation.ThrowIfCancellationRequested();

        if (!connectTask.IsCompleted)
            throw new TimedOutException("TCP failed to connect in time.");

        return ConstructTransceivers(tcp, udp, target_);
    }

    public async Task RunAsync()
    {
        CancellationToken cancellation = cancellationSource_.Token;

        var (tcp, udp) = await ConnectAsync(cancellation);

        Task tcpTask = tcp.Run(cancellation);
        Task udpTask = udp.Run(cancellation);

        Task first = await Task.WhenAny(tcpTask, udpTask);

        cancellationSource_.Cancel();

        try
        {
            await first;
        }
        catch (OtherSideEndedException) { }
    }

    public int UnreliableMessageMaxLength => 1500;

    public void Terminate() => cancellationSource_.Cancel();

    public void SendReliable(Memory<byte> message) => tcpMessages_.Post(message);
    public void SendUnreliable(Memory<byte> message) => udpMessages_.Post(message);

    void InvokeReliableMessage(Memory<byte> message) => OnReliableMessage?.Invoke(message);
    public event Action<Memory<byte>>? OnReliableMessage;

    void InvokeUnreliableMessage(Memory<byte> message) => OnUnreliableMessage?.Invoke(message);
    public event Action<Memory<byte>>? OnUnreliableMessage;
}
