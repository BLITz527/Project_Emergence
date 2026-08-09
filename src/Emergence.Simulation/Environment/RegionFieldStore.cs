using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Model.Environment;

namespace Emergence.Simulation.Fields;

/// <summary>Simulation-owned dense exact field storage. Phase 1.1 exposes no mutation API.</summary>
public sealed class RegionFieldStore
{
    private readonly ulong[][] _amountsByChannel;
    private readonly UInt128[] _channelTotals;

    public RegionFieldStore(RegionLatticeDefinition definition, IEnumerable<RegionFieldChannelAmounts> channels)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RegionFieldState validated = new(definition, channels);
        _amountsByChannel = new ulong[definition.FieldChannels.Definitions.Count][];
        _channelTotals = new UInt128[_amountsByChannel.Length];
        for (int slot = 0; slot < _amountsByChannel.Length; slot++)
        {
            ulong[] buffer = new ulong[definition.CellCount];
            for (int index = 0; index < buffer.Length; index++) buffer[index] = validated.GetAmount(slot, index).Quanta;
            _amountsByChannel[slot] = buffer;
            _channelTotals[slot] = validated.GetChannelTotal(slot);
        }
    }

    public RegionFieldStore(RegionFieldState state)
        : this(state?.Definition ?? throw new ArgumentNullException(nameof(state)), CreateChannels(state))
    {
    }

    public RegionLatticeDefinition Definition { get; }
    public RegionId RegionId => Definition.RegionId;
    public int AllocatedFieldBytes => checked(Definition.CellCount * _amountsByChannel.Length * sizeof(ulong));
    public Sha256Digest Digest => Capture().Digest;

    public bool TryGetAmount(FieldChannelId channel, LatticeCoordinate coordinate, out MatterAmount amount)
    {
        amount = default;
        if (!Definition.Contains(coordinate) || !Definition.FieldChannels.TryGetSlot(channel, out int slot)) return false;
        amount = new(_amountsByChannel[slot][Definition.GetLinearIndex(coordinate)]);
        return true;
    }
    public MatterAmount GetAmount(FieldChannelId channel, LatticeCoordinate coordinate)
    {
        if (!TryGetAmount(channel, coordinate, out MatterAmount amount)) throw new ArgumentOutOfRangeException();
        return amount;
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
    public void CopyRectangle(
        FieldChannelId channel, LatticeCoordinate topLeft, uint width, uint height, Span<MatterAmount> destination)
    {
        if (width == 0 || height == 0) throw new ArgumentOutOfRangeException(nameof(width));
        ulong required = checked((ulong)width * height);
        if ((ulong)destination.Length < required) throw new ArgumentException("Destination is too small for the requested rectangle.", nameof(destination));
        if (!Definition.FieldChannels.TryGetSlot(channel, out int slot)) throw new ArgumentOutOfRangeException(nameof(channel));
        if (topLeft.X >= Definition.Width || topLeft.Y >= Definition.Height
            || (ulong)topLeft.X + width > Definition.Width || (ulong)topLeft.Y + height > Definition.Height)
            throw new ArgumentOutOfRangeException(nameof(topLeft));
        int target = 0;
        for (uint y = 0; y < height; y++)
        for (uint x = 0; x < width; x++)
            destination[target++] = new(_amountsByChannel[slot][Definition.GetLinearIndex(new(topLeft.X + x, topLeft.Y + y))]);
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

    private static IEnumerable<RegionFieldChannelAmounts> CreateChannels(RegionFieldState state) =>
        state.Definition.FieldChannels.Definitions.Select((definition, slot) =>
        {
            MatterAmount[] copy = new MatterAmount[state.Definition.CellCount];
            for (int index = 0; index < copy.Length; index++) copy[index] = state.GetAmount(slot, index);
            return new RegionFieldChannelAmounts(definition.Id, Array.AsReadOnly(copy));
        });
}

public sealed class WorldEnvironmentStore
{
    private readonly RegionFieldStore _region;

    public WorldEnvironmentStore(EnvironmentDefinition definition, RegionFieldStore region)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        if (definition.Regions.Count != 1 || !definition.Regions[0].Equals(region.Definition))
            throw new ArgumentException("Environment store must exactly match the one-region definition.", nameof(region));
    }

    public WorldEnvironmentStore(WorldEnvironmentState state)
        : this(state?.Definition ?? throw new ArgumentNullException(nameof(state)), new RegionFieldStore(state.Regions.Single()))
    {
    }

    public EnvironmentDefinition Definition { get; }
    public Sha256Digest Digest => Capture().Digest;
    public RegionFieldStore Region => _region;
    public bool TryGetRegion(RegionId regionId, out RegionFieldStore? region)
    {
        region = regionId == _region.RegionId ? _region : null;
        return region is not null;
    }
    public WorldEnvironmentState Capture() => new(Definition, [_region.Capture()]);
}
