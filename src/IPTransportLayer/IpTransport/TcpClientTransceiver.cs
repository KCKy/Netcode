﻿using System.Buffers;
using System.Net.Sockets;
using Core.Extensions;
using Core.Utility;
using Serilog;

namespace DefaultTransport.IpTransport;

class TcpClientTransceiver : IProtocol<Memory<byte>, Memory<byte>>
{
    public TcpClientTransceiver(NetworkStream stream)
    {
        stream_ = stream;
    }

    readonly NetworkStream stream_;
    
    readonly Memory<byte> readLengthBuffer_ = new byte[sizeof(int)];

    readonly ILogger logger_ = Log.ForContext<TcpClientTransceiver>();

    async ValueTask ReadAsync(Memory<byte> buffer, CancellationToken cancellation)
    {
        try
        {
            await stream_.ReadExactlyAsync(buffer, cancellation);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            throw new OtherSideEndedException("Stream failed to read.");
        }
    }

    async ValueTask WriteAsync(Memory<byte> buffer, CancellationToken cancellation)
    {
        try
        {
            await stream_.WriteAsync(buffer, cancellation);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            throw new OtherSideEndedException("Stream failed to write.");
        }
    }

    public async ValueTask<Memory<byte>> ReceiveAsync(CancellationToken cancellation)
    {
        await ReadAsync(readLengthBuffer_, cancellation);
        int length = Bits.ReadInt(readLengthBuffer_.Span);

        logger_.Verbose("Received reliable message of length {Length}.", length);

        if (length <= 0)
            return Memory<byte>.Empty;
        
        var memory = ArrayPool<byte>.Shared.RentMemory(length);
        // We may "leak" some memory if we fail to read but GC will take care of it

        await ReadAsync(memory, cancellation);

        return memory;
    }

    readonly Memory<byte> writeLengthBuffer_ = new byte[sizeof(int)];
    
    public virtual async ValueTask SendAsync(Memory<byte> message, CancellationToken cancellation)
    {
        int length = message.Length;

        logger_.Verbose("Sending reliable message of length {Length}.", length);

        Bits.Write(length, writeLengthBuffer_.Span);

        await WriteAsync(writeLengthBuffer_, cancellation);
        await WriteAsync(message, cancellation);
        
        await stream_.FlushAsync(cancellation);
    }

    public async Task CloseAsync()
    {
        await stream_.DisposeAsync();
    }
}
