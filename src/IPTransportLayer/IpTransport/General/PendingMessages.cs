using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DefaultTransport.IpTransport;

/// <summary>
/// Represents a thread-safe collection of messages to be posted and retrieved.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
interface IPendingMessages<TMessage>
{
    void Post(TMessage message);
    ValueTask<TMessage> GetAsync(CancellationToken cancellation);
}

/// <summary>
/// Implementation of <see cref="IPendingMessages{TMessage}"/> where the posting order is kept.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
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

/// <summary>
/// Implementation of <see cref="IPendingMessages{TMessage}"/>.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
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
