using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;

namespace Emergence.Model.Environment;

public static class EnvironmentTechnicalLimits
{
    public const int MaxRegions = 1;
    public const int MaxFieldChannels = 16;
    public const uint MaxLatticeWidth = 512;
    public const uint MaxLatticeHeight = 512;
    public const int MaxCellsPerRegion = 262_144;
    public const int MaxChunksPerRegion = 4_096;
    public const int MaxFieldSlotsPerRegion = 4_194_304;
    public static IReadOnlyList<uint> AllowedChunkEdges { get; } = Array.AsReadOnly<uint>([8, 16, 32, 64]);
}

public sealed class FieldChannelDefinition : IEquatable<FieldChannelDefinition>
{
    public FieldChannelDefinition(FieldChannelId id, FieldChannelRole role, string displayName, string description)
    {
        if (!id.IsValid) throw new ArgumentException("Field channel ID must be valid.", nameof(id));
        if (role != FieldChannelRole.ConservedMaterial) throw new ArgumentOutOfRangeException(nameof(role));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Field channel display name cannot be empty.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Field channel description cannot be empty.", nameof(description));
        Id = id;
        Role = role;
        DisplayName = displayName;
        Description = description;
    }

    public FieldChannelId Id { get; }
    public FieldChannelRole Role { get; }
    public string DisplayName { get; }
    public string Description { get; }

    public bool Equals(FieldChannelDefinition? other) => other is not null
        && Id == other.Id && Role == other.Role && DisplayName == other.DisplayName && Description == other.Description;
    public override bool Equals(object? obj) => obj is FieldChannelDefinition other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Id, Role, DisplayName, Description);
}

[JsonConverter(typeof(FieldChannelCatalogJsonConverter))]
public sealed class FieldChannelCatalog : IEquatable<FieldChannelCatalog>
{
    public const string DigestDomainMarker = "ProjectEmergence.FieldChannelCatalog.v1";
    private readonly ReadOnlyCollection<FieldChannelDefinition> _definitions;
    private readonly Dictionary<FieldChannelId, int> _slots;

    public FieldChannelCatalog(IEnumerable<FieldChannelDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        FieldChannelDefinition?[] source = definitions.Cast<FieldChannelDefinition?>().ToArray();
        if (source.Length is 0 or > EnvironmentTechnicalLimits.MaxFieldChannels)
            throw new ArgumentException($"A field channel catalog requires 1 through {EnvironmentTechnicalLimits.MaxFieldChannels} definitions.", nameof(definitions));
        if (source.Any(static definition => definition is null))
            throw new ArgumentException("Field channel definitions cannot contain null.", nameof(definitions));
        FieldChannelDefinition[] sorted = source.Select(static definition => definition!).OrderBy(static definition => definition.Id).ToArray();
        if (sorted.Any(static definition => !definition.Id.IsValid)
            || sorted.Select(static definition => definition.Id).Distinct().Count() != sorted.Length)
            throw new ArgumentException("Field channel IDs must be valid and unique.", nameof(definitions));
        _definitions = Array.AsReadOnly(sorted);
        _slots = sorted.Select((definition, slot) => (definition.Id, slot)).ToDictionary(static item => item.Id, static item => item.slot);
        Digest = ComputeDigest(sorted);
    }

    public IReadOnlyList<FieldChannelDefinition> Definitions => _definitions;
    public Sha256Digest Digest { get; }
    public bool TryGet(FieldChannelId id, out FieldChannelDefinition? definition)
    {
        if (_slots.TryGetValue(id, out int slot)) { definition = _definitions[slot]; return true; }
        definition = null;
        return false;
    }
    public bool TryGetSlot(FieldChannelId id, out int slot) => _slots.TryGetValue(id, out slot);

    public bool Equals(FieldChannelCatalog? other) => other is not null
        && Digest == other.Digest && _definitions.SequenceEqual(other._definitions);
    public override bool Equals(object? obj) => obj is FieldChannelCatalog other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private static Sha256Digest ComputeDigest(IReadOnlyList<FieldChannelDefinition> definitions)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteUInt64(checked((ulong)definitions.Count));
        foreach (FieldChannelDefinition definition in definitions)
        {
            writer.WriteString(definition.Id.ToString());
            writer.WriteString(definition.Role.ToString());
        }
        return writer.FinalizeDigest();
    }

    internal static FieldChannelCatalog CreateValidated(IEnumerable<FieldChannelDefinition> definitions, Sha256Digest expected)
    {
        FieldChannelCatalog catalog = new(definitions);
        return catalog.Digest == expected ? catalog : throw new JsonException("Field channel catalog digest mismatch.");
    }
}

internal sealed class FieldChannelCatalogJsonConverter : JsonConverter<FieldChannelCatalog>
{
    public override FieldChannelCatalog Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        StrictModelJson.Exact(root, "definitions", "digest");
        try
        {
            FieldChannelDefinition[] definitions = root.GetProperty("definitions").EnumerateArray()
                .Select(element => ParseDefinition(element, options)).ToArray();
            return FieldChannelCatalog.CreateValidated(definitions, Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
        }
        catch (JsonException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            throw new JsonException("Invalid field channel catalog.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, FieldChannelCatalog value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WritePropertyName("definitions");
        writer.WriteStartArray();
        foreach (FieldChannelDefinition definition in value.Definitions)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("id"); JsonSerializer.Serialize(writer, definition.Id, options);
            writer.WritePropertyName("role"); JsonSerializer.Serialize(writer, definition.Role, options);
            writer.WriteString("displayName", definition.DisplayName);
            writer.WriteString("description", definition.Description);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }

    private static FieldChannelDefinition ParseDefinition(JsonElement element, JsonSerializerOptions options)
    {
        StrictModelJson.Exact(element, "id", "role", "displayName", "description");
        return new(
            JsonSerializer.Deserialize<FieldChannelId>(element.GetProperty("id"), options),
            JsonSerializer.Deserialize<FieldChannelRole>(element.GetProperty("role"), options),
            element.GetProperty("displayName").GetString() ?? throw new JsonException("Missing field channel display name."),
            element.GetProperty("description").GetString() ?? throw new JsonException("Missing field channel description."));
    }
}
