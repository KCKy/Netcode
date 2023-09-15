using System.Collections.Concurrent;
using System.Net;
using Core.Transport;
using Core.Transport.Client;
using System.Net.Sockets;
using MemoryPack;
using Serilog;

namespace DefaultTransport.TcpTransport;

// TODO: add more logging

public sealed class TcpClientTransport<TIn, TOut> : IClientTransport<TIn, TOut>
{
    readonly TcpClient client_ = new();
    NetworkStream? stream_;

    readonly TaskCompletionSource finished_ = new();
    int running_ = 0;
    volatile ClientFinishReason finishReason_ = ClientFinishReason.Unknown;

    readonly SemaphoreSlim outboxSize_ = new(0);
    readonly ConcurrentBag<TOut> outbox_ = new();

    readonly ILogger logger_ = Log.ForContext<TcpClientTransport<TIn, TOut>>();
    
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
                logger_.Fatal("The input stream has been corrupted by the server.");
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

    public ClientFinishReason FinishReason => finishReason_;

    void TryFinish(ClientFinishReason reason)
    {
        if (finished_.TrySetResult())
            finishReason_ = reason;
    }

    public async Task Run()
    {
        if (Interlocked.CompareExchange(ref running_, 1, 0) != 0)
            throw new InvalidOperationException("The transport is already running.");

        try
        {
            await client_.ConnectAsync(Target);
            stream_ = client_.GetStream();
        }
        catch (InvalidOperationException)
        {
            TryFinish(ClientFinishReason.NetworkError);
        }
        catch (SocketException)
        {
            TryFinish(ClientFinishReason.OsError);
        }

        Task finishedTask = finished_.Task;

        if (finishedTask.IsCompleted)
        {
            Task read = ReadAsync(finishedTask);
            Task write = WriteAsync(finishedTask);

            await Task.WhenAny(read, write);

            if (read.Exception is { InnerException: { } ex1 } )
                logger_.Error(ex1, "Read faulted.");

            if (read.Exception is { InnerException: { } ex2 } )
                logger_.Error(ex2, "Write faulted.");

            TryFinish(ClientFinishReason.NetworkError);

            await Task.WhenAll(read, write);
        }

        if (stream_ is not null)
            await stream_.DisposeAsync();

        client_.Dispose();
    }
    
    public event Action<TIn>? OnMessage;
    public required IPEndPoint Target { get; init; }

    public void SendReliable(TOut message)
    {
        outbox_.Add(message);
        outboxSize_.Release();
    }

    public void SendUnreliable(TOut message)
    {
        outbox_.Add(message);
        outboxSize_.Release();
    }

    public void Terminate() => TryFinish(ClientFinishReason.Disconnect);
}
