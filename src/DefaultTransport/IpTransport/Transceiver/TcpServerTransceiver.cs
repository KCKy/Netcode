﻿using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Kcky.Useful;

namespace Kcky.GameNewt.Transport.Default;

/// <summary>
/// Extends usability of <see cref="TcpClientTransceiver"/> to the server.
/// Designed to work with <see cref="IPendingMessages{T}"/> and <see cref="Receiver{T}"/>.
/// </summary>
sealed class TcpServerTransceiver : ISendProtocol<(Memory<byte> memory, int? id)>
{
    readonly ConcurrentDictionary<int, ConnectedClient> idToConnection_;

    readonly ILogger logger_;

    public TcpServerTransceiver(ConcurrentDictionary<int, ConnectedClient> clients, ILoggerFactory loggerFactory)
    {
        idToConnection_ = clients;
        logger_ = loggerFactory.CreateLogger<TcpServerTransceiver>();
    }

    public const int HeaderSize = 0;

    async ValueTask SafeSendAsync(ConnectedClient client, ReadOnlyMemory<byte> message, CancellationToken cancellation)
    {
        try
        {
            await client.ClientTransceiver.SendAsync(message, cancellation); // The memory is borrowed to be sent to the specific client.
        }
        catch (Exception ex)
        {
            logger_.LogError(ex, "Client {Id} failed to send.", client.Id);
            client.Cancellation.Cancel();
        }
    }

    public async ValueTask SendAsync((Memory<byte> memory, int? id) value, CancellationToken cancellation)
    {
        (var message, int? id) = value;

        if (id is not { } clientId)
        {
            // Broadcast
            foreach ((_, ConnectedClient client) in idToConnection_)
                await SafeSendAsync(client, message, cancellation);
        }
        else
        {
            // Unicast
            if (idToConnection_.TryGetValue(clientId, out ConnectedClient? client))
                await SafeSendAsync(client, message, cancellation);
        }

        ArrayPool<byte>.Shared.Return(message);
    }
}
