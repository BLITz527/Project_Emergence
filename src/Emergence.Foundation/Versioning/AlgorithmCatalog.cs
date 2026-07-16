using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;

namespace Emergence.Foundation.Versioning;

[JsonConverter(typeof(AlgorithmCatalogJsonConverter))]
public sealed class AlgorithmCatalog : IEquatable<AlgorithmCatalog>
{
    private const string DomainMarker = "ProjectEmergence.AlgorithmCatalog.v1";
    private readonly ReadOnlyCollection<AlgorithmReference> _entries;

    public AlgorithmCatalog(IEnumerable<AlgorithmReference> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        AlgorithmReference[] sorted = entries.OrderBy(static entry => entry.Id).ToArray();
        if (sorted.Any(static entry => entry.IsEmpty)) throw new ArgumentException("Default AlgorithmReference entries are not allowed.", nameof(entries));
        if (sorted.Select(static entry => entry.Id).Distinct().Count() != sorted.Length) throw new ArgumentException("Duplicate AlgorithmId entries are not allowed.", nameof(entries));
        _entries = Array.AsReadOnly(sorted);
        Digest = ComputeDigest(sorted);
    }

    public IReadOnlyList<AlgorithmReference> Entries => _entries;
    public Sha256Digest Digest { get; }

    public static AlgorithmCatalog Phase02 { get; } = new(
    [
        AlgorithmReference.Parse("foundation.canonical-hash@1.0.0"),
        AlgorithmReference.Parse("foundation.stable-id@1.0.0"),
        AlgorithmReference.Parse("foundation.logical-time@1.0.0"),
        AlgorithmReference.Parse("foundation.exact-quantity@1.0.0"),
        AlgorithmReference.Parse("foundation.immutable-configuration@1.0.0"),
    ]);

    public static AlgorithmCatalog Phase03 { get; } = new(
    [
        .. Phase02.Entries,
        AlgorithmReference.Parse("foundation.rng-seed@1.0.0"),
        AlgorithmReference.Parse("foundation.rng-addressed-sha256@1.0.0"),
        AlgorithmReference.Parse("foundation.rng-bounded-uint64@1.0.0"),
        AlgorithmReference.Parse("foundation.rng-domain-catalog@1.0.0"),
        AlgorithmReference.Parse("foundation.ruleset-manifest@1.0.0"),
        AlgorithmReference.Parse("foundation.ruleset-registry@1.0.0"),
    ]);

    public static AlgorithmCatalog Phase04 { get; } = new(
    [
        .. Phase03.Entries,
        AlgorithmReference.Parse("simulation.world-session@1.0.0"),
        AlgorithmReference.Parse("simulation.phase-graph@1.0.0"),
        AlgorithmReference.Parse("simulation.command-pipeline@1.0.0"),
        AlgorithmReference.Parse("simulation.event-id@1.0.0"),
        AlgorithmReference.Parse("simulation.event-commit@1.0.0"),
        AlgorithmReference.Parse("presentation.session-snapshot@1.0.0"),
    ]);

    public bool Equals(AlgorithmCatalog? other) => other is not null && _entries.SequenceEqual(other._entries);
    public override bool Equals(object? obj) => obj is AlgorithmCatalog other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private static Sha256Digest ComputeDigest(IReadOnlyList<AlgorithmReference> entries)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DomainMarker);
        writer.WriteUInt64(checked((ulong)entries.Count));
        foreach (AlgorithmReference entry in entries) { writer.WriteString(entry.Id.ToString()); writer.WriteString(entry.Version.ToString()); }
        return writer.FinalizeDigest();
    }

    internal static AlgorithmCatalog CreateValidated(IEnumerable<AlgorithmReference> entries, Sha256Digest expected)
    {
        AlgorithmCatalog catalog = new(entries);
        return catalog.Digest == expected ? catalog : throw new JsonException("Algorithm catalog digest mismatch.");
    }
}

internal sealed class AlgorithmCatalogJsonConverter : JsonConverter<AlgorithmCatalog>
{
    public override AlgorithmCatalog Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        AlgorithmReference[] entries = root.GetProperty("entries").EnumerateArray().Select(element => AlgorithmReference.Parse(element.GetString()!)).ToArray();
        Sha256Digest digest = Sha256Digest.Parse(root.GetProperty("digest").GetString()!);
        return AlgorithmCatalog.CreateValidated(entries, digest);
    }

    public override void Write(Utf8JsonWriter writer, AlgorithmCatalog value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("entries"); writer.WriteStartArray(); foreach (AlgorithmReference entry in value.Entries) writer.WriteStringValue(entry.ToString()); writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }
}
