using Core.DataStructures;
using Core.Providers;

namespace CoreTests;

/// <summary>
/// Tests for <see cref="ClientInputQueue{TClientInput}"/>.
/// </summary>
public sealed class ClientInputQueueTests
{
    /// <summary>
    /// Check whether the queue works in a simple use case. 
    /// </summary>
    /// <param name="id">The id of the fake client.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(42L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Basic(long id)
    {
        ClientInputQueue<MockStructure> queue = new(1, new DefaultClientInputPredictor<MockStructure>());
        
        MockStructure thirdStructure = new(1, 1, 1, 1);

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
        queue.AddInput(id, 2, thirdStructure);

        Assert.Equal(1, queue.Frame);
        Assert.Equal(1, frame.Length);
        frame.Span[0].Assert(id, new(), false);

        // 2
        frame = queue.ConstructAuthoritativeFrame();
        
        Assert.Equal(2, queue.Frame);
        Assert.Equal(1, frame.Length);
        frame.Span[0].Assert(id, thirdStructure, false);

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

    sealed class InputAuthCapturer
    {
        public long? IdCapture = null;
        public long? FrameCapture = null;
        public TimeSpan? DifferenceCapture = null;

        public void Capture(long id, long frame, TimeSpan difference)
        {
            IdCapture = id;
            FrameCapture = frame;
            DifferenceCapture = difference;
        }

        public void AssertValid(long id, long frame, TimeSpan min, TimeSpan max)
        {
            Assert.Equal(id, IdCapture);
            Assert.Equal(frame, FrameCapture);
            Assert.NotNull(DifferenceCapture);
            Assert.InRange(DifferenceCapture.Value, min, max);
        }
    }

    /// <summary>
    /// Check whether the queue notifies about late inputs.
    /// </summary>
    /// <param name="id">The id of the fake client.</param>
    /// <param name="tps">The tps of the fake server.</param>
    [Theory]
    [InlineData(0L, 1)]
    [InlineData(-1L, 42)]
    [InlineData(1L, 1)]
    [InlineData(42L, 42)]
    [InlineData(long.MaxValue, 1)]
    [InlineData(long.MinValue, 42)]
    public void InputAuthorLate(long id, int tps)
    {
        ClientInputQueue<MockStructure> queue = new(tps, new DefaultClientInputPredictor<MockStructure>());
        InputAuthCapturer capturer = new();

        queue.OnInputAuthored += capturer.Capture;

        queue.AddClient(id);

        queue.ConstructAuthoritativeFrame(); // Failed to send frame in time

        queue.AddInput(id, 0, new()); // Send late input

        capturer.AssertValid(id, 0, TimeSpan.MinValue, TimeSpan.Zero);
    }

    /// <summary>
    /// Check whether the queue notifies inputs which are provided in time.
    /// </summary>
    /// <param name="id">The id of the fake client.</param>
    /// <param name="tps">The tps of the fake server.</param>
    [Theory]
    [InlineData(0L, 1)]
    [InlineData(-1L, 42)]
    [InlineData(1L, 1)]
    [InlineData(42L, 42)]
    [InlineData(long.MaxValue, 1)]
    [InlineData(long.MinValue, 42)]
    public void InputAuthorInTime(long id, int tps)
    {
        ClientInputQueue<MockStructure> queue = new(tps, new DefaultClientInputPredictor<MockStructure>());
        InputAuthCapturer capturer = new();
        queue.OnInputAuthored += capturer.Capture;

        queue.AddClient(id);

        queue.AddInput(id, 0, new()); // Send in time input

        queue.ConstructAuthoritativeFrame();

        capturer.AssertValid(id, 0, TimeSpan.Zero, TimeSpan.MaxValue);
    }
}
