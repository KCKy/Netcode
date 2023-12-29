using System.Numerics;
using SFML.Graphics;
using SFML.System;

namespace SfmlExtensions;

public static class VectorExtensions
{
    public static Color ToColor(this Vector3 v) => new (ToByte(v.X), ToByte(v.Y), ToByte(v.Z));
    public static Color ToColor(this Vector3f v) => new (ToByte(v.X), ToByte(v.Y), ToByte(v.Z));

    public static Vector2 Lerp(this Vector2 from, Vector2 to, float t) => from + (to - from) * t;
    public static Vector3 Lerp(this Vector3 from, Vector3 to, float t) => from + (to - from) * t;
    public static Vector2f Lerp(this Vector2f from, Vector2f to, float t) => from + (to - from) * t;
    public static Vector3f Lerp(this Vector3f from, Vector3f to, float t) => from + (to - from) * t;

    static byte ToByte(float value)
    {
        return value switch
        {
            >= 1f => byte.MaxValue,
            <= 0f => byte.MinValue,
            _ => (byte)(value * byte.MaxValue)
        };
    }

    public static void Deconstruct(this Vector2i v, out int x, out int y)
    {
        x = v.X;
        y = v.Y;
    }
}
