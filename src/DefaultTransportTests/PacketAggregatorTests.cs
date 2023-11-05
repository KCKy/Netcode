using System.Buffers;
using DefaultTransport.Dispatcher;
using Useful;

namespace DefaultTransportTests;
public class PacketAggregatorTests
{
    Memory<byte> RentMemory(long value)
    {
        var mem = ArrayPool<byte>.Shared.RentMemory(sizeof(long));
        Bits.Write(value, mem.Span);
        return mem;
    }

    void AssertValue(ReadOnlySpan<byte> span, long value)
    {
        long actual = Bits.ReadLong(span);
        Assert.Equal(value, actual);
    }

    [Theory]
    [InlineData(100, 10)]
    void TestSimple(int count, int offset)
    {
        PacketAggregator aggregator = new();

        for (int i = 0; i < count; i++)
        {
            aggregator.Pop(i - offset);
            var packet = aggregator.AddAndConstruct(RentMemory(i), i, 0, int.MaxValue).Span;

            for (int j = Math.Max(i - offset + 1, 0); j <= i; j++)
            {
                AssertValue(packet, j);
                packet = packet[sizeof(long)..];
            }
        }
    }
}
