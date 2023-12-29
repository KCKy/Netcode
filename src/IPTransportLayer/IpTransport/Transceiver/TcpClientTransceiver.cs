using System.Buffers;
using System.Net.Sockets;
using Serilog;
using Useful;

namespace DefaultTransport.IpTransport;

sealed class TcpClientTransceiver : IProtocol<Memory<byte>, Memory<byte>>
{
    public TcpClientTransceiver(NetworkStream stream)
    {
        stream_ = stream;
    }

    readonly NetworkStream stream_;
    
    readonly Memory<byte> readLengthBuffer_ = new byte[sizeof(int)];

    readonly ILogger Logger = Log.ForContext<TcpClientTransceiver>();

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
        await ReadAsync(readLengthBuffer_, cancellation);
        int length = Bits.ReadInt(readLengthBuffer_.Span);

        Logger.Verbose("Received reliable message of length {Length}.", length);

        if (length <= 0)
            return Memory<byte>.Empty;
        
        var memory = ArrayPool<byte>.Shared.RentMemory(length);
        // We may "leak" some memory if we fail to read but GC will take care of it

        await ReadAsync(memory, cancellation);

        return memory;
    }

    readonly Memory<byte> writeLengthBuffer_ = new byte[sizeof(int)];
    
    public async ValueTask SendAsync(Memory<byte> message, CancellationToken cancellation)
    {
        int length = message.Length;

        Logger.Verbose("Sending reliable message of length {Length}.", length);

        Bits.Write(length, writeLengthBuffer_.Span);

        await WriteAsync(writeLengthBuffer_, cancellation);
        await WriteAsync(message, cancellation);
        
        await stream_.FlushAsync(cancellation);
    }
}
