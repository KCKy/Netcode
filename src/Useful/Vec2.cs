using System.Numerics;

namespace Useful;

/// <summary>
/// Two-dimensional vector over a generic field.
/// </summary>
/// <typeparam name="T">The field the vector is build over e.g. the type of both of the components.</typeparam>
public struct Vec2<T> :
        IAdditionOperators<Vec2<T>, Vec2<T>, Vec2<T>>,
        ISubtractionOperators<Vec2<T>, Vec2<T>, Vec2<T>>,
        IUnaryPlusOperators<Vec2<T>, Vec2<T>>,
        IUnaryNegationOperators<Vec2<T>, Vec2<T>>
    where T : unmanaged,
        IAdditionOperators<T, T, T>,
        ISubtractionOperators<T, T, T>,
        IUnaryPlusOperators<T, T>,
        IUnaryNegationOperators<T, T>,
        IMultiplyOperators<T, T, T>,
        IDivisionOperators<T, T, T>
{
    /// <summary>
    /// The value for the x-axis (first dimension).
    /// </summary>
    public T X;
    
    /// <summary>
    /// The value for the y-axis (second dimension).
    /// </summary>
    public T Y;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">The value for the x-axis (first dimension).</param>
    /// <param name="y">The value for the y-axis (second dimension).</param>
    public Vec2(T x, T y)
    {
        X = x;
        Y = y;
    }

    /// <inheritdoc/>
    public readonly override string ToString() => $"({X},{Y})";

    /// <inheritdoc/>
    public static Vec2<T> operator +(Vec2<T> left, Vec2<T> right) => new(left.X + right.X, left.Y + right.Y);

    /// <inheritdoc/>
    public static Vec2<T> operator -(Vec2<T> left, Vec2<T> right) => new(left.X - right.X, left.Y - right.Y);
    
    /// <inheritdoc/>
    public static Vec2<T> operator -(Vec2<T> value) => new(-value.X, -value.Y);

    /// <inheritdoc/>
    public static Vec2<T> operator +(Vec2<T> value) => new(+value.X, +value.Y);

    /// <summary>
    /// Multiply a vector with a scalar.
    /// </summary>
    /// <param name="left">The vector to multiply.</param>
    /// <param name="right">The scalar to multiply with.</param>
    /// <returns>Vector with elements multiplied by given scalar.</returns>
    public static Vec2<T> operator *(Vec2<T> left, T right) => new(left.X * right, left.Y * right);
    
    /// <summary>
    /// Multiply a vector with a scalar.
    /// </summary>
    /// <param name="right">The vector to multiply.</param>
    /// <param name="left">The scalar to multiply with.</param>
    /// <returns>Vector with elements multiplied by given scalar.</returns>
    public static Vec2<T> operator *(T left, Vec2<T> right) => right * left;

    /// <summary>
    /// Divide a vector with a scalar.
    /// </summary>
    /// <param name="left">The vector to multiply.</param>
    /// <param name="right">The scalar to multiply with.</param>
    /// <returns>Vector with elements divided by given scalar.</returns>;
    public static Vec2<T> operator /(Vec2<T> left, T right) => new(left.X / right, left.Y / right);

    /// <summary>
    /// The magnitude of the vector raised to the second power.
    /// </summary>
    public readonly T SquaredMagnitude => X * X + Y * Y;

    /// <summary>
    /// The dot product.
    /// </summary>
    /// <param name="right">The vector to multiply with.</param>
    /// <returns></returns>
    public readonly T Dot(Vec2<T> right) => X * right.X + Y * right.Y;

    /// <summary>
    /// Project a vector into a one-dimensional subspace.
    /// </summary>
    /// <param name="subspace">A vector generating desired subspace.</param>
    /// <remarks><paramref name="subspace"/> shall not be zero.</remarks>
    /// <returns>Vector projected onto the subspace.</returns>
    public readonly Vec2<T> Project(Vec2<T> subspace) => Dot(subspace) / subspace.SquaredMagnitude * subspace;
}
