using System;
using Kcky.GameNewt.DataStructures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kcky.GameNewt.Tests;

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
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Basic(int id)
    {
        ClientInputQueue<MockStructure> queue = new(1, (ref MockStructure _) => {}, (_, _, _) => {}, NullLoggerFactory.Instance);
        
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
        public int? IdCapture = null;
        public long? FrameCapture = null;
        public float? DifferenceCapture = null;

        public void Capture(int id, long frame, float difference)
        {
            IdCapture = id;
            FrameCapture = frame;
            DifferenceCapture = difference;
        }

        public void AssertValid(int id, long frame, float min, float max)
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
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, 42)]
    public void InputAuthorLate(int id, int tps)
    {
        InputAuthCapturer capturer = new();
        ClientInputQueue<MockStructure> queue = new(tps, (ref MockStructure _) => {}, capturer.Capture, NullLoggerFactory.Instance);
        
        queue.AddClient(id);

        queue.ConstructAuthoritativeFrame(); // Failed to send frame in time

        queue.AddInput(id, 0, new()); // Send late input

        capturer.AssertValid(id, 0, float.NegativeInfinity, 0);
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
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, 42)]
    public void InputAuthorInTime(int id, int tps)
    {
        InputAuthCapturer capturer = new();
        ClientInputQueue<MockStructure> queue = new(tps, (ref MockStructure _) => {}, capturer.Capture, NullLoggerFactory.Instance);

        queue.AddClient(id);

        queue.AddInput(id, 0, new()); // Send in time input

        queue.ConstructAuthoritativeFrame();

        capturer.AssertValid(id, 0, 0, float.PositiveInfinity);
    }
}
