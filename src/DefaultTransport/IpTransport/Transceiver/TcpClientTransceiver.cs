﻿using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Kcky.Useful;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kcky.GameNewt.Transport.Default;

/// <summary>
/// Implements sending and receiving messages over a TCP network stream.
/// Designed to work with <see cref="IPendingMessages{T}"/> and <see cref="Receiver{T}"/>.
/// </summary>
sealed class TcpClientTransceiver : IProtocol<Memory<byte>, Memory<byte>>
{
    public TcpClientTransceiver(NetworkStream stream, ILoggerFactory loggerFactory)
    {
        stream_ = stream;
        logger_ = loggerFactory.CreateLogger<TcpClientTransceiver>();
    }

    readonly NetworkStream stream_;
    
    readonly Memory<byte> readLengthBuffer_ = new byte[sizeof(int)];

    readonly ILogger logger_;

    public const int HeaderSize = 0;

    async ValueTask ReadAsync(Memory<byte> buffer, CancellationToken cancellation)
    {
        try
        {
            await stream_.ReadExactlyAsync(buffer, cancellation);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            throw new OtherSideEndedException("Stream failed to read.", ex);
        }
    }

    async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation)
    {
        try
        {
            await stream_.WriteAsync(buffer, cancellation);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            throw new OtherSideEndedException("Stream failed to write.", ex);
        }
    }

    public async ValueTask<Memory<byte>> ReceiveAsync(CancellationToken cancellation)
    {
        await ReadAsync(readLengthBuffer_, cancellation); // Read the length of the message
        int length = Bits.ReadInt(readLengthBuffer_.Span);

        logger_.LogTrace("Received reliable message of length {Length}.", length);

        /*
         * Message format:
         * [ Message Length: int ] [ Message ]
         */

        if (length <= 0)
            return Memory<byte>.Empty;
        
        var memory = ArrayPool<byte>.Shared.RentMemory(length); // Read the message
        // We may "leak" some memory if we fail to read but GC will take care of it

        await ReadAsync(memory, cancellation);

        return memory;
    }

    readonly Memory<byte> writeLengthBuffer_ = new byte[sizeof(int)];
    
    public async ValueTask SendAsync(Memory<byte> message, CancellationToken cancellation)
    {
        await SendAsync((ReadOnlyMemory<byte>)message, cancellation);
        ArrayPool<byte>.Shared.Return(message);
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellation)
    {
        int length = message.Length;

        logger_.LogTrace("Sending reliable message of length {Length}.", length);

        Bits.Write(length, writeLengthBuffer_.Span);

        /*
         * Message format:
         * Part 1: [ Message Length: int ]
         * Part 2: [ Message ]
         */

        await WriteAsync(writeLengthBuffer_, cancellation);
        await WriteAsync(message, cancellation);
        
        await stream_.FlushAsync(cancellation);
    }
}
