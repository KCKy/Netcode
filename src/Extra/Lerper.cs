using System.Collections.Concurrent;

namespace Extra;

public sealed class Lerper<T>
{
    record struct Frame(Dictionary<long, T> IdToEntity, float Length);

    readonly ConcurrentQueue<Frame> frames_ = new();

    Frame collectedFrame_ = new(new(), 0); // Frame which is correctly being constructed.

    Frame currentFrame_ = new(new(), 0); // The current frame we are lerping from.

    /// <summary>
    /// Add entity to current frame generation.
    /// </summary>
    /// <param name="id">Unique id of the entity.</param>
    /// <param name="value">Value of entity.</param>
    public void AddEntity(long id, T value) => collectedFrame_.IdToEntity.Add(id, value);

    /// <summary>
    /// End current frame generation.
    /// </summary>
    /// <param name="length">The time between the previous frame and this one.</param>
    public void NextFrame(float length)
    {
        collectedFrame_.Length = length;
        frames_.Enqueue(collectedFrame_);
        collectedFrame_ = new(new(), 0); // TODO: pool this
    }

    // Seconds we spend drawing the current frame
    float currentFrameTime_ = -0.1f;

    Frame? targetFrame_ = null;

    /// <summary>
    /// Moves the lerper by given delta, switches to next frame if available.
    /// </summary>
    /// <param name="delta">The amount of time which has passed.</param>
    /// <returns>The lerp amount between the current frame and the next frame.</returns>
    float UpdateFrameOffset(float delta)
    {
        currentFrameTime_ += delta;

        while (true)
        {
            if (frames_.TryPeek(out Frame value))
                targetFrame_ = value;

            if (targetFrame_ is not { } targetFrame )
                return 0; // There is no next frame to target

            if (currentFrameTime_ > targetFrame.Length)
            {
                currentFrameTime_ -= targetFrame.Length;
                currentFrame_ = targetFrame;
                frames_.TryDequeue(out _);
                continue;
            }

            return currentFrameTime_ / targetFrame.Length;
        }
    }

    void DrawNoPredictFallback(float t, EntityDraw onEntityDraw)
    {
        foreach (T from in currentFrame_.IdToEntity.Values)
            onEntityDraw(from, from, 0);
    }

    void DrawProper(float t, Dictionary<long, T> idToTarget, EntityDraw onEntityDraw)
    {
        foreach ((long id, T from) in currentFrame_.IdToEntity)
        {
            if (!idToTarget.TryGetValue(id, out T? target))
                continue;

            onEntityDraw(from, target, t);
        }
    }

    public void Draw(float delta)
    {
        float t = UpdateFrameOffset(delta);

        if (OnEntityDraw is not { } onEntityDraw)
            return;

        if (targetFrame_ is not { IdToEntity: var idToTarget })
            DrawNoPredictFallback(t, onEntityDraw);
        else
            DrawProper(t, idToTarget, onEntityDraw);
    }

    public delegate void EntityDraw(T previous, T current, float t);
    public event EntityDraw? OnEntityDraw;
}
