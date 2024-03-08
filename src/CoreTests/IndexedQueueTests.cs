using System;
using System.Linq;
using Core.DataStructures;

namespace CoreTests;

/// <summary>
/// Test class for <see cref="InputQueue{TInput}"/>
/// </summary>
public sealed class IndexedQueueTests
{
    record MockData(int Value);

    /// <summary>
    /// Test whether an empty queue behaves correctly.
    /// </summary>
    /// <param name="startValue">The reset value of the queue.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(42)]
    public void Empty(int startValue)
    {
        IndexedQueue<MockData> queue = new();

        queue.Set(startValue);

        // Queue is empty
        Assert.Null(queue[0]);
        Assert.Null(queue[1]);
        Assert.Null(queue[-1]);
        Assert.Null(queue[long.MaxValue]);
        Assert.Null(queue[long.MinValue]);
        Assert.Null(queue[42]);
        Assert.Null(queue[-42]);
    }

    /// <summary>
    /// Test whether adding elements works.
    /// </summary>
    /// <param name="startValue">The reset value of the queue.</param>
    /// <param name="count">The count of added elements.</param>
    [Theory]
    [InlineData(0, 50)]
    [InlineData(2, 50)]
    [InlineData(42, 50)]
    public void Add(int startValue, int count)
    {
        IndexedQueue<MockData> queue = new();

        queue.Set(startValue);

        foreach (MockData data in from x in Enumerable.Range(startValue, count) select new MockData(x))
        {
            long index = queue.Add(data);
            Assert.Equal(data.Value, index);

            for (int i = startValue; i <= data.Value; i++)
                Assert.Equal(i, queue[i]?.Value);

            Assert.Null(queue[startValue - 1]);
            Assert.Null(queue[data.Value + 1]);
        }
    }

    /// <summary>
    /// Test whether removing elements works.
    /// </summary>
    /// <param name="startValue">The reset value of the queue.</param>
    /// <param name="count">The count of added elements.</param>
    [Theory]
    [InlineData(0, 50)]
    [InlineData(2, 50)]
    [InlineData(42, 50)]
    public void Pop(int startValue, int count)
    {
        IndexedQueue<MockData> queue = new();

        queue.Set(startValue);

        foreach (MockData data in from x in Enumerable.Range(startValue, count) select new MockData(x))
        {
            long index = queue.Add(data);
            Assert.Equal(data.Value, index);
        }

        int end = startValue + count;

        for (int i = startValue; i < end; i++)
        {
            queue.Pop(i);

            for (int j = i + 1; j < end; j++)
                Assert.Equal(j, queue[j]?.Value);

            Assert.Null(queue[i]);
            Assert.Null(queue[end]);
        }
    }
}
