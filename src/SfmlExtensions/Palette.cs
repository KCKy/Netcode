using System;
using SFML.System;

namespace SfmlExtensions;

/// <summary>
/// A procedural cosine palette.
/// </summary>
/// <remarks>
/// Used formula: <code>A+B*cos(tau*(C*t+D))</code>.
/// Adapted from https://iquilezles.org/articles/palettes/.
/// </remarks>
/// <param name="A">A</param>
/// <param name="B">B</param>
/// <param name="C">C</param>
/// <param name="D">D</param>
public readonly record struct Palette(Vector3f A, Vector3f B, Vector3f C, Vector3f D)
{
    static float Formula(float a, float b, float c, float d, float t)
    {
        return a + b * MathF.Cos(MathF.Tau * (c * t + d));
    }

    /// <summary>
    ///     Sample the palette.
    /// </summary>
    /// <param name="t">The point to sample.</param>
    /// <returns>A vector of RGB values.</returns>
    public Vector3f this[float t]
    {
        get
        {
            if (t != 1f)
                t = MathF.IEEERemainder(MathF.Abs(t), 1);

            float r = Formula(A.X, B.X, C.X, D.X, t);
            float g = Formula(A.Y, B.Y, C.Y, D.Y, t);
            float b = Formula(A.Z, B.Z, C.Z, D.Z, t);
            return new(r, g, b);
        }
    }
}
