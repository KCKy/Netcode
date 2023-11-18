using System.Numerics;

namespace Useful;

public struct Vec2<T> where T : unmanaged, IBinaryNumber<T>
{
    public T X;
    public T Y;

    public Vec2(T x, T y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X.ToString()},{Y.ToString()})";

    public static Vec2<T> operator +(Vec2<T> left, Vec2<T> right) => new(left.X + right.X, left.Y + right.Y);
    public static Vec2<T> operator -(Vec2<T> left, Vec2<T> right) => new(left.X - right.X, left.Y - right.Y);
        
    public static Vec2<T> operator -(Vec2<T> value) => new(-value.X, -value.Y);
    public static Vec2<T> operator +(Vec2<T> value) => value;


    public static Vec2<T> operator *(Vec2<T> left, T right) => new(left.X * right, left.Y * right);
    public static Vec2<T> operator /(Vec2<T> left, T right) => new(left.X / right, left.Y / right);

    public static Vec2<T> operator *(T left, Vec2<T> right) => right * left;
    public static Vec2<T> operator /(T left, Vec2<T> right) => right / left;

    public readonly T SquaredMagnitude => X * X + Y * Y;

    public readonly T Dot(Vec2<T> right) => X * right.X + Y * right.Y;

    public readonly Vec2<T> Project(Vec2<T> subspace) => Dot(subspace) / subspace.SquaredMagnitude * subspace;
}
