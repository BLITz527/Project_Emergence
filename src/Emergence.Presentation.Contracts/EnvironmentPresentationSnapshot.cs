using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Model.Environment;

namespace Emergence.Presentation.Contracts;

public sealed class EnvironmentPresentationSnapshot
{
    private readonly ReadOnlyCollection<double> _normalizedSurface;
    private readonly ReadOnlyCollection<bool> _solidMask;

    public EnvironmentPresentationSnapshot(
        RegionId regionId,
        Sha256Digest regionDefinitionDigest,
        Sha256Digest regionStateDigest,
        Sha256Digest environmentStateDigest,
        uint width,
        uint height,
        FieldChannelId selectedChannel,
        string selectedChannelTotal,
        IEnumerable<double> normalizedSurface,
        IEnumerable<bool> solidMask,
        MatterAmount minimumFluidAmount,
        MatterAmount maximumFluidAmount)
    {
        if (regionId.IsEmpty) throw new ArgumentException("Presentation region ID cannot be empty.", nameof(regionId));
        if (width == 0 || height == 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!selectedChannel.IsValid) throw new ArgumentException("Selected field channel ID must be valid.", nameof(selectedChannel));
        if (string.IsNullOrEmpty(selectedChannelTotal) || (selectedChannelTotal.Length > 1 && selectedChannelTotal[0] == '0')
            || selectedChannelTotal.Any(static value => value is < '0' or > '9'))
            throw new ArgumentException("Selected channel total must be a canonical unsigned decimal string.", nameof(selectedChannelTotal));
        ArgumentNullException.ThrowIfNull(normalizedSurface);
        ArgumentNullException.ThrowIfNull(solidMask);
        double[] surface = normalizedSurface.ToArray();
        bool[] mask = solidMask.ToArray();
        int cellCount = checked((int)(width * height));
        if (surface.Length != cellCount || mask.Length != cellCount) throw new ArgumentException("Presentation surfaces must exactly match width times height.");
        if (surface.Any(static value => !double.IsFinite(value) || value < 0d || value > 1d))
            throw new ArgumentException("Normalized presentation values must be finite and between zero and one.", nameof(normalizedSurface));
        if (minimumFluidAmount.Quanta > maximumFluidAmount.Quanta) throw new ArgumentException("Minimum fluid amount cannot exceed maximum.");
        RegionId = regionId;
        RegionDefinitionDigest = regionDefinitionDigest;
        RegionStateDigest = regionStateDigest;
        EnvironmentStateDigest = environmentStateDigest;
        Width = width;
        Height = height;
        SelectedChannel = selectedChannel;
        SelectedChannelTotal = selectedChannelTotal;
        _normalizedSurface = Array.AsReadOnly(surface);
        _solidMask = Array.AsReadOnly(mask);
        MinimumFluidAmount = minimumFluidAmount;
        MaximumFluidAmount = maximumFluidAmount;
    }

    [JsonPropertyOrder(0)] public RegionId RegionId { get; }
    [JsonPropertyOrder(1)] public Sha256Digest RegionDefinitionDigest { get; }
    [JsonPropertyOrder(2)] public Sha256Digest RegionStateDigest { get; }
    [JsonPropertyOrder(3)] public Sha256Digest EnvironmentStateDigest { get; }
    [JsonPropertyOrder(4)] public uint Width { get; }
    [JsonPropertyOrder(5)] public uint Height { get; }
    [JsonPropertyOrder(6)] public FieldChannelId SelectedChannel { get; }
    [JsonPropertyOrder(7)] public string SelectedChannelTotal { get; }
    [JsonPropertyOrder(8)] public IReadOnlyList<double> NormalizedSurface => _normalizedSurface;
    [JsonPropertyOrder(9)] public IReadOnlyList<bool> SolidMask => _solidMask;
    [JsonPropertyOrder(10)] public MatterAmount MinimumFluidAmount { get; }
    [JsonPropertyOrder(11)] public MatterAmount MaximumFluidAmount { get; }
    [JsonPropertyOrder(12)] public bool HasBiologicalState => false;
}

public sealed record FieldProbePresentation(
    [property: JsonPropertyOrder(0)] RegionId RegionId,
    [property: JsonPropertyOrder(1)] LatticeCoordinate Coordinate,
    [property: JsonPropertyOrder(2)] FieldChannelId ChannelId,
    [property: JsonPropertyOrder(3)] bool IsSolid,
    [property: JsonPropertyOrder(4)] MatterAmount RawAmount,
    [property: JsonPropertyOrder(5)] VolumeAmount EffectiveVolume,
    [property: JsonPropertyOrder(6)] string DerivedConcentrationDisplay,
    [property: JsonPropertyOrder(7)] string ChannelTotal,
    [property: JsonPropertyOrder(8)] string SampleKind,
    [property: JsonPropertyOrder(9)] bool HasBiologicalState = false);
