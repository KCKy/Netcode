using System.Collections.Concurrent;

namespace DefaultTransport.IpTransport;

interface IPendingMessages<TMessage>
{
    void Post(TMessage message);
    ValueTask<TMessage> GetAsync(CancellationToken cancellation);
}

readonly struct QueueMessages<TMessage> : IPendingMessages<TMessage>
{
    readonly SemaphoreSlim outboxSize_ = new(0);
    readonly ConcurrentQueue<TMessage> outbox_ = new();

    public QueueMessages() { }

    public void Post(TMessage message)
    {
        outbox_.Enqueue(message);
        outboxSize_.Release();
    }

    public async ValueTask<TMessage> GetAsync(CancellationToken cancellation)
    {
        while (true)
        {
            await outboxSize_.WaitAsync(cancellation);
            if (outbox_.TryDequeue(out var value))
                return value;
        }
    }
}

readonly struct BagMessages<TMessage> : IPendingMessages<TMessage>
{
    readonly SemaphoreSlim outboxSize_ = new(0);
    readonly ConcurrentBag<TMessage> outbox_ = new();

    public BagMessages() { }

    public void Post(TMessage message)
    {
        outbox_.Add(message);
        outboxSize_.Release();
    }

    public async ValueTask<TMessage> GetAsync(CancellationToken cancellation)
    {
        while (true)
        {
            await outboxSize_.WaitAsync(cancellation);
            if (outbox_.TryTake(out var value))
                return value;
        }
    }
}
