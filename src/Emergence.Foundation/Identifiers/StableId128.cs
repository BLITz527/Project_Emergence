using System.Globalization;

namespace Emergence.Foundation.Identifiers;

public readonly struct StableId128 : IEquatable<StableId128>, IComparable<StableId128>, ISpanFormattable
{
    public StableId128(ulong high, ulong low)
    {
        High = high;
        Low = low;
    }

    public ulong High { get; }
    public ulong Low { get; }
    public bool IsEmpty => High == 0 && Low == 0;

    public static StableId128 FromUInt64(ulong value) => new(0, value);

    public static StableId128 Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text.AsSpan(), out StableId128 value)
            ? value
            : throw new FormatException("A stable identifier must contain exactly 32 hexadecimal characters.");
    }

    public static bool TryParse(string? text, out StableId128 value)
    {
        value = default;
        return text is not null && TryParse(text.AsSpan(), out value);
    }

    public static bool TryParse(ReadOnlySpan<char> text, out StableId128 value)
    {
        value = default;
        if (text.Length != 32
            || !ulong.TryParse(text[..16], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong high)
            || !ulong.TryParse(text[16..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong low))
        {
            return false;
        }

        value = new StableId128(high, low);
        return true;
    }

    public override string ToString() => string.Create(
        32,
        this,
        static (destination, value) => value.TryFormat(destination, out _, default, CultureInfo.InvariantCulture));

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (!string.IsNullOrEmpty(format))
        {
            throw new FormatException("StableId128 does not support format specifiers.");
        }
        return ToString();
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = 0;
        if (!format.IsEmpty || destination.Length < 32
            || !High.TryFormat(destination[..16], out int highWritten, "x16", CultureInfo.InvariantCulture)
            || !Low.TryFormat(destination[16..32], out int lowWritten, "x16", CultureInfo.InvariantCulture)
            || highWritten != 16 || lowWritten != 16)
        {
            return false;
        }
        charsWritten = 32;
        return true;
    }

    public int CompareTo(StableId128 other)
    {
        int high = High.CompareTo(other.High);
        return high != 0 ? high : Low.CompareTo(other.Low);
    }

    public bool Equals(StableId128 other) => High == other.High && Low == other.Low;
    public override bool Equals(object? obj) => obj is StableId128 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(High, Low);
    public static bool operator ==(StableId128 left, StableId128 right) => left.Equals(right);
    public static bool operator !=(StableId128 left, StableId128 right) => !left.Equals(right);
    public static bool operator <(StableId128 left, StableId128 right) => left.CompareTo(right) < 0;
    public static bool operator >(StableId128 left, StableId128 right) => left.CompareTo(right) > 0;
    public static bool operator <=(StableId128 left, StableId128 right) => left.CompareTo(right) <= 0;
    public static bool operator >=(StableId128 left, StableId128 right) => left.CompareTo(right) >= 0;
}
