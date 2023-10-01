using Serilog;

namespace Core.DataStructures;

/// <summary>
/// Reorders complete authoritative inputs from the server for the client's authoritative update loop.
/// This structure assures no index is skipped (unless it is done manually by changing <see cref="CurrentFrame"/>).
/// </summary>
/// <remarks>
/// This structure is thread safe.
/// </remarks>>
/// <typeparam name="TInput">The type of the input.</typeparam>
public interface IUpdateInputQueue<TInput>
{
    /// <summary>
    /// The index of the frame next <see cref="GetNextInputAsync"/> is going to return.
    /// </summary>
    /// <remarks>
    /// Increasing this value will result in throwing away all inputs of frames with lower index.
    /// </remarks>
    long CurrentFrame { get; set; }

    /// <summary>
    /// Adds input for given index.
    /// </summary>
    /// <remarks>
    /// If input for given index has already been provided or input collection has been stopped, the duplicate is ignored.
    /// </remarks>
    /// <param name="frame">Id if the frame update.</param>
    /// <param name="input">The input for the frame update.</param>
    void AddInput(long frame, TInput input);

    /// <summary>
    /// Stops collection of inputs, another <see cref="GetNextInputAsync"/> shall not complete. All future inputs will be ignored.
    /// </summary>
    void Stop();

    /// <summary>
    /// Collects the input for frame index <see cref="CurrentFrame"/>.
    /// </summary>
    /// <returns>Input for current frame index.</returns>
    /// <exception cref="OperationCanceledException">If data collection has been stopped.</exception>
    /// <exception cref="InvalidOperationException">If prior <see cref="GetNextInputAsync"/> has not been awaited.</exception>
    ValueTask<TInput> GetNextInputAsync();
}


/// <inheritdoc/>
public sealed class UpdateInputQueue<TInput> : IUpdateInputQueue<TInput>
{
    long currentFrame_ = 0;

    readonly PriorityQueue<TInput, long> inputs_ = new();
    readonly HashSet<long> heldFrames_ = new();

    readonly object mutex_ = new();

    TaskCompletionSource? waitForNext_ = null;

    bool stopped_ = false;
    bool waiting_ = false;

    readonly ILogger logger_ = Log.ForContext<UpdateInputQueue<TInput>>();

    /// <inheritdoc/>
    public long CurrentFrame
    {
        get
        {
            lock (mutex_)
                return currentFrame_;
        }
        set
        {
            lock (mutex_)
            {
                currentFrame_ = value;
                long frame;

                while (inputs_.TryPeek(out _, out frame) && frame < value)
                {
                    inputs_.Dequeue();
                    heldFrames_.Remove(frame);
                }

                if (frame == value)
                    waitForNext_?.TrySetResult();
            }
        }
    }

    /// <inheritdoc/>
    public void AddInput(long frame, TInput input)
    {
        lock (mutex_)
        {
            if (stopped_)
                return;

            if (frame < currentFrame_)
            {
                logger_.Debug("Duplicate input received for ${frame}", frame);
                return;
            }

            if (heldFrames_.Add(frame))
                inputs_.Enqueue(input, frame);

            if (frame == currentFrame_)
                waitForNext_?.TrySetResult();
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (mutex_)
        {
            stopped_ = true;
            waitForNext_?.TrySetCanceled();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TInput> GetNextInputAsync()
    {
        lock (mutex_)
        {
            if (stopped_)
                throw new OperationCanceledException();

            if (waiting_)
            {
                logger_.Fatal("To wait for more than one time.");
                throw new InvalidOperationException("Cannot wait more than one time.");
            }

            if (inputs_.TryPeek(out _, out long frame) && frame == currentFrame_)
            {
                heldFrames_.Remove(frame);
                currentFrame_++;
                return inputs_.Dequeue();
            }

            waiting_ = true;
            waitForNext_ = new TaskCompletionSource(); // TODO: rewrite without allocation
        }

        await waitForNext_.Task;
        
        lock (mutex_)
        {
            if (stopped_)
                throw new OperationCanceledException();

            waiting_ = false;

            heldFrames_.Remove(currentFrame_);
            currentFrame_++;
            return inputs_.Dequeue();
        }
    }
}
