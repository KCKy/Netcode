using Core.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreTests;

public class UpdateInputQueueTests
{
    [Theory]
    [InlineData(1L, 10)]
    public void BasicTest(long frame, int length)
    {
        UpdateInputQueue<int> queue = new();

        for (int i = 0; i < length; i++)
            queue.AddInput(i, i);

        queue.CurrentFrame = frame;

        for (long i = frame; i < length; i++)
        {
            int result = queue.GetNextInputAsync().AsTask().Result;
            Assert.Equal(i, result);
        }
    }

    // TODO: more tests
}
