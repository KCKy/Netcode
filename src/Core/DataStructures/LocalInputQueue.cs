﻿using Serilog;

namespace Core.DataStructures;

/// <summary>
/// Stores all inputs of the local client between authoritative and predict state.
/// Provides access to these inputs to reevaluate predict state in case of a misprediction.
/// </summary>
/// <typeparam name="TInput"></typeparam>
public interface ILocalInputQueue<TInput>
{
    /// <summary>
    /// Accessor.
    /// </summary>
    /// <param name="frame">Index of the frame.</param>
    /// <returns>Element at given frame index.</returns>
    /// <exception cref="IndexOutOfRangeException">If given index is out the queue range.</exception>
    TInput? this[long frame] { get; }

    /// <summary>
    /// Enqueues an element to the queue.
    /// </summary>
    /// <param name="input">Element to add.</param>
    void Add(TInput input, long frame);

    void Set(long frame);

    /// <summary>
    /// Marks all elements lesser than or equal to given index (even those yet to be added) to be deleted.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    void Pop(long frame);
}

/// <inheritdoc/>
public sealed class LocalInputQueue<TInput> : ILocalInputQueue<TInput>
where TInput : class
{
    readonly object mutex_ = new();
    readonly Dictionary<long, TInput> frameToInput_ = new();
    readonly ILogger Logger = Log.ForContext<LocalInputQueue<TInput>>();

    long firstFrame_ = 0;
    long lastFrame_ = -1;

    /// <inheritdoc/>
    public TInput? this[long frame]
    {
        get
        {
            lock (mutex_)
            {
                if (frame >= firstFrame_ && frame <= lastFrame_)
                    return frameToInput_[frame];
                
                Logger.Debug("To access non-contained ([{First}, {Last}]) {index}.", firstFrame_, lastFrame_, frame);
                return null;
            }
        }
    }

    /// <inheritdoc/>
    public void Add(TInput input, long frame)
    {
        lock (mutex_)
        {
            if (frame <= lastFrame_)
                return;

            if (frame != lastFrame_ + 1)
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "Frame must be consecutive.");

            lastFrame_++;
            Logger.Verbose("Adding local client input for frame {Frame}.", lastFrame_);
            frameToInput_.Add(lastFrame_, input);
        }
    }

    public void Set(long frame)
    {
        lock (mutex_)
        {
            Pop(frame);

            if (lastFrame_ < firstFrame_)
                lastFrame_ = firstFrame_ - 1;
        }
    }

    /// <inheritdoc/>
    public void Pop(long frame)
    {
        lock (mutex_)
        {
            while (firstFrame_ <= frame)
            {
                frameToInput_.Remove(firstFrame_);
                firstFrame_++;
            }
        }
    }
}
