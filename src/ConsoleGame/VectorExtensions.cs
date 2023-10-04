using SFML.Graphics;
using SFML.System;

namespace TestGame;

static class VectorExtensions
{
    public static Color ToColor(this Vector3f v) => new (ToByte(v.X), ToByte(v.Y), ToByte(v.Z));

    static byte ToByte(float value)
    {
        if (value >= 1f)
            return byte.MaxValue;
        if (value <= 0f)
            return byte.MinValue;
        return (byte)(value * byte.MaxValue);
    }

    public static void Deconstruct(this Vector2i v, out int x, out int y)
    {
        x = v.X;
        y = v.Y;
    }
}
