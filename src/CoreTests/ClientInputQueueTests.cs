using Core;

namespace CoreTests;

using Core.DataStructures;

public class ClientInputQueueTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(42L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void BasicTest(long id)
    {
        ClientInputQueue<MockInput> queue = new();
        MockInput thirdInput = new(1, 1, 1, 1);

        // -1
        Assert.Equal(-1, queue.Frame);

        // 0
        var frame = queue.ConstructAuthoritativeFrame();
        Assert.Equal(0, queue.Frame);
        Assert.True(frame.IsEmpty);

        // 1
        queue.AddClient(id);
        frame = queue.ConstructAuthoritativeFrame();
        queue.AddInput(id, 0, new());
        queue.AddInput(id, 2, thirdInput);

        Assert.Equal(1, queue.Frame);
        Assert.Equal(1, frame.Length);
        frame.Span[0].Assert(id, new(), false);

        // 2
        frame = queue.ConstructAuthoritativeFrame();
        
        Assert.Equal(2, queue.Frame);
        Assert.Equal(1, frame.Length);
        frame.Span[0].Assert(id, thirdInput, false);

        // 3
        queue.RemoveClient(id);
        frame = queue.ConstructAuthoritativeFrame();
        
        Assert.Equal(3, queue.Frame);
        Assert.Equal(1, frame.Length);
        frame.Span[0].Assert(id, new(), true);
        
        // 4
        frame = queue.ConstructAuthoritativeFrame();

        Assert.Equal(4, queue.Frame);
        Assert.True(frame.IsEmpty);

    }
    // TODO: more tests
}
