using System.Globalization;

namespace Emergence.Foundation.Time;

public readonly record struct SimulationTick(UInt128 Value) : IComparable<SimulationTick>
{
    public static SimulationTick MaxValue => new(UInt128.MaxValue);
    public static SimulationTick Parse(string text) => new(Unsigned128Text.Parse(text, nameof(SimulationTick)));
    public static bool TryParse(string? text, out SimulationTick value)
    {
        bool success = Unsigned128Text.TryParse(text, out UInt128 parsed);
        value = new(parsed);
        return success;
    }
    public static SimulationTick operator +(SimulationTick tick, TickSpan span) => new(checked(tick.Value + span.Value));
    public static TickSpan operator -(SimulationTick later, SimulationTick earlier) =>
        later.Value >= earlier.Value ? new(later.Value - earlier.Value) : throw new ArgumentOutOfRangeException(nameof(earlier), "The earlier tick cannot exceed the later tick.");
    public int CompareTo(SimulationTick other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct TickSpan(UInt128 Value) : IComparable<TickSpan>
{
    public static TickSpan MaxValue => new(UInt128.MaxValue);
    public static TickSpan Parse(string text) => new(Unsigned128Text.Parse(text, nameof(TickSpan)));
    public static bool TryParse(string? text, out TickSpan value)
    {
        bool success = Unsigned128Text.TryParse(text, out UInt128 parsed);
        value = new(parsed);
        return success;
    }
    public static TickSpan operator +(TickSpan left, TickSpan right) => new(checked(left.Value + right.Value));
    public static TickSpan operator *(TickSpan span, UInt128 multiplier) => new(checked(span.Value * multiplier));
    public static TickSpan operator *(UInt128 multiplier, TickSpan span) => span * multiplier;
    public int CompareTo(TickSpan other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct SequenceNumber(UInt128 Value) : IComparable<SequenceNumber>
{
    public static SequenceNumber MaxValue => new(UInt128.MaxValue);
    public static SequenceNumber Parse(string text) => new(Unsigned128Text.Parse(text, nameof(SequenceNumber)));
    public static bool TryParse(string? text, out SequenceNumber value)
    {
        bool success = Unsigned128Text.TryParse(text, out UInt128 parsed);
        value = new(parsed);
        return success;
    }
    public int CompareTo(SequenceNumber other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed class CheckedSequenceCounter
{
    public CheckedSequenceCounter() : this(default) { }
    public CheckedSequenceCounter(SequenceNumber lastIssued) => LastIssued = lastIssued;
    public SequenceNumber LastIssued { get; private set; }

    public SequenceNumber IssueNext()
    {
        if (!TryIssueNext(out SequenceNumber issued))
        {
            throw new OverflowException("The sequence counter is exhausted.");
        }
        return issued;
    }

    public bool TryIssueNext(out SequenceNumber issued)
    {
        if (LastIssued.Value == UInt128.MaxValue)
        {
            issued = default;
            return false;
        }
        issued = new SequenceNumber(checked(LastIssued.Value + UInt128.One));
        LastIssued = issued;
        return true;
    }
}

internal static class Unsigned128Text
{
    public static UInt128 Parse(string text, string typeName)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out UInt128 value) ? value : throw new FormatException($"{typeName} requires a canonical unsigned decimal string.");
    }

    public static bool TryParse(string? text, out UInt128 value)
    {
        value = default;
        if (string.IsNullOrEmpty(text) || (text.Length > 1 && text[0] == '0')) return false;
        return text.All(static character => character is >= '0' and <= '9')
            && UInt128.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
