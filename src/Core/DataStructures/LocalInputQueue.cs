using Serilog;

namespace Core.DataStructures;

/// <summary>
/// Stores all inputs of the local client between authoritative and predict state.
/// Provides access to these inputs to reevaluate predict state in case of a misprediction.
/// </summary>
/// <typeparam name="TInput"></typeparam>
public interface ILocalInputQueue<TInput>
{
    /// <summary>
    /// Index of the first frame in the queue (inclusive)
    /// </summary>
    long FirstFrame { get; }

    /// <summary>
    /// Index of the last frame in the queue (inclusive)
    /// </summary>
    long LastFrame { get; }

    /// <summary>
    /// Accessor.
    /// </summary>
    /// <param name="frame">Index of the frame.</param>
    /// <returns>Element at given frame index.</returns>
    /// <exception cref="IndexOutOfRangeException">If given index is out the queue range.</exception>
    TInput this[long frame] { get; }

    /// <summary>
    /// Enqueues an element to the queue.
    /// </summary>
    /// <param name="input">Element to add.</param>
    void Add(TInput input);

    /// <summary>
    /// Removes all elements lesser than or equal to given index.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    void Pop(long frame);
}

/// <inheritdoc/>
public sealed class LocalInputQueue<TInput> : ILocalInputQueue<TInput>
{
    readonly Dictionary<long, TInput> frameToInput_ = new();

    /// <inheritdoc/>
    public long FirstFrame { get; private set; } = 0;

    /// <inheritdoc/>
    public long LastFrame { get; private set; } = -1;


    readonly ILogger logger_ = Log.ForContext<LocalInputQueue<TInput>>();

    /// <inheritdoc/>
    public TInput this[long frame]
    {
        get
        {
            if (frame < FirstFrame || frame > LastFrame)
            {
                logger_.Fatal("To access non-contained ([{First}, {Last}]) {index}.", FirstFrame, LastFrame, frame);
                throw new IndexOutOfRangeException("Given frame is not in the queue.");
            }

            return frameToInput_[frame];
        }
    }

    /// <inheritdoc/>
    public void Add(TInput input)
    {
        LastFrame++;
        frameToInput_.Add(LastFrame, input);
    }

    /// <inheritdoc/>
    public void Pop(long frame)
    {
        while (FirstFrame < frame)
        {
            frameToInput_.Remove(FirstFrame);
            FirstFrame++;
        }
    }
}
