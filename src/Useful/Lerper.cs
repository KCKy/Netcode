using System.Collections.Concurrent;

namespace Useful;

/// <summary>
/// Provides information about the state of a lerper.
/// </summary>
public interface ILerperInfo
{ 
    /// <summary>
    /// Amount of frames currently in the lerper queue. See <see cref="Lerper{T}"/> for details.
    /// </summary>
    int FramesBehind { get; }

    /// <summary>
    /// Normalized time [0, 1] of the transition from the current frame to the next.
    /// </summary>
    public float CurrentFrameProgression { get; }
}

/// <summary>
/// Provides a way to tween discrete keyframes of entities for drawing.
/// </summary>
/// <example>
/// <code>
/// Lerper&lt;Entity&gt; lerper_ = new();
///
/// void Initialize()
/// {
///     lerper_.OnEntityDraw += HandleEntityDraw;
/// }
///
/// void HandleEntityDraw(Entity previous, Entity current, float t)
/// {
///     previous.DrawTweenedTo(current, t); // Draw an intermediate state from previous to current e.g. Lerp(previous, current, t)
/// }
///
/// void Simulate()
/// {
///     const float simulationDelta = //...
///     // State update code here
/// 
///     Entity entity = // ...
///     long entityId = entity.Id;
///
///     lerper_.AddEntity(entityId, entity);
///
///     // Do this for all relevant entities
///
///     lerper_.NextFrame(simulationDelta); // Finish frame production
/// }
///
/// void Draw(float delta)
/// {
///     lerper_.Draw(delta);
/// 
///     // Do other rendering.
/// }
/// </code>
/// </example>
/// <remarks>
/// <pare>
/// <see cref="Lerper{T}"/> works by keeping a frame queue (queue of frames which were not yet displayed by drawing),
/// and modifying playback speed to keep the queue non-empty. For this it keeps a weighted average of the queue size over time.
/// Needed speed coefficient is deduced from this average. To disregard old information an exponential weight function is used for calculating
/// the average. To change the weight function distribution see <see cref="Lerper{T}.WindowFunctionMedian"/>. To change the target average queue size see <see cref="Lerper{T}.FrameCountTarget"/>.
/// </pare>
/// <pare>
/// Threading model: it is safe to call <see cref="AddEntity"/> and <see cref="NextFrame"/> in a single thread and <see cref="Draw"/> concurrently in a different thread.
/// There can be a single synchronized producer of data to draw, and a single synchronized consumer which draws the produced data, i.e. a simulation thread and a draw thread.
/// </pare>
/// </remarks>
/// <typeparam name="T">The type of the state to be interpolated.</typeparam>
public sealed class Lerper<T> : ILerperInfo
{
    record struct Frame(Dictionary<long, T> IdToEntity, float Length);

    readonly Pool<Dictionary<long, T>> dictPool_ = new();

    readonly ConcurrentQueue<Frame> frames_ = new();

    Frame collectedFrame_ = new(new(), 0); // Frame which is currently being constructed.

    Frame currentFrame_ = new(new(), 0); // The current frame we are lerping from.

    /// <summary>
    /// Collect entity state for the current frame generation.
    /// </summary>
    /// <param name="id">Unique id of the entity.</param>
    /// <param name="value">State of the entity.</param>
    public void AddEntity(long id, T value) => collectedFrame_.IdToEntity.Add(id, value);

    /// <summary>
    /// End current frame generation. Puts the collected frame onto the interpolation queue. Starts collection of the next frame.
    /// </summary>
    /// <param name="length">The time this collected frame is supposed to take.</param>
    public void NextFrame(float length)
    {
        collectedFrame_.Length = length;
        frames_.Enqueue(collectedFrame_);
        collectedFrame_ = new(dictPool_.Rent(), 0);
    }

    // Seconds we spend drawing the current frame
    float currentFrameTime_ = 0f;

    /// <inheritdoc/>
    public float CurrentFrameProgression { get; private set; }

    /// <summary>
    /// Constructor.
    /// </summary>
    public Lerper()
    {
        WindowFunctionMedian = 0.05f;
    }

    /// <summary>
    /// The median of the average weight windowing function i.e. the amount of time which accounts for the recent 50 % of the weight function.
    /// </summary>
    public float WindowFunctionMedian
    {
        get => MathF.Log(2, windowFuncBase_);
        init
        {
            if (!float.IsRealNumber(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be a positive real number.");
            windowFuncBase_ = MathF.Pow(2, 1 / value);
        }
    }

    /// <summary>
    /// The target average of number of frames in the frame queue.
    /// </summary>
    public float FrameCountTarget { get; init; } = 1.5f;

    // Previously calculated average number of frames in the queue
    float previousAverage_ = 0; 

    // Previous count of frames in the queue
    int previousCount_ = 0;

    readonly float windowFuncBase_;

    float GetAverage(float delta)
    {
        int frameCount = previousCount_;
        previousCount_ = frames_.Count;

        float weight = MathF.Pow(windowFuncBase_, delta);

        float updatedAverage = (previousAverage_ + frameCount * (weight - 1)) / weight;

        previousAverage_ = updatedAverage;
        return updatedAverage;
    }

    float GetSpeed(float delta) => GetAverage(delta) / FrameCountTarget;

    (float t, Frame? targetFrame) UpdateFrameOffset(float delta)
    {
        // Moves the lerper by given delta, switches to next frame if available.

        currentFrameTime_ += delta * GetSpeed(delta);

        while (true)
        {
            if (!frames_.TryPeek(out Frame targetFrame))
            {
                CurrentFrameProgression = 0;
                return (0, null);
            }
            
            if (currentFrameTime_ < targetFrame.Length)
            {
                float progression = currentFrameTime_ / targetFrame.Length;
                CurrentFrameProgression = progression;
                return (progression, targetFrame);
            }
            
            currentFrameTime_ -= targetFrame.Length;
            
            currentFrame_.IdToEntity.Clear();
            dictPool_.Return(currentFrame_.IdToEntity);
            currentFrame_ = targetFrame;
            frames_.TryDequeue(out _);
        }
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

    void DrawImproper(float t, EntityDraw onEntityDraw)
    {
        foreach ((_, T from) in currentFrame_.IdToEntity)
            onEntityDraw(from, from, t);
    }

    /// <summary>
    /// Draw with given time progression.
    /// </summary>
    /// <param name="delta">The amount of time passed since the last draw.</param>
    public void Draw(float delta)
    {
        var result = UpdateFrameOffset(delta);
        if (OnEntityDraw is not { } onEntityDraw)
            return;

        if (result is (_, { } target))
            DrawProper(result.t, target.IdToEntity, onEntityDraw);
        else
            DrawImproper(result.t, onEntityDraw);
    }

    /// <inheritdoc/>
    public int FramesBehind => frames_.Count;

    /// <summary>
    /// Signal to draw an entity.
    /// </summary>
    /// <param name="previous">Entity state for frame which just exited the queue.</param>
    /// <param name="current">Entity state for frame which is at the end of the queue.</param>
    /// <param name="t">Weight in range <c>[0,1]</c> defining the transition from <paramref name="previous"/> to <paramref name="current"/>.
    /// 0 means full <paramref name="previous"/>, 1 full <paramref name="current"/>.</param>
    /// <remarks>
    /// If the queue is empty <paramref name="previous"/> equals <paramref name="current"/>.
    /// </remarks>
    public delegate void EntityDraw(T previous, T current, float t);

    /// <summary>
    /// Called on each draw call for each valid entity.
    /// </summary>
    /// <remarks>
    /// This event is raised only within the corresponding <see cref="Draw"/> call.
    /// </remarks>
    public event EntityDraw? OnEntityDraw;
}
