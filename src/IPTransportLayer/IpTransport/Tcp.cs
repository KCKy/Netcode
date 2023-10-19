using System.Buffers;
using System.Net.Sockets;
using Core.Extensions;
using Core.Utility;
using Serilog;

namespace DefaultTransport.IpTransport;

struct Tcp : IProtocol<Memory<byte>, Memory<byte>>
{
    public Tcp(TcpClient client)
    {
        client_ = client;

        try
        {
            stream_ = client.GetStream();
        }
        catch (InvalidOperationException)
        {
            client_.Dispose();
            throw;
        }
    }

    readonly TcpClient client_;
    readonly NetworkStream stream_;
    
    readonly Memory<byte> readLengthBuffer_ = new byte[sizeof(int)];

    readonly ILogger logger_ = Log.ForContext<Tcp>();

    public async ValueTask<Memory<byte>> ReadAsync(CancellationToken cancellation)
    {
        try
        {
            await stream_.ReadExactlyAsync(readLengthBuffer_, cancellation);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            throw new OtherSideEndedException("Stream failed to read next length.");
        }

        int length = Bits.ReadInt(readLengthBuffer_.Span);

        logger_.Verbose("Received message of length {Length}.", length);

        if (length <= 0)
            return Memory<byte>.Empty; // Header specified a message with no payload, skipping...

        var memory = ArrayPool<byte>.Shared.RentMemory(length);
        // We may "leak" some memory if we fail to read but GC will take care of it

        try
        {
            await stream_.ReadExactlyAsync(memory, cancellation);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            throw new OtherSideEndedException("Stream failed to read the payload.");
        }

        return memory;
    }

    readonly Memory<byte> writeLengthBuffer_ = new byte[sizeof(int)];
    
    public async ValueTask WriteAsync(Memory<byte> message, CancellationToken cancellation)
    {
        int length = message.Length;

        logger_.Verbose("Sending message of length {Length}.", length);

        Bits.Write(length, writeLengthBuffer_.Span);

        try
        {
            await stream_.WriteAsync(writeLengthBuffer_, cancellation);
            await stream_.WriteAsync(message, cancellation);
            message.ReturnToArrayPool();
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            throw new OtherSideEndedException();
        }
    
        await stream_.FlushAsync(cancellation);
    }

    public async Task CloseAsync()
    {
        await stream_.DisposeAsync();
        client_.Dispose();
    }
}
