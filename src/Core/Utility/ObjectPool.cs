using System.Collections.Concurrent;
using System.Diagnostics;
using MemoryPack;

namespace Core.Utility;

/*
/// <summary>
/// Thread-safe, lock free object pool.
/// </summary>
/// <typeparam name="TObject"></typeparam>
public static class ObjectPool<TObject> where TObject : class, new()
{
    // static readonly ConcurrentBag<TObject> Pooled = new();

    /// <summary>
    /// Return the object to the pool.
    /// </summary>
    /// <param name="obj">Object to return to the pool.</param>
    /// <remarks>
    /// All references are hereby invalidated. The argument is set to null.
    /// </remarks>
    public static void Destroy(TObject obj)
    {
        //Pooled.Add(obj);
    }

    /// <summary>
    /// Returns newly valid object from the pool.
    /// </summary>
    /// <returns>Object to be used.</returns>
    public static TObject Create()
    {
        // return Pooled.TryTake(out TObject? obj) ? obj : new TObject();
        return new();
    }
}


public static class DefaultProvider<TObject> where TObject : class, new()
{
    static readonly ReadOnlyMemory<byte> Default = MemoryPackSerializer.Serialize(new TObject());

    public static TObject Create()
    {
        TObject instance = ObjectPool<TObject>.Create();
        MemoryPackSerializer.Deserialize(Default.Span, ref instance!);
        Debug.Assert(instance != null);
        return instance;
    }
}
*/
