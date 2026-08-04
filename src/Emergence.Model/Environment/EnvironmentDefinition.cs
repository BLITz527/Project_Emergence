using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Versioning;

namespace Emergence.Model.Environment;

[JsonConverter(typeof(EnvironmentDefinitionJsonConverter))]
public sealed class EnvironmentDefinition : IEquatable<EnvironmentDefinition>
{
    public const string DigestDomainMarker = "ProjectEmergence.EnvironmentDefinition.v1";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    private readonly ReadOnlyCollection<RegionLatticeDefinition> _regions;

    public EnvironmentDefinition(FieldChannelCatalog fieldChannels, IEnumerable<RegionLatticeDefinition> regions)
    {
        FieldChannels = fieldChannels ?? throw new ArgumentNullException(nameof(fieldChannels));
        ArgumentNullException.ThrowIfNull(regions);
        RegionLatticeDefinition?[] source = regions.Cast<RegionLatticeDefinition?>().ToArray();
        if (source.Length != EnvironmentTechnicalLimits.MaxRegions || source.Any(static region => region is null))
            throw new ArgumentException("Phase 1.1 requires exactly one nonnull region.", nameof(regions));
        RegionLatticeDefinition[] sorted = source.Select(static region => region!).OrderBy(static region => region.RegionId).ToArray();
        if (sorted.Select(static region => region.RegionId).Distinct().Count() != sorted.Length)
            throw new ArgumentException("Environment region IDs must be unique.", nameof(regions));
        if (sorted.Any(region => region.FieldChannels.Digest != fieldChannels.Digest || !region.FieldChannels.Equals(fieldChannels)))
            throw new ArgumentException("Every region must use the environment field channel catalog.", nameof(regions));
        FormatVersion = SupportedFormatVersion;
        _regions = Array.AsReadOnly(sorted);
        Digest = ComputeDigest();
    }

    public SemanticVersion FormatVersion { get; }
    public FieldChannelCatalog FieldChannels { get; }
    public IReadOnlyList<RegionLatticeDefinition> Regions => _regions;
    public Sha256Digest Digest { get; }
    public bool TryGetRegion(RegionId regionId, out RegionLatticeDefinition? region)
    {
        region = _regions.Count == 1 && _regions[0].RegionId == regionId ? _regions[0] : null;
        return region is not null;
    }

    public bool Equals(EnvironmentDefinition? other) => other is not null
        && FormatVersion == other.FormatVersion && FieldChannels.Equals(other.FieldChannels)
        && _regions.SequenceEqual(other._regions) && Digest == other.Digest;
    public override bool Equals(object? obj) => obj is EnvironmentDefinition other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputeDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteDigest(FieldChannels.Digest);
        writer.WriteUInt64(checked((ulong)_regions.Count));
        foreach (RegionLatticeDefinition region in _regions)
        {
            writer.WriteString(region.RegionId.ToString());
            writer.WriteDigest(region.Digest);
        }
        return writer.FinalizeDigest();
    }

    internal static EnvironmentDefinition CreateValidated(
        SemanticVersion formatVersion, FieldChannelCatalog channels, IEnumerable<RegionLatticeDefinition> regions, Sha256Digest expected)
    {
        if (formatVersion != SupportedFormatVersion) throw new JsonException($"Unsupported environment definition format '{formatVersion}'.");
        EnvironmentDefinition definition = new(channels, regions);
        return definition.Digest == expected ? definition : throw new JsonException("Environment definition digest mismatch.");
    }
}

internal sealed class EnvironmentDefinitionJsonConverter : JsonConverter<EnvironmentDefinition>
{
    public override EnvironmentDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        StrictModelJson.Exact(root, "formatVersion", "fieldChannels", "regions", "digest");
        try
        {
            return EnvironmentDefinition.CreateValidated(
                SemanticVersion.Parse(root.GetProperty("formatVersion").GetString()!),
                JsonSerializer.Deserialize<FieldChannelCatalog>(root.GetProperty("fieldChannels"), options) ?? throw new JsonException("Missing field channel catalog."),
                root.GetProperty("regions").EnumerateArray().Select(element =>
                    JsonSerializer.Deserialize<RegionLatticeDefinition>(element, options) ?? throw new JsonException("Missing region definition.")),
                Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
        }
        catch (JsonException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new JsonException("Invalid environment definition.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, EnvironmentDefinition value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("formatVersion", value.FormatVersion.ToString());
        writer.WritePropertyName("fieldChannels"); JsonSerializer.Serialize(writer, value.FieldChannels, options);
        writer.WritePropertyName("regions"); writer.WriteStartArray();
        foreach (RegionLatticeDefinition region in value.Regions) JsonSerializer.Serialize(writer, region, options);
        writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }
}
