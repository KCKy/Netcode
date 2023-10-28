using Core.DataStructures;

namespace CoreTests;

public class LocalInputQueueTests
{
    /*
    [Theory]
    [InlineData(10L, 1, 2, 3)]
    [InlineData(-10L, 1, 2, 3, 4, 5, 6, 7, 8)]
    [InlineData(10L, 1)]
    public void BasicTest(long offset, params int[] data)
    {
        /*
        LocalInputQueue<int> queue = new();

        foreach (int x in data)
            queue.Add(x);

        queue.SetOffset(offset);

        int i = 0;
        foreach (int x in data)
        {
            Assert.Equal(x, queue[i + offset]);
            i++;
        }

        queue.Pop();

        try
        {
            _ = queue[offset];
        }
        catch (IndexOutOfRangeException)
        {
            return;
        }

        Assert.Fail("No exception was thrown.");
    }*/
}
