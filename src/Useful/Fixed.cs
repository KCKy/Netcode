using System.Diagnostics;
using System.Numerics;

namespace Useful;

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
    IComparisonOperators<Fixed,Fixed,bool>
{
    public override string ToString() => ((float)this).ToString();

    public static implicit operator float(Fixed value) => value.value_ * ToFloat;

    const int FractionalPart = 32;
    const long OneValue = 1L << FractionalPart;
    const float ToFloat = 1f / OneValue;

    readonly long value_;

    public Fixed(int value) => value_ = (long)value << FractionalPart;
    public Fixed(long binary) => value_ = binary;

    public static implicit operator Fixed(int value) => new(value);
    
    public static Fixed AdditiveIdentity => 0;
    public static Fixed MultiplicativeIdentity => new(OneValue);

    public static bool operator ==(Fixed left, Fixed right) => left.value_ == right.value_;
    public static bool operator !=(Fixed left, Fixed right) => left.value_ != right.value_;
    public static bool operator >(Fixed left, Fixed right) => left.value_ > right.value_;
    public static bool operator >=(Fixed left, Fixed right) => left.value_ >= right.value_;
    public static bool operator <(Fixed left, Fixed right) => left.value_ < right.value_;
    public static bool operator <=(Fixed left, Fixed right) => left.value_ <= right.value_;

    public static Fixed operator --(Fixed value) => new(value.value_ - OneValue);
    public static Fixed operator ++(Fixed value) => new(value.value_ + OneValue);

    public static Fixed MaxValue => new(long.MaxValue);
    public static Fixed MinValue => new(long.MinValue);

    public static Fixed operator +(Fixed left, Fixed right) => new(left.value_ + right.value_);
    public static Fixed operator -(Fixed left, Fixed right) => new(left.value_ - right.value_);

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

    public static Fixed operator /(Fixed left, Fixed right) => throw new NotImplementedException(); // TODO: implement fixed point division
    
    public static Fixed operator -(Fixed value) => new(-value.value_);
    public static Fixed operator +(Fixed value) => value;
    public static Fixed Abs(Fixed value) => new(Math.Abs(value.value_));

    public bool Equals(Fixed other) => value_ == other.value_;

    public int CompareTo(Fixed other) => value_.CompareTo(other.value_);

    public override int GetHashCode() => value_.GetHashCode();

    public override bool Equals(object? obj)
    {
        if (obj is Fixed other)
            return Equals(other);
        return false;
    }

    public int CompareTo(object? obj)
    {
        if (obj is not Fixed other)
            throw new ArgumentException("Invalid type.", nameof(obj));
        return CompareTo(other);
    }
}
