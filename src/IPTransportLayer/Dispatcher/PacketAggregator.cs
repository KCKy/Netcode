using System.Buffers;
using Useful;

namespace DefaultTransport.Dispatcher;

/// <summary>
/// Helper structure for aggregation of data generated over time for into packets.
/// </summary>
/// <example>
/// Instead of sending the information just for time <c>t</c>, we send it for times <c>[t - n, t]</c>, this way some packet loss is acceptable.
/// </example>
public sealed class PacketAggregator
{
    readonly Queue<Memory<byte>> aggregatedPackets_;
    int sum_;
    long oldestFrame_;

    /// <summary>
    /// Constructor.
    /// </summary>
    public PacketAggregator()
    {
        aggregatedPackets_ = new();
        sum_ = 0;
        oldestFrame_ = long.MinValue;
    }

    Memory<byte> ConstructAggregate(int header, int maxSize)
    {
        if (header < 0)
            throw new ArgumentOutOfRangeException(nameof(header), header, "Value cannot be negative.");

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

    void Add(Memory<byte> memory, long frame)
    {
        if (aggregatedPackets_.Count <= 0)
            oldestFrame_ = frame;

        aggregatedPackets_.Enqueue(memory);
        sum_ += memory.Length;
    }

    /// <summary>
    /// Add information of a given frame and construct a packet out of currently aggregated information.
    /// </summary>
    /// <param name="memory">Move of the most current information.</param>
    /// <param name="frame">The frame this information belongs to.</param>
    /// <param name="header">Number of bytes which shall be padded before the packet and left undefined for the caller to write. </param>
    /// <param name="maxSize">Maximum size the packet may have.</param>
    /// <returns>An aggregated packet.</returns>
    /// <remarks>
    /// The aggregated packet has the following format:
    /// <c> [ header: unused area ] [ Data of frame n ] ... [ Data of frame 'frame' ] </c>
    /// Where unprovided and obsolete (see <see cref="Pop"/>) frames are skipped.
    /// If the most current information is longer than <paramref name="maxSize"/>, <paramref name="maxSize"/> may be exceeded to hold the single information.
    /// </remarks>
    public Memory<byte> AddAndConstruct(Memory<byte> memory, long frame, int header, int maxSize)
    {
        Add(memory, frame);
        return ConstructAggregate(header, maxSize);
    }

    void Dequeue()
    {
        oldestFrame_++;
        var old = aggregatedPackets_.Dequeue();
        sum_ -= old.Length;

        if (!old.IsEmpty)
            ArrayPool<byte>.Shared.Return(old);
    }

    /// <summary>
    /// Mark given frame and all preceding as obsolete i.e. they will no longer be added to the aggregate.
    /// </summary>
    /// <param name="frame">The frame to mark as obsolete.</param>
    public void Pop(long frame)
    {
        while (aggregatedPackets_.Count > 0 && frame >= oldestFrame_)
            Dequeue();
    }
}
