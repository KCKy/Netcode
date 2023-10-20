using System.Drawing;
using System.Net;
using System.Net.Sockets;
using Core.Transport;
using Serilog;
using Serilog.Core;

namespace DefaultTransport.IpTransport;

// TODO: add more logging

public sealed class IpClientTransport : IClientTransport
{
    readonly IPEndPoint target_;
    readonly ILogger logger_ = Log.ForContext<IpClientTransport>();

    public int ConnectTimeoutMs { get; init; } = 2000;

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

    async ValueTask<(Transceiver<Tcp, QueueMessages<Memory<byte>>, Memory<byte>, Memory<byte>> tcpTransceiver, 
                     Transceiver<UdpSingleTarget, BagMessages<Memory<byte>>, Memory<byte>, Memory<byte>> udpTransceiver)>
        ConnectAsync(CancellationToken cancellation)
    {
        TcpClient tcp = new();
        Task connectTask = tcp.ConnectAsync(target_, cancellation).AsTask();

        await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs, cancellation));

        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPEndPoint local = tcp.Client.LocalEndPoint as IPEndPoint ??
                           throw new InvalidOperationException("Local end point does not exist.");
        IPEndPoint udpPoint = new(IPAddress.Any, local.Port);

        logger_.Debug("Starting UDP socket over endpoint {Endpoint}", udpPoint);

        udp.Bind(udpPoint);

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
