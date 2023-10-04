using SFML.System;

namespace TestGame;

readonly struct Palette
{
    public required Vector3f A { get; init; }
    public required Vector3f B { get; init; }
    public required Vector3f C { get; init; }
    public required Vector3f D { get; init; }

    public Palette() { }

    float Formula(float a, float b, float c, float d, float t) => a + b * MathF.Cos(MathF.Tau * (c * t + d));

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
