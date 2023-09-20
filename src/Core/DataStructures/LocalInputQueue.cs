using Serilog;

namespace Core.DataStructures;

/// <summary>
/// Stores all inputs of the local client between authoritative and predict state.
/// Provides access to these inputs to reevaluate predict state in case of a misprediction.
/// </summary>
/// <typeparam name="TInput"></typeparam>
public sealed class LocalInputQueue<TInput>
{
    readonly Dictionary<long, TInput> frameToInput_ = new();

    /// <summary>
    /// Index of the first frame in the queue (inclusive)
    /// </summary>
    public long FirstFrame { get; private set; } = 0;

    /// <summary>
    /// Index of the last frame in the queue (inclusive)
    /// </summary>
    public long LastFrame { get; private set; } = -1;

    long offset_ = 0;

    /// <summary>
    /// Moves the queue interval by an offset.
    /// </summary>
    /// <param name="offset">Offset to move the interval. Each element will have its index incremented by this value.</param>
    public void SetOffset(long offset)
    {
        offset_ += offset;
        FirstFrame += offset;
        LastFrame += offset;
    }

    readonly ILogger logger_ = Log.ForContext<LocalInputQueue<TInput>>();

    /// <summary>
    /// Accessor.
    /// </summary>
    /// <param name="frame">Index of the frame.</param>
    /// <returns>Element at given frame index.</returns>
    /// <exception cref="IndexOutOfRangeException">If given index is out the queue range.</exception>
    public TInput this[long frame]
    {
        get
        {
            if (frame < FirstFrame || frame > LastFrame)
            {
                logger_.Fatal("To access non-contained ([{First}, {Last}]) {index}.", FirstFrame, LastFrame, frame);
                throw new IndexOutOfRangeException("Given frame is not in the queue.");
            }

            return frameToInput_[frame - offset_];
        }
    }

    /// <summary>
    /// Enqueues an element to the queue.
    /// </summary>
    /// <param name="input">Element to add.</param>
    public void Add(TInput input)
    {
        LastFrame++;
        frameToInput_.Add(LastFrame - offset_, input);
    }

    /// <summary>
    /// Removes the first element from the queue.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Pop()
    {
        if (FirstFrame > LastFrame)
        {
            logger_.Fatal("To remove from empty collection ([{First}, {Last}]).", FirstFrame, LastFrame);
            throw new InvalidOperationException("There is no element to remove.");
        }

        frameToInput_.Remove(FirstFrame - offset_);
        FirstFrame++;
    }
}
