using Core.Extensions;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace DefaultTransport.IpTransport;

class SafeTcpClientTransceiver : TcpClientTransceiver
{
    readonly SemaphoreSlim mutex_ = new(1, 1);

    public SafeTcpClientTransceiver(NetworkStream stream) : base(stream) { }

    public override async ValueTask SendAsync(Memory<byte> message, CancellationToken cancellation)
    {
        await mutex_.WaitAsync(cancellation);
        
        try
        {
            await base.SendAsync(message, cancellation);
        }
        finally
        {
            mutex_.Release();
        }
    }
}

class TcpMultiTransceiver : ISendProtocol<Memory<byte>>
{
    readonly ConcurrentDictionary<long, ConnectedClient> idToConnection_;

    public TcpMultiTransceiver(ConcurrentDictionary<long, ConnectedClient> clients)
    {
        idToConnection_ = clients;
    }

    public async ValueTask SendAsync(Memory<byte> data, CancellationToken cancellation)
    {
        foreach ((_, ConnectedClient client) in idToConnection_)
            await client.Layer.SendAsync(data, cancellation);
        data.ReturnToArrayPool();
    }
}
