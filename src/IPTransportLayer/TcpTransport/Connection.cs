using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using DefaultTransport.TcpTransport;
using MemoryPack;
using Serilog;

namespace DefaultTransport;

// TODO: resolve memory model issues

internal enum ConnectionFinishReason
{
    Unknown = 0,
    Terminated,
    OtherSideEnded,
    Faulted,
    Corrupted
}

internal class Connection<TIn, TOut>
where TIn : class
{
    public Connection(TcpClient client, NetworkStream stream, Action<TIn> messageCallback)
    {
        client_ = client;
        stream_ = stream;
        Finish = finishSource_.Task;
        messageCallback_ = messageCallback;
    }

    readonly TcpClient client_;
    readonly NetworkStream stream_;
    
    readonly TaskCompletionSource<ConnectionFinishReason> finishSource_ = new();

    readonly ILogger logger_ = Log.ForContext<Connection<TIn, TOut>>();

    readonly SemaphoreSlim outboxSize_ = new(0);
    readonly ConcurrentBag<TOut> outbox_ = new();

    public readonly Task<ConnectionFinishReason> Finish;

    readonly byte[] readLengthBuffer_ = new byte[sizeof(int)];

    async Task<TIn?> TryReadAsync()
    {
        try
        {
            await stream_.ReadExactlyAsync(readLengthBuffer_, 0, sizeof(int));
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            TryFinish(ConnectionFinishReason.OtherSideEnded);
            return null;
        }
        
        var length = BitConverter.ToInt32(readLengthBuffer_, 0);

        if (length <= 0)
            return null;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            await stream_.ReadExactlyAsync(buffer, 0, length);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            TryFinish(ConnectionFinishReason.OtherSideEnded);
            return null;
        }
        
        return MemoryPackSerializer.Deserialize<TIn>(buffer);
    }

    async Task ReadAsync()
    {
        while (true)
        {
            // TODO: save memory

            var read = TryReadAsync();

            await Task.WhenAny(read, Finish);

            if (Finish.IsCompleted)
                return;

            if (read.Result is not { } message)
            {
                if (read.IsCompletedSuccessfully)
                    logger_.Fatal("The input stream has been corrupted.");
                
                TryFinish(ConnectionFinishReason.Corrupted);
                return;
            }

            messageCallback_(message);
        }
    }

    async Task WriteAsync()
    {
        while (true)
        {
            Task wait = outboxSize_.WaitAsync();

            await Task.WhenAny(wait, Finish);

            if (Finish.IsCompleted)
                return;

            if (outbox_.TryTake(out TOut? message))
            {
                // TODO: improve
                byte[] payload = MemoryPackSerializer.Serialize(message);
                byte[] header = BitConverter.GetBytes(payload.Length);

                try
                {
                    await stream_.WriteAsync(header);
                    await stream_.WriteAsync(payload);
                }
                catch (Exception ex) when (ex is EndOfStreamException or IOException)
                {
                    TryFinish(ConnectionFinishReason.OtherSideEnded);
                    return;
                }
            }
    
            await stream_.FlushAsync();
        }
    }

    public async Task Run()
    {
        Task read = ReadAsync();
        Task write = WriteAsync();

        await Task.WhenAny(read, write);

        if (read.Exception is { InnerException: { } ex1 } )
            logger_.Error(ex1, "Read faulted.");

        if (read.Exception is { InnerException: { } ex2 } )
            logger_.Error(ex2, "Write faulted.");

        TryFinish(ConnectionFinishReason.Faulted);

        await Task.WhenAll(read, write);

        await stream_.DisposeAsync();
        client_.Dispose();
    }

    public void Send(TOut message)
    {
        outbox_.Add(message);
        outboxSize_.Release();
    }

    readonly Action<TIn> messageCallback_;

    void TryFinish(ConnectionFinishReason reason) => finishSource_.TrySetResult(reason);

    public void TryFinish() => TryFinish(ConnectionFinishReason.Terminated);
}
