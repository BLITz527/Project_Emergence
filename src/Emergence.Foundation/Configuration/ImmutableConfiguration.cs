using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Configuration;

[JsonConverter(typeof(ImmutableConfigurationJsonConverter))]
public sealed class ImmutableConfiguration : IEquatable<ImmutableConfiguration>
{
    public const string DigestDomainMarker = "ProjectEmergence.Configuration.v1";
    private readonly ReadOnlyCollection<ConfigurationEntry> _entries;

    public ImmutableConfiguration(ConfigurationSchemaId schemaId, SemanticVersion schemaVersion, IEnumerable<ConfigurationEntry> entries)
    {
        if (schemaId.IsEmpty) throw new ArgumentException("Configuration schema ID cannot be empty.", nameof(schemaId));
        ArgumentNullException.ThrowIfNull(entries);
        ConfigurationEntry[] source = entries.ToArray();
        if (source.Any(static entry => entry is null)) throw new ArgumentException("Configuration entries cannot be null.", nameof(entries));
        if (source.Any(static entry => entry.Key.IsEmpty)) throw new ArgumentException("Default configuration keys are not allowed.", nameof(entries));
        ConfigurationEntry[] sorted = source.OrderBy(static entry => entry.Key).ToArray();
        if (sorted.Select(static entry => entry.Key).Distinct().Count() != sorted.Length) throw new ArgumentException("Duplicate configuration keys are not allowed.", nameof(entries));
        SchemaId = schemaId;
        SchemaVersion = schemaVersion;
        _entries = Array.AsReadOnly(sorted);
        Digest = ComputeDigest(schemaId, schemaVersion, sorted);
    }

    public ConfigurationSchemaId SchemaId { get; }
    public SemanticVersion SchemaVersion { get; }
    public IReadOnlyList<ConfigurationEntry> Entries => _entries;
    public Sha256Digest Digest { get; }

    public bool Equals(ImmutableConfiguration? other) => other is not null && SchemaId == other.SchemaId && SchemaVersion == other.SchemaVersion && _entries.SequenceEqual(other._entries);
    public override bool Equals(object? obj) => obj is ImmutableConfiguration other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private static Sha256Digest ComputeDigest(ConfigurationSchemaId schemaId, SemanticVersion version, IReadOnlyList<ConfigurationEntry> entries)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteString(schemaId.ToString());
        writer.WriteString(version.ToString());
        writer.WriteUInt64(checked((ulong)entries.Count));
        foreach (ConfigurationEntry entry in entries)
        {
            writer.WriteString(entry.Key.ToString());
            writer.WriteString(entry.Value.Kind.ToString());
            switch (entry.Value.Kind)
            {
                case ConfigurationValueKind.Boolean: writer.WriteBoolean(entry.Value.Boolean); break;
                case ConfigurationValueKind.UInt64: writer.WriteUInt64(entry.Value.UInt64); break;
                case ConfigurationValueKind.Digest: writer.WriteDigest(entry.Value.Digest); break;
                default: writer.WriteString(entry.Value.CanonicalText()); break;
            }
        }
        return writer.FinalizeDigest();
    }

    internal static ImmutableConfiguration CreateValidated(ConfigurationSchemaId schemaId, SemanticVersion version, IEnumerable<ConfigurationEntry> entries, Sha256Digest expected)
    {
        ImmutableConfiguration configuration = new(schemaId, version, entries);
        return configuration.Digest == expected ? configuration : throw new JsonException("Configuration digest mismatch.");
    }
}

internal sealed class ImmutableConfigurationJsonConverter : JsonConverter<ImmutableConfiguration>
{
    public override ImmutableConfiguration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        ConfigurationSchemaId schema = new(root.GetProperty("schemaId").GetString()!);
        SemanticVersion version = SemanticVersion.Parse(root.GetProperty("schemaVersion").GetString()!);
        List<ConfigurationEntry> entries = [];
        foreach (JsonElement element in root.GetProperty("entries").EnumerateArray())
        {
            ConfigurationKey key = new(element.GetProperty("key").GetString()!);
            ConfigurationValue value = JsonSerializer.Deserialize<ConfigurationValue>(element.GetProperty("value"), options);
            entries.Add(new(key, value));
        }
        Sha256Digest digest = Sha256Digest.Parse(root.GetProperty("digest").GetString()!);
        return ImmutableConfiguration.CreateValidated(schema, version, entries, digest);
    }

    public override void Write(Utf8JsonWriter writer, ImmutableConfiguration value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("schemaId", value.SchemaId.ToString());
        writer.WriteString("schemaVersion", value.SchemaVersion.ToString());
        writer.WritePropertyName("entries"); writer.WriteStartArray();
        foreach (ConfigurationEntry entry in value.Entries)
        {
            writer.WriteStartObject(); writer.WriteString("key", entry.Key.ToString()); writer.WritePropertyName("value"); JsonSerializer.Serialize(writer, entry.Value, options); writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }
}
