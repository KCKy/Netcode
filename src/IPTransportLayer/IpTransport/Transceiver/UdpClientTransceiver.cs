﻿using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Kcky.Useful;

namespace Kcky.GameNewt.Transport.Default;

static class Udp
{
    internal const int MaxDatagramSize = 0x10000;
}

/// <summary>
/// Implements sending and receiving messages over a UDP socket for the client.
/// Designed to work with <see cref="IPendingMessages{T}"/> and <see cref="Receiver{T}"/>.
/// </summary>
sealed class UdpClientTransceiver : IProtocol<Memory<byte>, Memory<byte>>
{
    readonly Socket client_;
    readonly IPEndPoint target_;
    readonly Memory<byte> id_;

    readonly ILogger logger_ = Log.ForContext<UdpClientTransceiver>();

    public UdpClientTransceiver(Socket client, IPEndPoint target, long id)
    {
        client_ = client;
        target_ = target;
        id_ = new byte[sizeof(long)];
        Bits.Write(id, id_.Span);
    }

    public const int HeaderSize = sizeof(long);

    public async ValueTask<Memory<byte>> ReceiveAsync(CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.RentMemory(Udp.MaxDatagramSize);

        while (true)
        {
            SocketReceiveFromResult result = await client_.ReceiveFromAsync(buffer, target_, cancellation);

            int length = result.ReceivedBytes;

            logger_.Verbose("Received unreliable message of length {Length}.", length);

            if (result.RemoteEndPoint.Equals(target_))
                return buffer[..length];

            logger_.Debug("The unreliable message did not come from server.");
        } 
    }

    public async ValueTask SendAsync(Memory<byte> message, CancellationToken cancellation)
    {
        id_.CopyTo(message); // Put authentication ID in the header
        await client_.SendToAsync(message, target_, cancellation);
        logger_.Verbose("Sent unreliable message to target {Target} with length {Length}.", target_, message.Length);
        ArrayPool<byte>.Shared.Return(message);
    }
}
