using System;
using System.Numerics;
using Kcky.Useful;
using MemoryPack;

namespace Kcky.GameNewt.Utility;

[MemoryPackable]
public sealed partial class Random
{
    [MemoryPackInclude]
    ulong s0_, s1_, s2_, s3_;

    [MemoryPackConstructor]
    Random() : this(42, 42, 42, 42) { }

    /// <summary>
    /// Construct the random number generator with given 256-bit seed.
    /// The seed must be non-zero.
    /// </summary>
    /// <param name="a">First part of the seed.</param>
    /// <param name="b">Second part of the seed.</param>
    /// <param name="c">Third part of the seed.</param>
    /// <param name="d">Fourth part of the seed.</param>
    /// <exception cref="ArgumentException">The seed is all zeroes.</exception>
    public Random(long a, long b, long c, long d)
    {
        if (a == 0 && b == 0 && c == 0 && d == 0)
            throw new ArgumentException("The seed must be non-zero.");

        s0_ = (ulong)a;
        s1_ = (ulong)b;
        s2_ = (ulong)c;
        s3_ = (ulong)d;
    }

    uint NextUInt32() => (uint)(NextUInt64() >> 32);

    // NextUInt64 is based on the algorithm from http://prng.di.unimi.it/xoshiro256starstar.c:
    //
    //     Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
    //
    //     To the extent possible under law, the author has dedicated all copyright
    //     and related and neighboring rights to this software to the public domain
    //     worldwide. This software is distributed without any warranty.
    //
    //     See <http://creativecommons.org/publicdomain/zero/1.0/>.
    ulong NextUInt64()
    {
        ulong s0 = s0_, s1 = s1_, s2 = s2_, s3 = s3_;
 
        ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;
        ulong t = s1 << 17;
 
        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;
 
        s2 ^= t;
        s3 = BitOperations.RotateLeft(s3, 45);
 
        s0_ = s0;
        s1_ = s1;
        s2_ = s2;
        s3_ = s3;
 
        return result;
    }

    // NextUInt32/64 algorithms based on https://arxiv.org/pdf/1805.10941.pdf and https://github.com/lemire/fastrange.
    uint NextUInt32(uint maxValue)
    {
        ulong randomProduct = (ulong)maxValue * NextUInt32();
        uint lowPart = (uint)randomProduct;
 
        if (lowPart < maxValue)
        {
            uint remainder = (0u - maxValue) % maxValue;
 
            while (lowPart < remainder)
            {
                randomProduct = (ulong)maxValue * NextUInt32();
                lowPart = (uint)randomProduct;
            }
        }
 
        return (uint)(randomProduct >> 32);
    }

    ulong NextUInt64(ulong maxValue)
    {
        ulong randomProduct = Math.BigMul(maxValue, NextUInt64(), out ulong lowPart);
 
        if (lowPart < maxValue)
        {
            ulong remainder = (0ul - maxValue) % maxValue;
 
            while (lowPart < remainder)
            {
                randomProduct = Math.BigMul(maxValue, NextUInt64(), out lowPart);
            }
        }
 
        return randomProduct;
    }
    
    /// <inheritdoc cref="System.Random.Next()"/>
    public int Next()
    {
        while (true)
        {
            ulong result = NextUInt64() >> 33;
            if (result != int.MaxValue)
                return (int)result;
        }
    }

    /// <inheritdoc cref="System.Random.Next(int)"/>
    public int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue, nameof(maxValue));
        return (int)NextUInt32((uint)maxValue);
    }

    /// <inheritdoc cref="System.Random.Next(int, int)"/>
    public int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        return (int)NextUInt32((uint)(maxValue - minValue)) + minValue;
    }

    /// <inheritdoc cref="System.Random.NextInt64()"/>
    public long NextInt64()
    {
        while (true)
        {
            ulong result = NextUInt64() >> 1;
            if (result != long.MaxValue)
            {
                return (long)result;
            }
        }
    }

    /// <inheritdoc cref="System.Random.NextInt64(long)"/>
    public long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (long)NextUInt64((ulong)maxValue);
    }

    /// <inheritdoc cref="System.Random.NextInt64(long, long)"/>
    public long NextInt64(long minValue, long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        return (long)NextUInt64((ulong)(maxValue - minValue)) + minValue;
    }

    /// <summary>Returns a random <see cref="Fixed"/> that is greater than or equal to 0 and less than 1.</summary>
    /// <returns>A <see cref="Fixed"/> that is greater than or equal to 0, and less than 1.</returns>
    public Fixed NextFixed() => Fixed.FromBinary(NextInt64() & 0xFF_FF_FF_FFL);
    
    /// <inheritdoc cref="System.Random.Shuffle{T}(Span{T})"/>
    public void Shuffle<T>(Span<T> values)
    {
        int n = values.Length;
 
        for (int i = 0; i < n - 1; i++)
        {
            int j = Next(i, n);
 
            if (j != i)
                (values[i], values[j]) = (values[j], values[i]);
        }
    }

    /// <inheritdoc cref="System.Random.GetItems{T}(ReadOnlySpan{T},Span{T})"/>
    public void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination)
    {
        if (choices.IsEmpty)
            throw new ArgumentException("The number of choices cannot be zero.", nameof(choices));
        
        for (int i = 0; i < destination.Length; i++)
            destination[i] = choices[Next(choices.Length)];
    }
}
