using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Core.Extensions;

namespace DefaultTransport.IpTransport;

static class Udp
{
    internal const int MaxDatagramSize = 0x10000;
}

struct UdpMultiTarget : IProtocol<(Memory<byte> payload, IPEndPoint target), (Memory<byte> payload, IPEndPoint target)>
{
    readonly Socket client_;
    readonly IPEndPoint ipPoint_ = new(IPAddress.Loopback, 0);

    public UdpMultiTarget(Socket client)
    {
        client_ = client;
    }

    public async ValueTask<(Memory<byte> payload, IPEndPoint target)> ReadAsync(CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.RentMemory(Udp.MaxDatagramSize);
        SocketReceiveFromResult result;

        IPEndPoint sender;

        while (true)
        {
            result = await client_.ReceiveFromAsync(buffer, ipPoint_, cancellation);
            
            if (result.RemoteEndPoint is not IPEndPoint valid)
                continue;

            sender = valid;
            break;
        } 

        return (buffer[..result.ReceivedBytes], sender);
    }

    public async ValueTask WriteAsync((Memory<byte> payload, IPEndPoint target) value, CancellationToken cancellation)
    {
        await client_.SendToAsync(value.payload, value.target, cancellation);
        value.payload.ReturnToArrayPool();
    }

    public Task CloseAsync()
    {
        client_.Dispose();
        return Task.CompletedTask;
    }
}

struct UdpSingleTarget : IProtocol<Memory<byte>, Memory<byte>>
{
    readonly Socket client_;
    readonly IPEndPoint target_;

    public UdpSingleTarget(Socket client, IPEndPoint target)
    {
        client_ = client;
        target_ = target;
    }

    public async ValueTask<Memory<byte>> ReadAsync(CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.RentMemory(Udp.MaxDatagramSize);

        while (true)
        {
            SocketReceiveFromResult result = await client_.ReceiveFromAsync(buffer, target_, cancellation);
            
            if (result.RemoteEndPoint.Equals(target_))
                return buffer[..result.ReceivedBytes];
        } 
    }

    public async ValueTask WriteAsync(Memory<byte> message, CancellationToken cancellation)
    {
        await client_.SendToAsync(message, target_, cancellation);
        message.ReturnToArrayPool();
    }

    public Task CloseAsync()
    {
        client_.Dispose();
        return Task.CompletedTask;
    }
}
