using System.Buffers;
using System.Collections.Concurrent;
using Serilog;
using Useful;

namespace DefaultTransport.IpTransport;

sealed class TcpServerTransceiver : ISendProtocol<(Memory<byte> memory, long? id)>
{
    readonly ConcurrentDictionary<long, ConnectedClient> idToConnection_;

    readonly ILogger Logger = Log.ForContext<TcpServerTransceiver>();

    public TcpServerTransceiver(ConcurrentDictionary<long, ConnectedClient> clients)
    {
        idToConnection_ = clients;
    }

    public const int HeaderSize = 0;

    async ValueTask SafeSendAsync(ConnectedClient client, Memory<byte> message, CancellationToken cancellation)
    {
        try
        {
            await client.ClientTransceiver.SendAsync(message, cancellation);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Client {Id} failed to send.", client.Id);
            client.Cancellation.Cancel();
        }
    }

    public async ValueTask SendAsync((Memory<byte> memory, long? id) value, CancellationToken cancellation)
    {
        (var message, long? id) = value;

        if (id is not { } clientId)
        {
            foreach ((_, ConnectedClient client) in idToConnection_)
                await SafeSendAsync(client, message, cancellation);
        }
        else
        {
            if (idToConnection_.TryGetValue(clientId, out ConnectedClient? client))
                await SafeSendAsync(client, message, cancellation);
        }

        ArrayPool<byte>.Shared.Return(message);
    }
}
