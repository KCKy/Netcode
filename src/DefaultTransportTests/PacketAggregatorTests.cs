using System.Buffers;
using DefaultTransport.Dispatcher;
using Useful;

namespace DefaultTransportTests;

/// <summary>
/// Tests for <see cref="PacketAggregator"/>.
/// </summary>
public sealed class PacketAggregatorTests
{
    static Memory<byte> GetMemory(long value)
    {
        var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(long));
        Bits.Write(value, mem.Span);
        return mem;
    }

    static void AssertValue(ReadOnlySpan<byte> span, long value)
    {
        long actual = Bits.ReadLong(span);
        Assert.Equal(value, actual);
    }

    /// <summary>
    /// Test whether packet aggregation is consistent.
    /// </summary>
    /// <param name="count">Number of constructing passes.</param>
    /// <param name="offset">The length of the aggregate in element count.</param>
    [Theory]
    [InlineData(100, 10)]
    public void TestSimple(int count, int offset)
    {
        PacketAggregator aggregator = new();

        for (int i = 0; i < count; i++)
        {
            aggregator.Pop(i - offset);
            var packet = aggregator.AddAndConstruct(GetMemory(i), i, 0, int.MaxValue).Span;

            for (int j = Math.Max(i - offset + 1, 0); j <= i; j++)
            {
                AssertValue(packet, j);
                packet = packet[sizeof(long)..];
            }
        }
    }
}
