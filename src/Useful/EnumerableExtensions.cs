using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Useful;

public static class EnumerableExtensions
{
    public static IEnumerable<(int index, T value)> WithIndexes<T>(this IEnumerable<T> self)
    {
        int i = 0;
        foreach (var item in self)
            yield return (i++, item);
    }
}
