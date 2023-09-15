using Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Extensions;

public static class ObjectExtensions
{
    public static void Destroy<T>(this T obj) where T : class, new() => ObjectPool<T>.Destroy(obj);
}
