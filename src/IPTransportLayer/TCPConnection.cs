using Core.Transport;
using System.Collections.Concurrent;
using System.Net.Sockets;
using MemoryPack;
using Serilog;

namespace DefaultTransport;

internal class Connection<TIn, TOut>
{
    public Connection(TcpClient client, NetworkStream stream)
    {
        client_ = client;
        stream_ = stream;
        finish_ = finishSource_.Task;
    }

    readonly TcpClient client_;
    readonly NetworkStream stream_;
    readonly Task finish_;
    readonly TaskCompletionSource finishSource_ = new();

    readonly ILogger logger_ = Log.ForContext<Connection<TIn, TOut>>();

    volatile ClientFinishReason finishReason_ = ClientFinishReason.Unknown;
    public ClientFinishReason FinishReason => finishReason_;
    readonly SemaphoreSlim outboxSize_ = new(0);
    readonly ConcurrentBag<TOut> outbox_ = new();

    async Task ReadAsync(Task finished)
    {
        while (true)
        {
            var read = MemoryPackSerializer.DeserializeAsync<TIn>(stream_!).AsTask();

            await Task.WhenAny(read, finished);

            if (finished.IsCompleted)
                return;

            if (read.Result is not { } message)
            {
                logger_.Fatal("The input stream has been corrupted.");
                TryFinish(ClientFinishReason.Corruption);
                return;
            }

            OnMessage?.Invoke(message);
        }
    }

    async Task WriteAsync(Task finished)
    {
        while (true)
        {
            Task wait = outboxSize_.WaitAsync();

            await Task.WhenAny(wait, finished);

            if (finished.IsCompleted)
                return;

            if (outbox_.TryTake(out TOut? message))
                await MemoryPackSerializer.SerializeAsync(stream_!, message);
        }
    }

    public async Task Run()
    {
        if (finish_.IsCompleted)
        {
            Task read = ReadAsync(finish_);
            Task write = WriteAsync(finish_);

            await Task.WhenAny(read, write);

            if (read.Exception is { InnerException: { } ex1 } )
                logger_.Error(ex1, "Read faulted.");

            if (read.Exception is { InnerException: { } ex2 } )
                logger_.Error(ex2, "Write faulted.");

            TryFinish(ClientFinishReason.NetworkError);

            await Task.WhenAll(read, write);
        }

        await stream_.DisposeAsync();
        client_.Dispose();
    }


    public event Action<TIn>? OnMessage;

    void TryFinish(ClientFinishReason reason)
    {
        if (finishSource_.TrySetResult())
            finishReason_ = reason;
    }
}