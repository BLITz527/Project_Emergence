using System.Collections.ObjectModel;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Results;
using Emergence.Model.Environment;

namespace Emergence.Simulation.Fields;

public readonly record struct ExactConcentration
{
    public ExactConcentration(MatterAmount numerator, VolumeAmount denominator)
    {
        if (denominator.Quanta == 0) throw new ArgumentException("Concentration denominator must be nonzero.", nameof(denominator));
        Numerator = numerator;
        Denominator = denominator;
    }
    public MatterAmount Numerator { get; }
    public VolumeAmount Denominator { get; }
}

public sealed class FieldProbeResult
{
    internal FieldProbeResult(
        bool success, RegionId regionId, LatticeCoordinate coordinate, FieldChannelId channelId,
        bool isInsideRegion, bool isSolid, VolumeAmount effectiveVolume, MatterAmount amount,
        ExactConcentration? concentration, Sha256Digest regionStateDigest, UInt128 channelTotal,
        IEnumerable<FoundationIssue> issues)
    {
        Success = success;
        RegionId = regionId;
        Coordinate = coordinate;
        ChannelId = channelId;
        IsInsideRegion = isInsideRegion;
        IsSolid = isSolid;
        EffectiveVolume = effectiveVolume;
        Amount = amount;
        Concentration = concentration;
        RegionStateDigest = regionStateDigest;
        ChannelTotal = channelTotal;
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public bool Success { get; }
    public RegionId RegionId { get; }
    public LatticeCoordinate Coordinate { get; }
    public FieldChannelId ChannelId { get; }
    public bool IsInsideRegion { get; }
    public bool IsSolid { get; }
    public VolumeAmount EffectiveVolume { get; }
    public MatterAmount Amount { get; }
    public ExactConcentration? Concentration { get; }
    public Sha256Digest RegionStateDigest { get; }
    public UInt128 ChannelTotal { get; }
    public IReadOnlyList<FoundationIssue> Issues { get; }
}

public sealed class FieldProbeService
{
    public FieldProbeResult Probe(WorldEnvironmentStore environment, RegionId regionId, LatticeCoordinate coordinate, FieldChannelId channelId)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.TryGetRegion(regionId, out RegionFieldStore? region) || region is null)
            return Failure(regionId, coordinate, channelId, "environment.probe-region", "Unknown environment region");
        if (!region.Definition.Contains(coordinate))
            return Failure(regionId, coordinate, channelId, "environment.probe-coordinate", "Coordinate is outside the region");
        if (!region.Definition.FieldChannels.TryGetSlot(channelId, out _))
            return Failure(regionId, coordinate, channelId, "environment.probe-channel", "Unknown field channel");

        VolumeAmount volume = region.Definition.GetEffectiveVolume(coordinate);
        MatterAmount amount = region.GetAmount(channelId, coordinate);
        bool solid = volume.Quanta == 0;
        return new(
            true, regionId, coordinate, channelId, true, solid, volume, amount,
            solid ? null : new ExactConcentration(amount, volume),
            region.Digest, region.GetChannelTotal(channelId), []);
    }

    private static FieldProbeResult Failure(RegionId regionId, LatticeCoordinate coordinate, FieldChannelId channelId, string code, string summary) =>
        new(false, regionId, coordinate, channelId, false, false, default, default, null, default, UInt128.Zero,
            [new FoundationIssue(new(code), IssueSeverity.Error, summary, "The authoritative probe request was rejected without interpolation or mutation.")]);
}
