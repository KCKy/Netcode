using System.Net;
using System.Net.Sockets;
using Core.Transport;
using Serilog;
using Useful;

namespace DefaultTransport.IpTransport;

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

    public void Terminate() => cancellationSource_.Cancel();

    async ValueTask<(TcpClient, TcpClientTransceiver, UdpClientTransceiver)> ConnectAsync(CancellationToken cancellation)
    {
        TcpClient tcp = new(AddressFamily.InterNetwork);
        Task connectTask = tcp.ConnectAsync(target_, cancellation).AsTask();

        await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs, cancellation));

        cancellation.ThrowIfCancellationRequested();

        if (!connectTask.IsCompleted)
            throw new TimedOutException("TCP failed to connect in time.");

        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPEndPoint local = tcp.Client.LocalEndPoint as IPEndPoint ??
                           throw new InvalidOperationException("Local end point does not exist.");
        IPEndPoint udpPoint = new(IPAddress.Any, local.Port);

        udp.Bind(udpPoint);

        logger_.Debug("Began tcp at {local} and udp at {UdpLocal}.", local, udpPoint);

        NetworkStream stream = tcp.GetStream();

        Memory<byte> idRaw = new byte[sizeof(long)];
        await stream.ReadExactlyAsync(idRaw, cancellation);
        long id = Bits.ReadLong(idRaw.Span);

        return (tcp, new(stream), new(udp, target_, id));
    }

    public async Task RunAsync()
    {
        CancellationToken cancellation = cancellationSource_.Token;

        (TcpClient tcpClient, TcpClientTransceiver tcp, UdpClientTransceiver udp) = await ConnectAsync(cancellation);
        
        MemorySender<QueueMessages<Memory<byte>>> tcpMemorySender = new(tcp,  tcpMessages_);
        Receiver<Memory<byte>> tcpReceiver = new(tcp);

        MemorySender<BagMessages<Memory<byte>>> udpMemorySender = new(udp, udpMessages_);
        Receiver<Memory<byte>> udpReceiver = new(udp);

        tcpReceiver.OnMessage += InvokeReliableMessage;
        udpReceiver.OnMessage += InvokeUnreliableMessage;

        Task tcpSendTask = tcpMemorySender.RunAsync(cancellation);
        Task tcpReceiveTask = tcpReceiver.RunAsync(cancellation);
        Task udpSendTask = udpReceiver.RunAsync(cancellation);
        Task udpReceiveTask = udpMemorySender.RunAsync(cancellation);

        Task first = await Task.WhenAny(tcpSendTask, tcpReceiveTask, udpSendTask, udpReceiveTask);

        cancellationSource_.Cancel();

        try
        {
            await first;
        }
        catch (OtherSideEndedException) { }
        finally
        {
            tcpClient.Dispose();
        }
    }

    public int UnreliableMessageMaxLength => 1500;

    public int ReliableMessageHeader => TcpClientTransceiver.HeaderSize;
    public void SendReliable(Memory<byte> message) => tcpMessages_.Post(message);
    public int UnreliableMessageHeader => UdpClientTransceiver.HeaderSize;
    public void SendUnreliable(Memory<byte> message) => udpMessages_.Post(message);

    void InvokeReliableMessage(Memory<byte> message) => OnReliableMessage?.Invoke(message);
    public event Action<Memory<byte>>? OnReliableMessage;

    void InvokeUnreliableMessage(Memory<byte> message) => OnUnreliableMessage?.Invoke(message);
    public event Action<Memory<byte>>? OnUnreliableMessage;
}
