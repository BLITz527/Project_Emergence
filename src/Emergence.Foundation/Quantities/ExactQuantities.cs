using System.Globalization;

namespace Emergence.Foundation.Quantities;

public readonly record struct MatterAmount(ulong Quanta) : IComparable<MatterAmount>
{
    public static MatterAmount Parse(string text) => new(QuantityText.Parse(text, nameof(MatterAmount)));
    public static bool TryParse(string? text, out MatterAmount value) { bool ok = QuantityText.TryParse(text, out ulong parsed); value = new(parsed); return ok; }
    public static MatterAmount operator +(MatterAmount left, MatterAmount right) => new(checked(left.Quanta + right.Quanta));
    public static MatterAmount operator -(MatterAmount left, MatterAmount right) => left.Quanta >= right.Quanta ? new(left.Quanta - right.Quanta) : throw new OverflowException("Matter subtraction underflowed.");
    public static MatterAmount operator *(MatterAmount amount, ulong multiplier) => new(QuantityMath.Multiply(amount.Quanta, multiplier));
    public static MatterAmount operator *(ulong multiplier, MatterAmount amount) => amount * multiplier;
    public bool TrySubtract(MatterAmount amount, out MatterAmount result) { if (Quanta < amount.Quanta) { result = default; return false; } result = new(Quanta - amount.Quanta); return true; }
    public int CompareTo(MatterAmount other) => Quanta.CompareTo(other.Quanta);
    public override string ToString() => Quanta.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct EnergyAmount(ulong Quanta) : IComparable<EnergyAmount>
{
    public static EnergyAmount Parse(string text) => new(QuantityText.Parse(text, nameof(EnergyAmount)));
    public static bool TryParse(string? text, out EnergyAmount value) { bool ok = QuantityText.TryParse(text, out ulong parsed); value = new(parsed); return ok; }
    public static EnergyAmount operator +(EnergyAmount left, EnergyAmount right) => new(checked(left.Quanta + right.Quanta));
    public static EnergyAmount operator -(EnergyAmount left, EnergyAmount right) => left.Quanta >= right.Quanta ? new(left.Quanta - right.Quanta) : throw new OverflowException("Energy subtraction underflowed.");
    public static EnergyAmount operator *(EnergyAmount amount, ulong multiplier) => new(QuantityMath.Multiply(amount.Quanta, multiplier));
    public static EnergyAmount operator *(ulong multiplier, EnergyAmount amount) => amount * multiplier;
    public bool TrySubtract(EnergyAmount amount, out EnergyAmount result) { if (Quanta < amount.Quanta) { result = default; return false; } result = new(Quanta - amount.Quanta); return true; }
    public int CompareTo(EnergyAmount other) => Quanta.CompareTo(other.Quanta);
    public override string ToString() => Quanta.ToString(CultureInfo.InvariantCulture);
}

internal static class QuantityMath
{
    public static ulong Multiply(ulong value, ulong multiplier)
    {
        UInt128 product = (UInt128)value * multiplier;
        return product <= ulong.MaxValue ? (ulong)product : throw new OverflowException("Exact quantity multiplication overflowed UInt64.");
    }
}

internal static class QuantityText
{
    public static ulong Parse(string text, string typeName)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out ulong value) ? value : throw new FormatException($"{typeName} requires a canonical unsigned decimal string.");
    }
    public static bool TryParse(string? text, out ulong value)
    {
        value = default;
        if (string.IsNullOrEmpty(text) || (text.Length > 1 && text[0] == '0')) return false;
        return text.All(static character => character is >= '0' and <= '9')
            && ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
