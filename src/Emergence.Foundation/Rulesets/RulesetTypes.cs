using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Rulesets;

[JsonConverter(typeof(RulesetKeyJsonConverter))]
public readonly record struct RulesetKey : IComparable<RulesetKey>
{
    public RulesetKey(RulesetId id, SemanticVersion version)
    {
        if (id.IsEmpty) throw new ArgumentException("Ruleset ID cannot be empty.", nameof(id));
        Id = id; Version = version;
    }
    public RulesetId Id { get; }
    public SemanticVersion Version { get; }
    public bool IsEmpty => Id.IsEmpty;
    public static RulesetKey Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text); int separator = text.IndexOf('@');
        if (separator != 32 || text.LastIndexOf('@') != separator) throw new FormatException("A ruleset key must use id@major.minor.patch form.");
        return new(RulesetId.Parse(text[..separator]), SemanticVersion.Parse(text[(separator + 1)..]));
    }
    public static bool TryParse(string? text, out RulesetKey value) { try { value = Parse(text!); return true; } catch (Exception e) when (e is ArgumentException or FormatException) { value = default; return false; } }
    public int CompareTo(RulesetKey other) { int result = Id.CompareTo(other.Id); return result != 0 ? result : Version.CompareTo(other.Version); }
    public override string ToString() => IsEmpty ? string.Empty : $"{Id}@{Version}";
}

[JsonConverter(typeof(RulesetDescriptorJsonConverter))]
public sealed class RulesetDescriptor : IEquatable<RulesetDescriptor>
{
    public const string DigestDomainMarker = "ProjectEmergence.RulesetManifest.v1";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    public RulesetDescriptor(SemanticVersion formatVersion, RulesetKey key, string displayName, AlgorithmCatalog algorithms, RngDomainCatalog rngDomains, ImmutableConfiguration configuration)
    {
        if (formatVersion != SupportedFormatVersion) throw new ArgumentException("Ruleset format version must be exactly 1.0.0.", nameof(formatVersion));
        if (key.IsEmpty) throw new ArgumentException("Ruleset key cannot be empty.", nameof(key));
        ValidateDisplayName(displayName);
        FormatVersion = formatVersion; Key = key; DisplayName = displayName;
        Algorithms = algorithms ?? throw new ArgumentNullException(nameof(algorithms));
        RngDomains = rngDomains ?? throw new ArgumentNullException(nameof(rngDomains));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Digest = ComputeDigest();
    }
    public SemanticVersion FormatVersion { get; }
    public RulesetKey Key { get; }
    public string DisplayName { get; }
    public AlgorithmCatalog Algorithms { get; }
    public RngDomainCatalog RngDomains { get; }
    public ImmutableConfiguration Configuration { get; }
    public Sha256Digest Digest { get; }
    public bool Equals(RulesetDescriptor? other) => other is not null && FormatVersion == other.FormatVersion && Key == other.Key && DisplayName == other.DisplayName && Algorithms.Equals(other.Algorithms) && RngDomains.Equals(other.RngDomains) && Configuration.Equals(other.Configuration);
    public override bool Equals(object? obj) => obj is RulesetDescriptor other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();
    private Sha256Digest ComputeDigest() { using CanonicalHashWriter writer = new(); writer.WriteString(DigestDomainMarker); writer.WriteString(FormatVersion.ToString()); writer.WriteString(Key.Id.ToString()); writer.WriteString(Key.Version.ToString()); writer.WriteString(DisplayName); writer.WriteDigest(Algorithms.Digest); writer.WriteDigest(RngDomains.Digest); writer.WriteDigest(Configuration.Digest); return writer.FinalizeDigest(); }
    internal static RulesetDescriptor CreateValidated(SemanticVersion format, RulesetKey key, string display, AlgorithmCatalog algorithms, RngDomainCatalog domains, ImmutableConfiguration config, Sha256Digest digest) { RulesetDescriptor value = new(format, key, display, algorithms, domains, config); return value.Digest == digest ? value : throw new JsonException("Ruleset descriptor digest mismatch."); }
    private static void ValidateDisplayName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int scalarCount = 0; ReadOnlySpan<char> remaining = value.AsSpan();
        while (!remaining.IsEmpty) { System.Buffers.OperationStatus status = System.Text.Rune.DecodeFromUtf16(remaining, out System.Text.Rune rune, out int consumed); if (status != System.Buffers.OperationStatus.Done) throw new ArgumentException("Ruleset display name must contain valid Unicode scalar content.", nameof(value)); scalarCount++; if (System.Text.Rune.IsControl(rune)) throw new ArgumentException("Ruleset display name cannot contain control characters.", nameof(value)); remaining = remaining[consumed..]; }
        if (scalarCount is < 1 or > 120 || value != value.Trim()) throw new ArgumentException("Ruleset display name must contain 1 through 120 scalars without surrounding whitespace.", nameof(value));
    }
}

[JsonConverter(typeof(RulesetRegistryJsonConverter))]
public sealed class RulesetRegistry : IEquatable<RulesetRegistry>
{
    public const string DigestDomainMarker = "ProjectEmergence.RulesetRegistry.v1";
    private readonly RulesetDescriptor[] _array;
    private readonly IReadOnlyList<RulesetDescriptor> _entries;
    public RulesetRegistry(IEnumerable<RulesetDescriptor> entries)
    {
        ArgumentNullException.ThrowIfNull(entries); RulesetDescriptor?[] source = entries.Cast<RulesetDescriptor?>().ToArray();
        if (source.Any(static x => x is null)) throw new ArgumentException("Ruleset descriptors cannot be null.", nameof(entries));
        _array = source.Select(static x => x!).OrderBy(static x => x.Key).ToArray();
        if (_array.Select(static x => x.Key).Distinct().Count() != _array.Length) throw new ArgumentException("Duplicate ruleset keys are not allowed.", nameof(entries));
        _entries = Array.AsReadOnly(_array); Digest = ComputeDigest(_array);
    }
    public IReadOnlyList<RulesetDescriptor> Entries => _entries;
    public Sha256Digest Digest { get; }
    public bool TryGet(RulesetKey key, out RulesetDescriptor? descriptor) { int low = 0, high = _array.Length - 1; while (low <= high) { int mid = low + ((high - low) / 2); int c = _array[mid].Key.CompareTo(key); if (c == 0) { descriptor = _array[mid]; return true; } if (c < 0) low = mid + 1; else high = mid - 1; } descriptor = null; return false; }
    public bool Equals(RulesetRegistry? other) => other is not null && _entries.SequenceEqual(other._entries);
    public override bool Equals(object? obj) => obj is RulesetRegistry other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();
    private static Sha256Digest ComputeDigest(IReadOnlyList<RulesetDescriptor> entries) { using CanonicalHashWriter writer = new(); writer.WriteString(DigestDomainMarker); writer.WriteUInt64((ulong)entries.Count); foreach (RulesetDescriptor descriptor in entries) { writer.WriteString(descriptor.Key.Id.ToString()); writer.WriteString(descriptor.Key.Version.ToString()); writer.WriteDigest(descriptor.Digest); } return writer.FinalizeDigest(); }
    internal static RulesetRegistry CreateValidated(IEnumerable<RulesetDescriptor> entries, Sha256Digest digest) { RulesetRegistry value = new(entries); return value.Digest == digest ? value : throw new JsonException("Ruleset registry digest mismatch."); }
}

public static class FoundationReferenceRuleset
{
    public const string FileName = "foundation-reference.ruleset.json";
    public static RulesetDescriptor Create() => new(
        new(1, 0, 0),
        new(RulesetId.FromUInt64(1), new(1, 0, 0)),
        "Project Emergence Foundation Reference",
        AlgorithmCatalog.Phase03,
        RngDomainCatalog.Phase03,
        new ImmutableConfiguration(new("foundation.ruleset"), new(1, 0, 0),
        [
            new(new("foundation.reference-mode"), ConfigurationValue.FromBoolean(true)),
            new(new("foundation.rng-policy"), ConfigurationValue.FromString("addressed")),
            new(new("foundation.strict-validation"), ConfigurationValue.FromBoolean(true)),
        ]));
}

internal static class StrictRulesetJson
{
    public static void ValidateDescriptor(JsonElement root)
    {
        Exact(root, "formatVersion", "key", "displayName", "algorithms", "rngDomains", "configuration", "digest");
        JsonElement algorithms = root.GetProperty("algorithms"); Exact(algorithms, "entries", "digest");
        JsonElement domains = root.GetProperty("rngDomains"); Exact(domains, "entries", "digest");
        JsonElement config = root.GetProperty("configuration"); Exact(config, "schemaId", "schemaVersion", "entries", "digest");
        foreach (JsonElement entry in config.GetProperty("entries").EnumerateArray()) { Exact(entry, "key", "value"); Exact(entry.GetProperty("value"), "kind", "value"); }
    }
    public static void Exact(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a JSON object.");
        HashSet<string> allowed = new(expected, StringComparer.Ordinal); HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject()) { if (!allowed.Contains(property.Name)) throw new JsonException($"Unknown property '{property.Name}'."); if (!seen.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}'."); }
        if (seen.Count != allowed.Count) throw new JsonException($"Missing required property '{allowed.First(x => !seen.Contains(x))}'.");
    }
}

internal sealed class RulesetKeyJsonConverter : JsonConverter<RulesetKey> { public override RulesetKey Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType == JsonTokenType.String ? RulesetKey.Parse(r.GetString()!) : throw new JsonException(); public override void Write(Utf8JsonWriter w, RulesetKey v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString()); }
internal sealed class RulesetDescriptorJsonConverter : JsonConverter<RulesetDescriptor>
{
    public override RulesetDescriptor Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref r); JsonElement x = document.RootElement; StrictRulesetJson.ValidateDescriptor(x);
        try { return RulesetDescriptor.CreateValidated(SemanticVersion.Parse(x.GetProperty("formatVersion").GetString()!), RulesetKey.Parse(x.GetProperty("key").GetString()!), x.GetProperty("displayName").GetString()!, JsonSerializer.Deserialize<AlgorithmCatalog>(x.GetProperty("algorithms"), o)!, JsonSerializer.Deserialize<RngDomainCatalog>(x.GetProperty("rngDomains"), o)!, JsonSerializer.Deserialize<ImmutableConfiguration>(x.GetProperty("configuration"), o)!, Sha256Digest.Parse(x.GetProperty("digest").GetString()!)); }
        catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException) { throw new JsonException("Invalid ruleset descriptor.", e); }
    }
    public override void Write(Utf8JsonWriter w, RulesetDescriptor v, JsonSerializerOptions o) { w.WriteStartObject(); w.WriteString("formatVersion", v.FormatVersion.ToString()); w.WriteString("key", v.Key.ToString()); w.WriteString("displayName", v.DisplayName); w.WritePropertyName("algorithms"); JsonSerializer.Serialize(w, v.Algorithms, o); w.WritePropertyName("rngDomains"); JsonSerializer.Serialize(w, v.RngDomains, o); w.WritePropertyName("configuration"); JsonSerializer.Serialize(w, v.Configuration, o); w.WriteString("digest", v.Digest.ToString()); w.WriteEndObject(); }
}
internal sealed class RulesetRegistryJsonConverter : JsonConverter<RulesetRegistry>
{
    public override RulesetRegistry Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) { using JsonDocument d = JsonDocument.ParseValue(ref r); JsonElement x = d.RootElement; StrictRulesetJson.Exact(x, "entries", "digest"); return RulesetRegistry.CreateValidated(x.GetProperty("entries").EnumerateArray().Select(y => JsonSerializer.Deserialize<RulesetDescriptor>(y, o)!), Sha256Digest.Parse(x.GetProperty("digest").GetString()!)); }
    public override void Write(Utf8JsonWriter w, RulesetRegistry v, JsonSerializerOptions o) { w.WriteStartObject(); w.WritePropertyName("entries"); w.WriteStartArray(); foreach (RulesetDescriptor x in v.Entries) JsonSerializer.Serialize(w, x, o); w.WriteEndArray(); w.WriteString("digest", v.Digest.ToString()); w.WriteEndObject(); }
}
