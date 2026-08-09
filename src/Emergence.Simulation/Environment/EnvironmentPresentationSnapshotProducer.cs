using System.Globalization;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Quantities;
using Emergence.Model.Environment;
using Emergence.Presentation.Contracts;

namespace Emergence.Simulation.Fields;

/// <summary>Creates replaceable rendering data without advancing or mutating authoritative state.</summary>
public sealed class EnvironmentPresentationSnapshotProducer
{
    public EnvironmentPresentationSnapshot Create(WorldSession session, FieldChannelId selectedChannel)
    {
        ArgumentNullException.ThrowIfNull(session);
        WorldEnvironmentState environment = session.EnvironmentState
            ?? throw new ArgumentException("An environment presentation snapshot requires a V3 session.", nameof(session));
        RegionFieldState region = environment.Regions.Single();
        if (!region.Definition.FieldChannels.TryGetSlot(selectedChannel, out int slot))
            throw new ArgumentOutOfRangeException(nameof(selectedChannel));

        double[] normalized = new double[region.Definition.CellCount];
        bool[] solid = new bool[region.Definition.CellCount];
        ulong minimum = ulong.MaxValue;
        ulong maximum = 0;
        for (int index = 0; index < region.Definition.CellCount; index++)
        {
            bool isSolid = region.Definition.EffectiveVolumes[index].Quanta == 0;
            solid[index] = isSolid;
            if (isSolid) continue;
            ulong amount = region.GetAmount(slot, index).Quanta;
            minimum = Math.Min(minimum, amount);
            maximum = Math.Max(maximum, amount);
        }
        if (minimum == ulong.MaxValue) throw new InvalidOperationException("Environment definition has no fluid cells.");
        for (int index = 0; index < region.Definition.CellCount; index++)
        {
            if (solid[index]) { normalized[index] = 0d; continue; }
            ulong amount = region.GetAmount(slot, index).Quanta;
            normalized[index] = minimum == maximum ? 0.5d : (amount - minimum) / (double)(maximum - minimum);
        }
        return new(
            region.RegionId,
            region.Definition.Digest,
            region.Digest,
            environment.Digest,
            region.Definition.Width,
            region.Definition.Height,
            selectedChannel,
            region.GetChannelTotal(slot).ToString(CultureInfo.InvariantCulture),
            normalized,
            solid,
            new MatterAmount(minimum),
            new MatterAmount(maximum));
    }

    public FieldProbePresentation Probe(WorldSession session, LatticeCoordinate coordinate, FieldChannelId channel)
    {
        ArgumentNullException.ThrowIfNull(session);
        WorldEnvironmentState environment = session.EnvironmentState
            ?? throw new ArgumentException("An environment presentation probe requires a V3 session.", nameof(session));
        FieldProbeResult result = new FieldProbeService().Probe(new WorldEnvironmentStore(environment), environment.Regions.Single().RegionId, coordinate, channel);
        if (!result.Success) throw new ArgumentOutOfRangeException(nameof(coordinate), result.Issues[0].Summary);
        string concentration = result.Concentration is null
            ? "unavailable (zero effective volume)"
            : $"{result.Concentration.Value.Numerator}/{result.Concentration.Value.Denominator}";
        return new(result.RegionId, result.Coordinate, result.ChannelId, result.IsSolid, result.Amount, result.EffectiveVolume,
            concentration, result.ChannelTotal.ToString(CultureInfo.InvariantCulture), "AUTHORITATIVE CELL SAMPLE");
    }
}
