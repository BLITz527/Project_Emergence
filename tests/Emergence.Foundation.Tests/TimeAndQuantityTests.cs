using System.Globalization;
using System.Text.Json;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Time;

namespace Emergence.Foundation.Tests;

public sealed class TimeAndQuantityTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("340282366920938463463374607431768211455")]
    public void TimeValuesRoundTrip(string text)
    {
        SimulationTick tick = SimulationTick.Parse(text); TickSpan span = TickSpan.Parse(text); SequenceNumber sequence = SequenceNumber.Parse(text);
        Assert.Equal($"\"{text}\"", JsonDefaults.Serialize(tick, false));
        Assert.Equal(tick, JsonSerializer.Deserialize<SimulationTick>(JsonDefaults.Serialize(tick, false), JsonDefaults.Compact));
        Assert.Equal(span, JsonSerializer.Deserialize<TickSpan>(JsonDefaults.Serialize(span, false), JsonDefaults.Compact));
        Assert.Equal(sequence, JsonSerializer.Deserialize<SequenceNumber>(JsonDefaults.Serialize(sequence, false), JsonDefaults.Compact));
    }

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("340282366920938463463374607431768211456")]
    public void TimeRejectsNoncanonicalOrOverflow(string text) => Assert.False(SimulationTick.TryParse(text, out _));

    [Fact] public void TickAdditionOverflows() => Assert.Throws<OverflowException>(() => _ = SimulationTick.MaxValue + new TickSpan(1));
    [Fact] public void SpanAdditionOverflows() => Assert.Throws<OverflowException>(() => _ = TickSpan.MaxValue + new TickSpan(1));
    [Fact] public void SpanMultiplicationOverflows() => Assert.Throws<OverflowException>(() => _ = new TickSpan(UInt128.MaxValue) * 2);
    [Fact] public void TickSubtractionRejectsNegative() => Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SimulationTick(1) - new SimulationTick(2));
    [Fact] public void TickSubtractionProducesSpan() => Assert.Equal(new TickSpan(7), new SimulationTick(9) - new SimulationTick(2));

    [Fact]
    public void CounterStartsAtZeroAndIssuesOne()
    {
        CheckedSequenceCounter counter = new();
        Assert.Equal(new SequenceNumber(0), counter.LastIssued);
        Assert.Equal(new SequenceNumber(1), counter.IssueNext());
    }

    [Fact]
    public void CounterExhaustionNeverWraps()
    {
        CheckedSequenceCounter counter = new(new SequenceNumber(UInt128.MaxValue - 1));
        Assert.Equal(SequenceNumber.MaxValue, counter.IssueNext());
        Assert.False(counter.TryIssueNext(out _));
        Assert.Equal(SequenceNumber.MaxValue, counter.LastIssued);
        Assert.Throws<OverflowException>(() => counter.IssueNext());
    }

    [Fact]
    public void CounterStateRoundTrips()
    {
        CheckedSequenceCounter counter = new(new SequenceNumber(42));
        string json = JsonDefaults.Serialize(counter, false);
        CheckedSequenceCounter restored = JsonSerializer.Deserialize<CheckedSequenceCounter>(json, JsonDefaults.Compact)!;
        Assert.Equal(counter.LastIssued, restored.LastIssued);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(18446744073709551615UL)]
    public void QuantitiesRoundTrip(ulong value)
    {
        MatterAmount matter = new(value); EnergyAmount energy = new(value);
        Assert.Equal(matter, JsonSerializer.Deserialize<MatterAmount>(JsonDefaults.Serialize(matter, false), JsonDefaults.Compact));
        Assert.Equal(energy, JsonSerializer.Deserialize<EnergyAmount>(JsonDefaults.Serialize(energy, false), JsonDefaults.Compact));
    }

    [Fact] public void QuantityAdditionDetectsOverflow() => Assert.Throws<OverflowException>(() => _ = new MatterAmount(ulong.MaxValue) + new MatterAmount(1));
    [Fact] public void QuantityMultiplicationDetectsOverflow() => Assert.Throws<OverflowException>(() => _ = new EnergyAmount(ulong.MaxValue) * 2);
    [Fact] public void QuantitySubtractionNeverUnderflows() => Assert.Throws<OverflowException>(() => _ = new MatterAmount(1) - new MatterAmount(2));
    [Fact] public void TrySubtractFailureReturnsZeroResult() { MatterAmount original = new(1); Assert.False(original.TrySubtract(new MatterAmount(2), out MatterAmount result)); Assert.Equal(default, result); Assert.Equal(new MatterAmount(1), original); }

    [Fact]
    public void DeterministicallySeededQuantityValuesRoundTrip()
    {
        Random random = new(527); byte[] bytes = new byte[8];
        for (int index = 0; index < 100; index++) { random.NextBytes(bytes); ulong raw = BitConverter.ToUInt64(bytes); MatterAmount value = new(raw); Assert.Equal(value, JsonSerializer.Deserialize<MatterAmount>(JsonDefaults.Serialize(value, false), JsonDefaults.Compact)); }
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void QuantityJsonIsCultureInvariant(string culture)
    {
        CultureInfo prior = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); Assert.Equal("\"18446744073709551615\"", JsonDefaults.Serialize(new MatterAmount(ulong.MaxValue), false)); }
        finally { CultureInfo.CurrentCulture = prior; }
    }
}
