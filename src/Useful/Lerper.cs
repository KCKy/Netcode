﻿using System.Collections.Concurrent;

namespace Useful;

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
    float currentFrameTime_ = 0f;

    double NewCountStrength { get; init; } = 100000;

    double FrameCountTarget { get; init; } = 1.5;

    double weightedFrameCountAverage_ = 0;

    float MakeSmooth(float delta)
    { 
        int frameCount = frames_.Count;

        double weight = Math.Pow(NewCountStrength, delta);

        double oldAverage = weightedFrameCountAverage_;

        weightedFrameCountAverage_ = (oldAverage + frameCount * (weight - 1)) / weight; 
        
        double newSpeed = weightedFrameCountAverage_ / FrameCountTarget;

        //Log.Debug("P = {P}, X = {X}, D = {D}, f(P, X, D) = {f}, g(P, X, D) = {g}", oldAverage, frameCount, delta, weightedFrameCountAverage_, newSpeed);

        return (float)(delta * newSpeed);
    }

    /// <summary>
    /// Moves the lerper by given delta, switches to next frame if available.
    /// </summary>
    /// <param name="delta">The amount of time which has passed.</param>
    /// <returns>The lerp amount between the current frame and the next frame.</returns>
    (float t, Frame? targetFrame) UpdateFrameOffset(float delta)
    {
        currentFrameTime_ += MakeSmooth(delta);

        while (true)
        {
            if (!frames_.TryPeek(out Frame targetFrame))
                return (0, null);
            
            if (currentFrameTime_ < targetFrame.Length)
                return (currentFrameTime_ / targetFrame.Length, targetFrame);

            currentFrameTime_ -= targetFrame.Length;
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
        foreach ((long id, T from) in currentFrame_.IdToEntity)
        {
            onEntityDraw(from, from, t);
        }
    }

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

    public int FramesBehind => frames_.Count;

    public delegate void EntityDraw(T previous, T current, float t);
    public event EntityDraw? OnEntityDraw;
}
