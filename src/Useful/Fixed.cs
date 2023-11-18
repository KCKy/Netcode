using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Useful;

public readonly struct Fixed : IBinaryNumber<Fixed>
{
    public override string ToString() => ((float)this).ToString();

    public static implicit operator float(Fixed value) => value.value_ * ToFloat;

    public static bool IsPow2(Fixed value) => throw new NotImplementedException();
    public static Fixed Log2(Fixed value) => throw new NotImplementedException();
    public static bool TryConvertFromChecked<TOther>(TOther value, out Fixed result) where TOther : INumberBase<TOther> => throw new NotImplementedException();
    public static bool TryConvertFromSaturating<TOther>(TOther value, out Fixed result) where TOther : INumberBase<TOther> => throw new NotImplementedException();
    public static bool TryConvertFromTruncating<TOther>(TOther value, out Fixed result) where TOther : INumberBase<TOther> => throw new NotImplementedException();
    public static bool TryConvertToChecked<TOther>(Fixed value, out TOther result) where TOther : INumberBase<TOther> => throw new NotImplementedException();
    public static bool TryConvertToSaturating<TOther>(Fixed value, out TOther result) where TOther : INumberBase<TOther> => throw new NotImplementedException();
    public static bool TryConvertToTruncating<TOther>(Fixed value, out TOther result) where TOther : INumberBase<TOther> => throw new NotImplementedException();
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Fixed result) => throw new NotImplementedException();
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider, out Fixed result) => throw new NotImplementedException();
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
    public static Fixed Parse(string s, IFormatProvider? provider) => throw new NotImplementedException();
    public static bool TryParse(string? s, IFormatProvider? provider, out Fixed result) => throw new NotImplementedException();
    public static Fixed Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => throw new NotImplementedException();
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Fixed result) => throw new NotImplementedException();
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => throw new NotImplementedException();

    const int FractionalPart = 32;
    const long OneValue = 1L << FractionalPart;
    const float ToFloat = 1f / OneValue;

    readonly long value_;

    public Fixed(int value) => value_ = (long)value << FractionalPart;
    public Fixed(long binary) => value_ = binary;
    public static implicit operator Fixed(int value) => new (value);
    public static Fixed operator +(Fixed left, Fixed right) => new(left.value_ + right.value_);
    public static Fixed AdditiveIdentity => 0;
    public static bool operator ==(Fixed left, Fixed right) => left.value_ == right.value_;
    public static bool operator !=(Fixed left, Fixed right) => left.value_ != right.value_;
    public static bool operator >(Fixed left, Fixed right) => left.value_ > right.value_;
    public static bool operator >=(Fixed left, Fixed right) => left.value_ >= right.value_;
    public static bool operator <(Fixed left, Fixed right) => left.value_ < right.value_;
    public static bool operator <=(Fixed left, Fixed right) => left.value_ <= right.value_;
    public static Fixed operator --(Fixed value) => new(value.value_ - OneValue);
    public static Fixed operator ++(Fixed value) => new(value.value_ + OneValue);
    public static Fixed MultiplicativeIdentity => new(OneValue);

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

    public static Fixed operator /(Fixed left, Fixed right) => throw new NotImplementedException();

    public static Fixed operator -(Fixed left, Fixed right) => new(left.value_ - right.value_);
    public static Fixed operator -(Fixed value) => new(-value.value_);
    public static Fixed operator +(Fixed value) => value;
    public static Fixed operator &(Fixed left, Fixed right) => new(left.value_ & right.value_);
    public static Fixed operator |(Fixed left, Fixed right) => new(left.value_ | right.value_);
    public static Fixed operator ^(Fixed left, Fixed right) => new(left.value_ ^ right.value_);
    public static Fixed operator ~(Fixed value) => new(~value.value_);
    public static Fixed Abs(Fixed value) => new(Math.Abs(value.value_));
    public static bool IsCanonical(Fixed value) => true;
    public static bool IsComplexNumber(Fixed value) => false;
    public static bool IsEvenInteger(Fixed value) => (value.value_ >> FractionalPart) % 2 == 0;
    public static bool IsFinite(Fixed value) => true;
    public static bool IsImaginaryNumber(Fixed value) => false;
    public static bool IsInfinity(Fixed value) => false;
    public static bool IsInteger(Fixed value) => (value.value_ & ((1L << FractionalPart) - 1L)) != 0;
    public static bool IsNaN(Fixed value) => false;
    public static bool IsNegative(Fixed value) => value.value_ < 0;
    public static bool IsNegativeInfinity(Fixed value) => false;
    public static bool IsNormal(Fixed value) => true;
    public static bool IsOddInteger(Fixed value) => !IsInteger(value);
    public static bool IsPositive(Fixed value) => value.value_ < 0;
    public static bool IsPositiveInfinity(Fixed value) => false;
    public static bool IsRealNumber(Fixed value) => true;
    public static bool IsSubnormal(Fixed value) => false;
    public static bool IsZero(Fixed value) => value.value_ == 0;
    public static Fixed MaxMagnitude(Fixed x, Fixed y) => x < y ? x : y;
    public static Fixed MaxMagnitudeNumber(Fixed x, Fixed y) => MaxMagnitude(x, y);
    public static Fixed MinMagnitude(Fixed x, Fixed y) => x > y ? x : y;
    public static Fixed MinMagnitudeNumber(Fixed x, Fixed y) => MinMagnitude(x, y);
    public static Fixed Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => throw new NotImplementedException();
    public static Fixed Parse(string s, NumberStyles style, IFormatProvider? provider) => throw new NotImplementedException();
    public static Fixed One => new(OneValue);
    public static int Radix => 2;
    public static Fixed Zero => new(0L);
    public bool Equals(Fixed other) => value_ == other.value_;
    public int CompareTo(Fixed other) => value_.CompareTo(other.value_);
    public static Fixed operator %(Fixed left, Fixed right) => throw new NotImplementedException();
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
