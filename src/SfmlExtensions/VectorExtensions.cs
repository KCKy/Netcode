using System.Numerics;
using SFML.Graphics;
using SFML.System;

namespace SfmlExtensions;

/// <summary>
/// Extensions for vector types.
/// </summary>
public static class VectorExtensions
{
    /// <summary>
    /// Convert given vector into an RGB color.
    /// </summary>
    /// <param name="v">Vector to convert.</param>
    /// <returns>Color corresponding to the vector.</returns>
    public static Color ToColor(this Vector3 v) => new (ToByte(v.X), ToByte(v.Y), ToByte(v.Z));
    
    /// <summary>
    /// Convert given vector into an RGB color.
    /// </summary>
    /// <param name="v">Vector to convert.</param>
    /// <returns>Color corresponding to the vector.</returns>
    public static Color ToColor(this Vector3f v) => new (ToByte(v.X), ToByte(v.Y), ToByte(v.Z));

    /// <summary>
    /// Linear interpolation (lerp).
    /// </summary>
    /// <param name="from">The source vector.</param>
    /// <param name="to">The destination vector.</param>
    /// <param name="t">Normalized weight of the second vector.</param>
    /// <returns>An interpolated vector.</returns>
    public static Vector2 Lerp(this Vector2 from, Vector2 to, float t) => from + (to - from) * t;

    /// <summary>
    /// Linear interpolation (lerp).
    /// </summary>
    /// <param name="from">The source vector.</param>
    /// <param name="to">The destination vector.</param>
    /// <param name="t">Normalized weight of the second vector.</param>
    /// <returns>An interpolated vector.</returns>
    public static Vector3 Lerp(this Vector3 from, Vector3 to, float t) => from + (to - from) * t;

    /// <summary>
    /// Linear interpolation (lerp).
    /// </summary>
    /// <param name="from">The source vector.</param>
    /// <param name="to">The destination vector.</param>
    /// <param name="t">Normalized weight of the second vector.</param>
    /// <returns>An interpolated vector.</returns>
    public static Vector2f Lerp(this Vector2f from, Vector2f to, float t) => from + (to - from) * t;

    /// <summary>
    /// Linear interpolation (lerp).
    /// </summary>
    /// <param name="from">The source vector.</param>
    /// <param name="to">The destination vector.</param>
    /// <param name="t">Normalized weight of the second vector.</param>
    /// <returns>An interpolated vector.</returns>
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
}
