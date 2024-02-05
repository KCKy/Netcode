using System.Collections.Concurrent;

namespace Useful;

/// <summary>
/// A simple object pool for trivially constructable classes.
/// </summary>
/// <remarks>
/// This class is thread-safe.
/// </remarks>
/// <typeparam name="T">The to be pooled.</typeparam>
public sealed class Pool<T>
    where T : class, new()
{
    readonly ConcurrentBag<T> pooledObjects_ = new(); 
    readonly int maxPooledObjects_ = 32;
    
    /// <summary>
    /// Maximum number of objects to be pooled. Excess objects shall be freed.
    /// </summary>
    public int MaxPooledObjects
    {
        get => maxPooledObjects_;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be zero.");
            maxPooledObjects_ = value;
        }
    }

    /// <summary>
    /// Rent an object from a pool.
    /// </summary>
    /// <returns>A valid instance of the class.</returns>
    public T Rent() => pooledObjects_.TryTake(out T? value) ? value : new();

    /// <summary>
    /// Return an object to a pool.
    /// </summary>
    /// <remarks>
    /// The instance may in origin from elsewhere than the pool.
    /// The caller promises that the instance won't be modified as if destroyed and all references of it shall be shortly dropped.
    /// </remarks>
    /// <param name="obj">An instance to be put in the pool.</param>
    public void Return(T obj)
    {
        if (pooledObjects_.Count < maxPooledObjects_)
            pooledObjects_.Add(obj);
    }
}
