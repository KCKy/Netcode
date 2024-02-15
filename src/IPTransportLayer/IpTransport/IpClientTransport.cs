using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Core.Transport;
using Serilog;
using Useful;

namespace DefaultTransport.IpTransport;

/// <summary>
/// Implementation of the client transport over TCP/UDP. The client for <see cref="IpServerTransport"/>.
/// </summary>
/// <remarks>
/// The transport is unencrypted and prone to spoofing attacks.
/// The transport creates a TCP and UDP socket (not some available ports) and tries to establish the connection.
/// </remarks>
public sealed class IpClientTransport : IClientTransport
{
    readonly IPEndPoint target_;
    readonly ILogger logger_ = Log.ForContext<IpClientTransport>();

    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeoutMs { get; init; } = 2000;

    readonly QueueMessages<Memory<byte>> tcpMessages_ = new();
    readonly BagMessages<Memory<byte>> udpMessages_ = new();

    readonly CancellationTokenSource cancellationSource_ = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="target">The target address to connect to.</param>
    public IpClientTransport(IPEndPoint target)
    {
        target_ = target;
    }

    /// <inheritdoc/>
    public void Terminate() => cancellationSource_.Cancel();

    int hasStarted_ = 0;

    async ValueTask<(TcpClient, Socket, TcpClientTransceiver, UdpClientTransceiver)> ConnectAsync(CancellationToken cancellation)
    {
        TcpClient tcp = new(AddressFamily.InterNetwork);
        Task connectTask = tcp.ConnectAsync(target_, cancellation).AsTask();

        logger_.Information("Client trying to connect to {Target}.", target_);

        await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs, cancellation));

        cancellation.ThrowIfCancellationRequested();

        if (!connectTask.IsCompleted)
            throw new TimedOutException("TCP failed to connect in time.");

        await connectTask;

        Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPEndPoint local = tcp.Client.LocalEndPoint as IPEndPoint ??
                           throw new InvalidOperationException("Local end point does not exist.");
        
        IPEndPoint anyPoint = new(IPAddress.Any, 0);
        udp.Bind(anyPoint);

        logger_.Information("Began tcp at {local} and udp at {UdpLocal}.", local, udp.LocalEndPoint);

        NetworkStream stream = tcp.GetStream();

        // We expect the server to send us our transport ID.
        // This is going to be used in UDP messages to identify us.
        // This identification method is prone to spoofing, the attacker just needs to get a hold of the ID,
        // then they may send any unreliable messages to the server and even steal the unreliable traffic.
        // TODO: to remedy this packet signing should be employed (e.g. some secret hashing function of the packet id or so)
        // but for purposes of demonstration this method works.

        Memory<byte> idRaw = new byte[sizeof(long)];
        await stream.ReadExactlyAsync(idRaw, cancellation);
        long id = Bits.ReadLong(idRaw.Span);

        return (tcp, udp, new(stream), new(udp, target_, id));
    }

    /// <summary>
    /// Start the connection.
    /// </summary>
    /// <remarks>
    /// This method is not thread safe and is expected to be run once.
    /// The task will return cancelled, if the connection was terminated client side, successfully, if the connection was ended by the other side,
    /// with a <see cref="TimeoutException"/> if the connecting client timed out, or a different exception if the underlying network socket fails.
    /// </remarks>
    /// <exception cref="InvalidOperationException">If the client has already started once.</exception>
    /// <returns>Task representing the connection lifetime.</returns>
    public async Task RunAsync()
    {
        if (Interlocked.CompareExchange(ref hasStarted_, 1, 0) != 0)
            throw new InvalidOperationException("The client has already started.");

        CancellationToken cancellation = cancellationSource_.Token;

        (TcpClient tcpClient, Socket udpClient, TcpClientTransceiver tcp, UdpClientTransceiver udp) = await ConnectAsync(cancellation);
        
        Sender<QueueMessages<Memory<byte>>, Memory<byte>> tcpMemorySender = new(tcp,  tcpMessages_);
        Receiver<Memory<byte>> tcpReceiver = new(tcp);

        Sender<BagMessages<Memory<byte>>, Memory<byte>> udpMemorySender = new(udp, udpMessages_);
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
            udpClient.Dispose();
        }
    }

    /// <inheritdoc/>
    public int UnreliableMessageMaxLength => 1500;

    /// <inheritdoc/>
    public int ReliableMessageHeader => TcpClientTransceiver.HeaderSize;

    /// <inheritdoc/>
    public void SendReliable(Memory<byte> message) => tcpMessages_.Post(message);

    /// <inheritdoc/>
    public int UnreliableMessageHeader => UdpClientTransceiver.HeaderSize;
    
    /// <inheritdoc/>
    public void SendUnreliable(Memory<byte> message) => udpMessages_.Post(message);

    void InvokeReliableMessage(Memory<byte> message) => OnReliableMessage?.Invoke(message);
    
    /// <inheritdoc/>
    public event ClientMessageEvent? OnReliableMessage;

    void InvokeUnreliableMessage(Memory<byte> message) => OnUnreliableMessage?.Invoke(message);
    
    /// <inheritdoc/>
    public event ClientMessageEvent? OnUnreliableMessage;
}
