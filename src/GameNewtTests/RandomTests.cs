using System;
using System.Collections.Generic;
using System.Reflection;
using Kcky.Useful;
using Random = Kcky.GameNewt.Utility.Random;

namespace Kcky.GameNewt.Tests;

/// <summary>
/// Tests for <see cref="Random"/>
/// </summary>
public sealed class RandomTests
{
    static readonly int SampleCount = 1000;
    static readonly int PerSeedSteps = 10000;

    readonly record struct Seed(long A, long B, long C, long D);

    static System.Random GetOriginalRandom(Seed seed)
    {
        System.Random random = new();

        FieldInfo implField = typeof(System.Random).GetField("_impl", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object impl = implField.GetValue(random)!;
        Type implType = impl.GetType();

        FieldInfo seedA = implType.GetField("_s0", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo seedB = implType.GetField("_s1", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo seedC = implType.GetField("_s2", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo seedD = implType.GetField("_s3", BindingFlags.Instance | BindingFlags.NonPublic)!;

        seedA.SetValue(impl, (ulong)seed.A);
        seedB.SetValue(impl, (ulong)seed.B);
        seedC.SetValue(impl, (ulong)seed.C);
        seedD.SetValue(impl, (ulong)seed.D);

        return random;
    }

    static Random GetOurRandom(Seed seed) => new(seed.A, seed.B, seed.C, seed.D);

    static IEnumerable<Seed> GetSeedSequence()
    {
        System.Random seedGenerator = new(42);

        for (int i = 0; i < SampleCount; i++)
        {
            yield return new()
            {
                A = seedGenerator.NextInt64(),
                B = seedGenerator.NextInt64(),
                C = seedGenerator.NextInt64(),
                D = seedGenerator.NextInt64()
            };
        }
    }

    /// <summary>
    /// Test for <see cref="Random.Next()"/>.
    /// </summary>
    [Fact]
    public void Next()
    {
        foreach (Seed seed in GetSeedSequence())
        {
            Random our = GetOurRandom(seed);
            System.Random original = GetOriginalRandom(seed);

            for (int i = 0; i < PerSeedSteps; i++)
                Assert.Equal(original.Next(), our.Next());
        }
    }

    /// <summary>
    /// Test for <see cref="Random.Next(int)"/>.
    /// </summary>
    /// <param name="maxValue">Max value.</param>
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(int.MaxValue / 2)]
    [InlineData(int.MaxValue)]
    [Theory]
    public void NextMax(int maxValue)
    {
        foreach (Seed seed in GetSeedSequence())
        {
            Random our = GetOurRandom(seed);
            System.Random original = GetOriginalRandom(seed);

            for (int i = 0; i < PerSeedSteps; i++)
                Assert.Equal(original.Next(maxValue), our.Next(maxValue));
        }
    }

    /// <summary>
    /// Test for <see cref="Random.Next(int, int)"/>.
    /// </summary>
    /// <param name="minValue">Min value.</param>
    /// <param name="maxValue">Max value.</param>
    [InlineData(0, 10)]
    [InlineData(-10, 10)]
    [InlineData(-1000, 1000)]
    [InlineData(int.MinValue, 0)]
    [InlineData(0, int.MaxValue)]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(int.MinValue/2, int.MaxValue / 2)]
    [Theory]
    public void NextMinMax(int minValue, int maxValue)
    {
        foreach (Seed seed in GetSeedSequence())
        {
            Random our = GetOurRandom(seed);
            System.Random original = GetOriginalRandom(seed);

            for (int i = 0; i < PerSeedSteps; i++)
                Assert.Equal(original.Next(minValue, maxValue), our.Next(minValue, maxValue));
        }
    }

    /// <summary>
    /// Test for <see cref="Random.NextInt64()"/>.
    /// </summary>
    [Fact]
    public void NextInt64()
    {
        foreach (Seed seed in GetSeedSequence())
        {
            Random our = GetOurRandom(seed);
            System.Random original = GetOriginalRandom(seed);

            for (int i = 0; i < PerSeedSteps; i++)
                Assert.Equal(original.NextInt64(), our.NextInt64());
        }
    }

    /// <summary>
    /// Test for <see cref="Random.NextInt64(long)"/>.
    /// </summary>
    /// <param name="maxValue">Max value.</param>
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(int.MaxValue / 2)]
    [InlineData(int.MaxValue)]
    [Theory]
    public void NextInt64Max(long maxValue)
    {
        foreach (Seed seed in GetSeedSequence())
        {
            Random our = GetOurRandom(seed);
            System.Random original = GetOriginalRandom(seed);

            for (int i = 0; i < PerSeedSteps; i++)
                Assert.Equal(original.NextInt64(maxValue), our.NextInt64(maxValue));
        }
    }

    /// <summary>
    /// Test for <see cref="Random.NextInt64(long, long)"/>.
    /// </summary>
    /// <param name="minValue">Min value.</param>
    /// <param name="maxValue">Max value.</param>
    [InlineData(0, 10)]
    [InlineData(-10, 10)]
    [InlineData(-1000, 1000)]
    [InlineData(long.MinValue, 0)]
    [InlineData(0, long.MaxValue)]
    [InlineData(long.MinValue, long.MaxValue)]
    [InlineData(long.MinValue/2, long.MaxValue / 2)]
    [Theory]
    public void NextInt64MinMax(long minValue, long maxValue)
    {
        foreach (Seed seed in GetSeedSequence())
        {
            Random our = GetOurRandom(seed);
            System.Random original = GetOriginalRandom(seed);

            for (int i = 0; i < PerSeedSteps; i++)
                Assert.Equal(original.NextInt64(minValue, maxValue), our.NextInt64(minValue, maxValue));
        }
    }

    /// <summary>
    /// Test for <see cref="Random.NextFixed()"/>.
    /// </summary>
    [Fact]
    public void FixedInRange()
    {
        foreach (Seed seed in GetSeedSequence())
        {
            Random our = GetOurRandom(seed);
            Fixed zero = new(0);
            Fixed one = new(1);

            for (int i = 0; i < PerSeedSteps; i++)
            {
                Fixed value = our.NextFixed();
                Assert.True(value >= zero);
                Assert.True(value < one);
            }
        }
    }
}
