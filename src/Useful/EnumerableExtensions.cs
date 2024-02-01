namespace Useful;

/// <summary>
/// Extensions for <see cref="IEnumerable{T}"/>.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Transforms an enumerable to provide positional indexes with the elements.
    /// </summary>
    /// <typeparam name="T">Type of the enumerated elements.</typeparam>
    /// <param name="self">The enumerable to transforms.</param>
    /// <returns>Enumerable over tuples of positional indexes and values.</returns>
    public static IEnumerable<(int index, T value)> WithIndexes<T>(this IEnumerable<T> self)
    {
        int i = 0;
        foreach (var item in self)
            yield return (i++, item);
    }
}
