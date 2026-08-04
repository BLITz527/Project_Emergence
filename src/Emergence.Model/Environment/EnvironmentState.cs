using System.Collections.ObjectModel;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Versioning;

namespace Emergence.Model.Environment;

public sealed class RegionFieldChannelAmounts
{
    public RegionFieldChannelAmounts(FieldChannelId channelId, IReadOnlyList<MatterAmount> amounts)
    {
        if (!channelId.IsValid) throw new ArgumentException("Field channel ID must be valid.", nameof(channelId));
        ChannelId = channelId;
        Amounts = amounts ?? throw new ArgumentNullException(nameof(amounts));
    }

    public FieldChannelId ChannelId { get; }
    public IReadOnlyList<MatterAmount> Amounts { get; }
}

/// <summary>Immutable Model snapshot of all exact field amounts for one region.</summary>
public sealed class RegionFieldState : IEquatable<RegionFieldState>
{
    public const string DigestDomainMarker = "ProjectEmergence.RegionFieldState.v1";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    private readonly ulong[][] _amountsByChannel;
    private readonly UInt128[] _channelTotals;

    public RegionFieldState(RegionLatticeDefinition definition, IEnumerable<RegionFieldChannelAmounts> channels)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ArgumentNullException.ThrowIfNull(channels);
        RegionFieldChannelAmounts?[] source = channels.Cast<RegionFieldChannelAmounts?>().ToArray();
        if (source.Length != definition.FieldChannels.Definitions.Count || source.Any(static item => item is null))
            throw new ArgumentException("Region field state requires exactly one nonnull amount buffer per channel.", nameof(channels));

        _amountsByChannel = new ulong[source.Length][];
        _channelTotals = new UInt128[source.Length];
        bool[] seen = new bool[source.Length];
        foreach (RegionFieldChannelAmounts item in source.Select(static item => item!))
        {
            if (!definition.FieldChannels.TryGetSlot(item.ChannelId, out int slot) || seen[slot])
                throw new ArgumentException("Region field channels must exactly match the definition catalog without duplicates.", nameof(channels));
            if (item.Amounts.Count != definition.CellCount)
                throw new ArgumentException("Every region field amount buffer must equal the definition cell count.", nameof(channels));
            ulong[] copy = new ulong[item.Amounts.Count];
            UInt128 total = UInt128.Zero;
            for (int index = 0; index < copy.Length; index++)
            {
                MatterAmount amount = item.Amounts[index];
                if (definition.EffectiveVolumes[index].Quanta == 0 && amount.Quanta != 0)
                    throw new ArgumentException("Solid cells cannot contain field matter.", nameof(channels));
                copy[index] = amount.Quanta;
                total = checked(total + amount.Quanta);
            }
            _amountsByChannel[slot] = copy;
            _channelTotals[slot] = total;
            seen[slot] = true;
        }
        if (seen.Any(static value => !value)) throw new ArgumentException("A region field channel is missing.", nameof(channels));

        FormatVersion = SupportedFormatVersion;
        RegionId = definition.RegionId;
        Digest = ComputeDigest();
    }

    public SemanticVersion FormatVersion { get; }
    public RegionLatticeDefinition Definition { get; }
    public RegionId RegionId { get; }
    public Sha256Digest Digest { get; }

    public MatterAmount GetAmount(FieldChannelId channel, LatticeCoordinate coordinate)
    {
        if (!Definition.FieldChannels.TryGetSlot(channel, out int slot)) throw new ArgumentOutOfRangeException(nameof(channel));
        return GetAmount(slot, Definition.GetLinearIndex(coordinate));
    }
    public MatterAmount GetAmount(int channelSlot, int linearIndex)
    {
        if ((uint)channelSlot >= (uint)_amountsByChannel.Length) throw new ArgumentOutOfRangeException(nameof(channelSlot));
        if ((uint)linearIndex >= (uint)Definition.CellCount) throw new ArgumentOutOfRangeException(nameof(linearIndex));
        return new(_amountsByChannel[channelSlot][linearIndex]);
    }
    public UInt128 GetChannelTotal(FieldChannelId channel)
    {
        if (!Definition.FieldChannels.TryGetSlot(channel, out int slot)) throw new ArgumentOutOfRangeException(nameof(channel));
        return _channelTotals[slot];
    }
    public UInt128 GetChannelTotal(int channelSlot)
    {
        if ((uint)channelSlot >= (uint)_channelTotals.Length) throw new ArgumentOutOfRangeException(nameof(channelSlot));
        return _channelTotals[channelSlot];
    }
    public void CopyChannel(FieldChannelId channel, Span<MatterAmount> destination)
    {
        if (destination.Length < Definition.CellCount) throw new ArgumentException("Destination is smaller than the region cell count.", nameof(destination));
        if (!Definition.FieldChannels.TryGetSlot(channel, out int slot)) throw new ArgumentOutOfRangeException(nameof(channel));
        for (int index = 0; index < Definition.CellCount; index++) destination[index] = new(_amountsByChannel[slot][index]);
    }
    public RegionFieldState Capture()
    {
        RegionFieldChannelAmounts[] channels = Definition.FieldChannels.Definitions.Select((definition, slot) =>
        {
            MatterAmount[] copy = new MatterAmount[Definition.CellCount];
            for (int index = 0; index < copy.Length; index++) copy[index] = new(_amountsByChannel[slot][index]);
            return new RegionFieldChannelAmounts(definition.Id, Array.AsReadOnly(copy));
        }).ToArray();
        return new(Definition, channels);
    }

    public bool Equals(RegionFieldState? other)
    {
        if (other is null || !Definition.Equals(other.Definition) || Digest != other.Digest) return false;
        for (int slot = 0; slot < _amountsByChannel.Length; slot++)
            if (!_amountsByChannel[slot].AsSpan().SequenceEqual(other._amountsByChannel[slot])) return false;
        return true;
    }
    public override bool Equals(object? obj) => obj is RegionFieldState other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputeDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteDigest(Definition.Digest);
        writer.WriteUInt64(checked((ulong)_amountsByChannel.Length));
        for (int slot = 0; slot < _amountsByChannel.Length; slot++)
        {
            writer.WriteString(Definition.FieldChannels.Definitions[slot].Id.ToString());
            writer.WriteUInt128(_channelTotals[slot]);
            writer.WriteUInt64(checked((ulong)Definition.CellCount));
            foreach (ulong amount in _amountsByChannel[slot]) writer.WriteUInt64(amount);
        }
        return writer.FinalizeDigest();
    }
}

/// <summary>Immutable exact environmental state captured at a coherent session boundary.</summary>
public sealed class WorldEnvironmentState : IEquatable<WorldEnvironmentState>
{
    public const string DigestDomainMarker = "ProjectEmergence.WorldEnvironmentState.v1";
    private readonly ReadOnlyCollection<RegionFieldState> _regions;

    public WorldEnvironmentState(EnvironmentDefinition definition, IEnumerable<RegionFieldState> regions)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ArgumentNullException.ThrowIfNull(regions);
        RegionFieldState?[] source = regions.Cast<RegionFieldState?>().ToArray();
        if (source.Length != definition.Regions.Count || source.Any(static region => region is null))
            throw new ArgumentException("Environment state requires exactly one nonnull state per defined region.", nameof(regions));
        RegionFieldState[] sorted = source.Select(static region => region!).OrderBy(static region => region.RegionId).ToArray();
        for (int index = 0; index < sorted.Length; index++)
        {
            if (sorted[index].RegionId != definition.Regions[index].RegionId
                || !sorted[index].Definition.Equals(definition.Regions[index]))
                throw new ArgumentException("Environment region state does not match its immutable definition.", nameof(regions));
        }
        _regions = Array.AsReadOnly(sorted);
        Digest = ComputeDigest();
    }

    public EnvironmentDefinition Definition { get; }
    public IReadOnlyList<RegionFieldState> Regions => _regions;
    public Sha256Digest Digest { get; }
    public bool TryGetRegion(RegionId regionId, out RegionFieldState? region)
    {
        region = _regions.Count == 1 && _regions[0].RegionId == regionId ? _regions[0] : null;
        return region is not null;
    }
    public WorldEnvironmentState Capture() => new(Definition, _regions.Select(static region => region.Capture()));

    public bool Equals(WorldEnvironmentState? other) => other is not null
        && Definition.Equals(other.Definition) && _regions.SequenceEqual(other._regions) && Digest == other.Digest;
    public override bool Equals(object? obj) => obj is WorldEnvironmentState other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputeDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteDigest(Definition.Digest);
        writer.WriteUInt64(checked((ulong)_regions.Count));
        foreach (RegionFieldState region in _regions)
        {
            writer.WriteString(region.RegionId.ToString());
            writer.WriteDigest(region.Digest);
        }
        return writer.FinalizeDigest();
    }
}
