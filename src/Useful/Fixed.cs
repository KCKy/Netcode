using System;
using System.Diagnostics;
using System.Numerics;

namespace Useful;

/// <summary>
/// 64 bit fixed point number with 32 bits for the fractional part.
/// </summary>
public readonly struct Fixed :
    IAdditionOperators<Fixed, Fixed, Fixed>,
    ISubtractionOperators<Fixed, Fixed, Fixed>,
    IUnaryPlusOperators<Fixed, Fixed>,
    IUnaryNegationOperators<Fixed, Fixed>,
    IMultiplyOperators<Fixed, Fixed, Fixed>,
    IDivisionOperators<Fixed, Fixed, Fixed>,
    IIncrementOperators<Fixed>,
    IDecrementOperators<Fixed>,
    IMultiplicativeIdentity<Fixed, Fixed>,
    IAdditiveIdentity<Fixed, Fixed>,
    IMinMaxValue<Fixed>,
    IComparisonOperators<Fixed, Fixed, bool>
{
    /// <inheritdoc/>
    public override string ToString() => ((float)this).ToString();

    /// <summary>
    /// Implicit convert to a floating point approximation.
    /// </summary>
    /// <param name="value">Value for conversion.</param>
    public static implicit operator float(Fixed value) => value.value_ * ToFloat;

    const int FractionalPart = 32; // Number of bits for the fractional part
    const long OneValue = 1L << FractionalPart;
    const float ToFloat = 1f / OneValue;

    readonly long value_;

    /// <summary>
    /// Create a fixed number from an integer.
    /// </summary>
    /// <param name="value">An integer.</param>
    public Fixed(int value) => value_ = (long)value << FractionalPart;
    
    /// <summary>
    /// Create a fixed number with given binary representation.
    /// </summary>
    /// <param name="binary">Two's complement binary representation.</param>
    /// <returns>Corresponding fixed number.</returns>
    public static Fixed FromBinary(long binary) => new(binary);
    Fixed(long binary) => value_ = binary;

    /// <summary>
    /// Implicit conversion from an integer.
    /// </summary>
    /// <param name="value">An integer.</param>
    public static implicit operator Fixed(int value) => new(value);
    
    /// <inheritdoc/>
    public static Fixed AdditiveIdentity => 0;
    
    /// <inheritdoc/>
    public static Fixed MultiplicativeIdentity => new(OneValue);

    /// <inheritdoc/>
    public static bool operator ==(Fixed left, Fixed right) => left.value_ == right.value_;

    /// <inheritdoc/>
    public static bool operator !=(Fixed left, Fixed right) => left.value_ != right.value_;

    /// <inheritdoc/>
    public static bool operator >(Fixed left, Fixed right) => left.value_ > right.value_;

    /// <inheritdoc/>
    public static bool operator >=(Fixed left, Fixed right) => left.value_ >= right.value_;

    /// <inheritdoc/>
    public static bool operator <(Fixed left, Fixed right) => left.value_ < right.value_;

    /// <inheritdoc/>
    public static bool operator <=(Fixed left, Fixed right) => left.value_ <= right.value_;

    /// <inheritdoc/>
    public static Fixed operator --(Fixed value) => new(value.value_ - OneValue);

    /// <inheritdoc/>
    public static Fixed operator ++(Fixed value) => new(value.value_ + OneValue);

    /// <inheritdoc/>
    public static Fixed MaxValue => new(long.MaxValue);

    /// <inheritdoc/>
    public static Fixed MinValue => new(long.MinValue);

    /// <inheritdoc/>
    public static Fixed operator +(Fixed left, Fixed right) => new(left.value_ + right.value_);

    /// <inheritdoc/>
    public static Fixed operator -(Fixed left, Fixed right) => new(left.value_ - right.value_);

    /// <inheritdoc/>
    public static Fixed operator *(Fixed left, Fixed right)
    {
        Debug.Assert(FractionalPart == 32);

        // (Lup + Ldw)*(Rup + Rdw) = Lup * Rup + Ldw * Rdw + Lup * Rdw + Ldw * Rdw

        long l = left.value_;
        long r = right.value_;

        const long intMask = (1L << 32) - 1L;

        long lup = l >> 32;
        long rup = r >> 32;
        long ldw = l & intMask;
        long rdw = r & intMask;

        long value = (lup * rup) << 32;
        value += lup * rdw;
        value += ldw * rup;
        value += (ldw * rdw) >> 32;

        return new(value);
    }

    /// <summary>
    /// Approximation of the reciprocal of given fixed point value.
    /// </summary>
    /// <remarks>
    /// For value close to zero like 2^-32, where the reciprocal cannot be represented, an incorrect value may be returned or an <see cref="OverflowException"/> may be thrown.
    /// </remarks>
    public Fixed Reciprocal =>
        // SOURCE: http://www.sunshine2k.de/articles/coding/fp/sunfp.html#ch54
        new(((1L << 63) / -value_) << 1);

    /// <inheritdoc/>
    public static Fixed operator /(Fixed left, Fixed right) => left * right.Reciprocal;

    /// <inheritdoc/>
    public static Fixed operator -(Fixed value) => new(-value.value_);

    /// <inheritdoc/>
    public static Fixed operator +(Fixed value) => value;

    /// <summary>
    /// The absolute value.
    /// </summary>
    /// <param name="value">A fixed point value.</param>
    /// <returns>The absolute value of <paramref name="value"/>.</returns>
    public static Fixed Abs(Fixed value) => new(Math.Abs(value.value_));

    /// <inheritdoc/>
    public override int GetHashCode() => value_.GetHashCode();

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is Fixed other)
            return value_ == other.value_;
        return false;
    }
}
