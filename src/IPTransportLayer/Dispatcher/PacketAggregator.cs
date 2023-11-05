using System.Buffers;
using Useful;

namespace DefaultTransport.Dispatcher;

public struct PacketAggregator
{
    readonly object mutex_ = new();
    readonly Queue<Memory<byte>> aggregatedPackets_;
    int sum_;
    long oldestFrame_;

    public PacketAggregator()
    {
        aggregatedPackets_ = new();
        sum_ = 0;
        oldestFrame_ = 0;
    }

    Memory<byte> ConstructAggregate(int header, int maxSize)
    {
        lock (mutex_)
        {
            int totalSize = header + sum_;

            while (maxSize < totalSize && aggregatedPackets_.Count > 1)
            {
                Dequeue();
                totalSize = header + sum_;
            }

            var aggregate = ArrayPool<byte>.Shared.RentMemory(totalSize);
            var span = aggregate.Span[header..];

            foreach (var packet in aggregatedPackets_)
            {
                packet.Span.CopyTo(span);
                span = span[packet.Length..];
            }

            return aggregate;
        }
    }

    void Add(Memory<byte> memory, long frame)
    {
        if (aggregatedPackets_.Count <= 0)
            oldestFrame_ = frame;

        aggregatedPackets_.Enqueue(memory);
        sum_ += memory.Length;
    }

    public Memory<byte> AddAndConstruct(Memory<byte> memory, long frame, int header, int maxSize)
    {
        lock (mutex_)
        {
            Add(memory, frame);
            return ConstructAggregate(header, maxSize);
        }
    }

    void Dequeue()
    {
        oldestFrame_++;
        var old = aggregatedPackets_.Dequeue();
        sum_ -= old.Length;

        if (!old.IsEmpty)
            ArrayPool<byte>.Shared.Return(old);
    }

    public void Pop(long frame)
    {
        lock (mutex_)
        {
            while (aggregatedPackets_.Count > 0 && frame >= oldestFrame_)
                Dequeue();
        }
    }
}
