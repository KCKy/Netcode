using System.Buffers;

namespace Core.Extensions;

 public static class ArrayPoolExtensions
{
    public static Memory<T> RentMemory<T>(this ArrayPool<T> pool, int size) => pool.Rent(size).AsMemory(0, size);
}
