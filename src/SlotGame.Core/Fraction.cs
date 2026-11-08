using System.Globalization;
using System.Numerics;

namespace SlotGame.Core;

/// <summary>
/// Exact rational number backed by <see cref="BigInteger"/>. Always stored in lowest terms with a positive
/// denominator, so equality is structural. Used by <see cref="RationalAnalyzer"/> to state RTP as an exact fraction.
/// </summary>
public readonly struct Fraction : IEquatable<Fraction>, IComparable<Fraction>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public static readonly Fraction Zero = new(0);
    public static readonly Fraction One = new(1);

    public Fraction(BigInteger numerator) : this(numerator, BigInteger.One) { }

    public Fraction(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new DivideByZeroException("Fraction denominator cannot be zero.");
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        if (!gcd.IsOne && !gcd.IsZero) { numerator /= gcd; denominator /= gcd; }
        Numerator = numerator;
        Denominator = numerator.IsZero ? BigInteger.One : denominator;
    }

    /// <summary>Converts a decimal literal (for example a paytable entry of 0.07) without binary rounding.</summary>
    public static Fraction FromDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 0xFF;
        var magnitude = new BigInteger((uint)bits[0])
            | (new BigInteger((uint)bits[1]) << 32)
            | (new BigInteger((uint)bits[2]) << 64);
        if (bits[3] < 0) magnitude = -magnitude;
        return new Fraction(magnitude, BigInteger.Pow(10, scale));
    }

    public static Fraction operator +(Fraction a, Fraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
    public static Fraction operator -(Fraction a, Fraction b) =>
        new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
    public static Fraction operator -(Fraction a) => new(-a.Numerator, a.Denominator);
    public static Fraction operator *(Fraction a, Fraction b) =>
        new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
    public static Fraction operator /(Fraction a, Fraction b) =>
        new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    public static implicit operator Fraction(int value) => new(value);
    public static implicit operator Fraction(long value) => new(value);

    public static bool operator ==(Fraction a, Fraction b) => a.Equals(b);
    public static bool operator !=(Fraction a, Fraction b) => !a.Equals(b);

    public bool IsZero => Numerator.IsZero;

    public double ToDouble()
    {
        // Scale before dividing so very large numerators/denominators keep full double precision.
        var shift = Math.Max(0, (int)(BigInteger.Abs(Denominator).GetBitLength() - BigInteger.Abs(Numerator).GetBitLength()) + 64);
        var scaled = (Numerator << shift) / Denominator;
        return (double)scaled / Math.Pow(2, shift);
    }

    /// <summary>Decimal expansion truncated (not rounded) to <paramref name="digits"/> places.</summary>
    public string ToDecimalString(int digits)
    {
        var sign = Numerator.Sign < 0 ? "-" : "";
        var abs = BigInteger.Abs(Numerator);
        var whole = BigInteger.DivRem(abs, Denominator, out var remainder);
        var fraction = remainder * BigInteger.Pow(10, digits) / Denominator;
        return $"{sign}{whole}.{fraction.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')}";
    }

    public bool Equals(Fraction other) => Numerator == other.Numerator && Denominator == other.Denominator;
    public override bool Equals(object? obj) => obj is Fraction other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
    public int CompareTo(Fraction other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
    public override string ToString() => Denominator.IsOne ? Numerator.ToString() : $"{Numerator}/{Denominator}";
}
