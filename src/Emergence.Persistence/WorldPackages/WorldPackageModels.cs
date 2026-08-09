using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Text;
using Emergence.Foundation.Versioning;
using Emergence.Model;

namespace Emergence.Persistence.WorldPackages;

public static class WorldPackageTechnicalLimits
{
    public const long MaxPackageBytes = 67_108_864;
    public const int ExactEntryCount = 3;
    public const int MaxManifestBytes = 1_048_576;
    public const int MaxDefinitionBytes = 8_388_608;
    public const int MaxSnapshotBytes = 50_331_648;
    public const long MaxTotalUncompressedBytes = 59_768_832;
    public const int MaxJsonDepth = 64;
    public const long MaxEnvironmentPackageBytes = 268_435_456;
    public const int MaxEnvironmentManifestBytes = 2_097_152;
    public const int MaxEnvironmentDefinitionBytes = 16_777_216;
    public const int MaxEnvironmentSnapshotBytes = 16_777_216;
    public const int MaxFieldChunkBytes = 8_388_608;
    public const long MaxTotalFieldChunkBytes = 201_326_592;
    public const int MaxEnvironmentPackageEntries = 4_099;
}

public sealed class WorldPackageFileEntry : IEquatable<WorldPackageFileEntry>
{
    public WorldPackageFileEntry(string path, ulong uncompressedByteLength, Sha256Digest sha256)
    {
        if (path is not (WorldPackagePaths.DefinitionEntry or WorldPackagePaths.SnapshotEntry)
            && !WorldPackagePaths.IsCanonicalFieldChunkPath(path))
            throw new ArgumentException("World package data path is not canonical.", nameof(path));
        Path = path;
        UncompressedByteLength = uncompressedByteLength;
        Sha256 = sha256;
    }

    public string Path { get; }
    public ulong UncompressedByteLength { get; }
    public Sha256Digest Sha256 { get; }

    public bool Equals(WorldPackageFileEntry? other) => other is not null
        && Path == other.Path && UncompressedByteLength == other.UncompressedByteLength && Sha256 == other.Sha256;
    public override bool Equals(object? obj) => obj is WorldPackageFileEntry other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Path, UncompressedByteLength, Sha256);
}

[JsonConverter(typeof(WorldPackageManifestJsonConverter))]
public sealed class WorldPackageManifest : IEquatable<WorldPackageManifest>
{
    public const string IdentityDigestDomainMarker = "ProjectEmergence.WorldPackageIdentity.v1";
    public const string ManifestDigestDomainMarker = "ProjectEmergence.WorldPackageManifest.v1";
    public const string EnvironmentIdentityDigestDomainMarker = "ProjectEmergence.WorldPackageIdentity.v2";
    public const string EnvironmentManifestDigestDomainMarker = "ProjectEmergence.WorldPackageManifest.v2";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    public static SemanticVersion EnvironmentFormatVersion { get; } = new(2, 0, 0);
    private readonly ReadOnlyCollection<WorldPackageFileEntry> _entries;

    private WorldPackageManifest(
        SemanticVersion formatVersion,
        WorldId worldId,
        BranchId branchId,
        Sha256Digest sessionDefinitionDigest,
        Sha256Digest snapshotDigest,
        Sha256Digest stateDigest,
        Sha256Digest rulesetRegistryDigest,
        Sha256Digest runtimeAlgorithmCatalogDigest,
        Sha256Digest? environmentDefinitionDigest,
        Sha256Digest? environmentStateDigest,
        IEnumerable<WorldPackageFileEntry> entries)
    {
        if (worldId.IsEmpty) throw new ArgumentException("Package world ID cannot be empty.", nameof(worldId));
        if (branchId.IsEmpty) throw new ArgumentException("Package branch ID cannot be empty.", nameof(branchId));
        ArgumentNullException.ThrowIfNull(entries);
        WorldPackageFileEntry?[] source = entries.Cast<WorldPackageFileEntry?>().ToArray();
        bool environment = formatVersion == EnvironmentFormatVersion;
        if (formatVersion != SupportedFormatVersion && !environment)
            throw new ArgumentException("Unsupported world package manifest format.", nameof(formatVersion));
        if (source.Any(static item => item is null)
            || (!environment && source.Length != 2)
            || (environment && (source.Length < 3 || source.Length > WorldPackageTechnicalLimits.MaxEnvironmentPackageEntries - 1)))
            throw new ArgumentException("World package manifest data-entry count is invalid.", nameof(entries));
        WorldPackageFileEntry[] copy = source.Select(static item => item!).ToArray();
        if (copy[0].Path != WorldPackagePaths.DefinitionEntry || copy[1].Path != WorldPackagePaths.SnapshotEntry)
            throw new ArgumentException("World package manifest entries must be definition.json then snapshot.json.", nameof(entries));
        if (!environment && copy.Skip(2).Any()) throw new ArgumentException("V1 packages cannot contain field chunks.", nameof(entries));
        if (environment && (environmentDefinitionDigest is null || environmentStateDigest is null
            || copy.Skip(2).Any(entry => !WorldPackagePaths.IsCanonicalFieldChunkPath(entry.Path))
            || !copy.Skip(2).Select(static entry => entry.Path).SequenceEqual(copy.Skip(2).Select(static entry => entry.Path).Order(StringComparer.Ordinal))))
            throw new ArgumentException("V2 packages require environment digests and canonical field-chunk order.", nameof(entries));
        if (!environment && (environmentDefinitionDigest is not null || environmentStateDigest is not null))
            throw new ArgumentException("V1 packages cannot carry environment digests.", nameof(entries));
        if (copy.Select(static item => item.Path).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Duplicate world package manifest entries are not allowed.", nameof(entries));

        FormatVersion = formatVersion;
        WorldId = worldId;
        BranchId = branchId;
        SessionDefinitionDigest = sessionDefinitionDigest;
        SnapshotDigest = snapshotDigest;
        StateDigest = stateDigest;
        RulesetRegistryDigest = rulesetRegistryDigest;
        RuntimeAlgorithmCatalogDigest = runtimeAlgorithmCatalogDigest;
        EnvironmentDefinitionDigest = environmentDefinitionDigest;
        EnvironmentStateDigest = environmentStateDigest;
        _entries = Array.AsReadOnly(copy);
        PackageIdentityDigest = ComputePackageIdentityDigest();
        Digest = ComputeManifestDigest();
    }

    public SemanticVersion FormatVersion { get; }
    public WorldId WorldId { get; }
    public BranchId BranchId { get; }
    public Sha256Digest SessionDefinitionDigest { get; }
    public Sha256Digest SnapshotDigest { get; }
    public Sha256Digest StateDigest { get; }
    public Sha256Digest RulesetRegistryDigest { get; }
    public Sha256Digest RuntimeAlgorithmCatalogDigest { get; }
    public Sha256Digest? EnvironmentDefinitionDigest { get; }
    public Sha256Digest? EnvironmentStateDigest { get; }
    public Sha256Digest PackageIdentityDigest { get; }
    public IReadOnlyList<WorldPackageFileEntry> Entries => _entries;
    public Sha256Digest Digest { get; }

    public static WorldPackageManifest Create(WorldSessionSnapshot snapshot, IEnumerable<WorldPackageFileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(
            snapshot.EnvironmentState is null ? SupportedFormatVersion : EnvironmentFormatVersion,
            snapshot.Definition.WorldIdentity.WorldId,
            snapshot.Definition.BranchIdentity.BranchId,
            snapshot.Definition.Digest,
            snapshot.Digest,
            snapshot.StateDigest,
            snapshot.Definition.RulesetRegistryDigest,
            snapshot.Definition.RuntimeAlgorithms.Digest,
            snapshot.Definition.EnvironmentDefinitionDigest,
            snapshot.EnvironmentState?.Digest,
            entries);
    }

    public bool Equals(WorldPackageManifest? other) => other is not null
        && FormatVersion == other.FormatVersion
        && WorldId == other.WorldId
        && BranchId == other.BranchId
        && SessionDefinitionDigest == other.SessionDefinitionDigest
        && SnapshotDigest == other.SnapshotDigest
        && StateDigest == other.StateDigest
        && RulesetRegistryDigest == other.RulesetRegistryDigest
        && RuntimeAlgorithmCatalogDigest == other.RuntimeAlgorithmCatalogDigest
        && EnvironmentDefinitionDigest == other.EnvironmentDefinitionDigest
        && EnvironmentStateDigest == other.EnvironmentStateDigest
        && PackageIdentityDigest == other.PackageIdentityDigest
        && Entries.SequenceEqual(other.Entries)
        && Digest == other.Digest;
    public override bool Equals(object? obj) => obj is WorldPackageManifest other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputePackageIdentityDigest()
    {
        using CanonicalHashWriter writer = new();
        bool environment = FormatVersion == EnvironmentFormatVersion;
        writer.WriteString(environment ? EnvironmentIdentityDigestDomainMarker : IdentityDigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteString(WorldId.ToString());
        writer.WriteString(BranchId.ToString());
        writer.WriteDigest(SessionDefinitionDigest);
        writer.WriteDigest(SnapshotDigest);
        writer.WriteDigest(StateDigest);
        writer.WriteDigest(RulesetRegistryDigest);
        writer.WriteDigest(RuntimeAlgorithmCatalogDigest);
        if (environment)
        {
            writer.WriteDigest(EnvironmentDefinitionDigest!.Value);
            writer.WriteDigest(EnvironmentStateDigest!.Value);
        }
        writer.WriteUInt64(checked((ulong)Entries.Count));
        foreach (WorldPackageFileEntry entry in Entries) writer.WriteString(entry.Path);
        return writer.FinalizeDigest();
    }

    private Sha256Digest ComputeManifestDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(FormatVersion == EnvironmentFormatVersion ? EnvironmentManifestDigestDomainMarker : ManifestDigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteDigest(PackageIdentityDigest);
        writer.WriteUInt64(checked((ulong)Entries.Count));
        foreach (WorldPackageFileEntry entry in Entries)
        {
            writer.WriteString(entry.Path);
            writer.WriteUInt64(entry.UncompressedByteLength);
            writer.WriteDigest(entry.Sha256);
        }
        return writer.FinalizeDigest();
    }

    internal static WorldPackageManifest CreateValidated(
        SemanticVersion formatVersion,
        WorldId worldId,
        BranchId branchId,
        Sha256Digest sessionDefinitionDigest,
        Sha256Digest snapshotDigest,
        Sha256Digest stateDigest,
        Sha256Digest rulesetRegistryDigest,
        Sha256Digest runtimeAlgorithmCatalogDigest,
        Sha256Digest packageIdentityDigest,
        IEnumerable<WorldPackageFileEntry> entries,
        Sha256Digest digest)
    {
        if (formatVersion != SupportedFormatVersion) throw new JsonException($"Unsupported world package manifest format '{formatVersion}'.");
        WorldPackageManifest manifest = new(formatVersion, worldId, branchId, sessionDefinitionDigest, snapshotDigest, stateDigest,
            rulesetRegistryDigest, runtimeAlgorithmCatalogDigest, null, null, entries);
        if (manifest.PackageIdentityDigest != packageIdentityDigest) throw new JsonException("World package identity digest mismatch.");
        return manifest.Digest == digest ? manifest : throw new JsonException("World package manifest digest mismatch.");
    }

    internal static WorldPackageManifest CreateEnvironmentValidated(
        SemanticVersion formatVersion,
        WorldId worldId,
        BranchId branchId,
        Sha256Digest sessionDefinitionDigest,
        Sha256Digest snapshotDigest,
        Sha256Digest stateDigest,
        Sha256Digest rulesetRegistryDigest,
        Sha256Digest runtimeAlgorithmCatalogDigest,
        Sha256Digest environmentDefinitionDigest,
        Sha256Digest environmentStateDigest,
        Sha256Digest packageIdentityDigest,
        IEnumerable<WorldPackageFileEntry> entries,
        Sha256Digest digest)
    {
        if (formatVersion != EnvironmentFormatVersion) throw new JsonException($"Unsupported environment package manifest format '{formatVersion}'.");
        WorldPackageManifest manifest = new(formatVersion, worldId, branchId, sessionDefinitionDigest, snapshotDigest, stateDigest,
            rulesetRegistryDigest, runtimeAlgorithmCatalogDigest, environmentDefinitionDigest, environmentStateDigest, entries);
        if (manifest.PackageIdentityDigest != packageIdentityDigest) throw new JsonException("World package identity digest mismatch.");
        return manifest.Digest == digest ? manifest : throw new JsonException("World package manifest digest mismatch.");
    }
}

public sealed class WorldPackageDocument
{
    internal WorldPackageDocument(
        WorldPackageManifest manifest,
        WorldSessionDefinition definition,
        WorldSessionSnapshot snapshot,
        string packagePath)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        PackagePath = packagePath ?? throw new ArgumentNullException(nameof(packagePath));
        if (!snapshot.Definition.Equals(definition)) throw new ArgumentException("Snapshot definition does not match definition entry.", nameof(snapshot));
        if (manifest.WorldId != definition.WorldIdentity.WorldId || manifest.BranchId != definition.BranchIdentity.BranchId
            || manifest.SessionDefinitionDigest != definition.Digest || manifest.SnapshotDigest != snapshot.Digest
            || manifest.StateDigest != snapshot.StateDigest || manifest.RulesetRegistryDigest != definition.RulesetRegistryDigest
            || manifest.RuntimeAlgorithmCatalogDigest != definition.RuntimeAlgorithms.Digest)
            throw new ArgumentException("World package manifest does not match the definition and snapshot documents.", nameof(manifest));
        if (snapshot.EnvironmentState is not null
            && (manifest.FormatVersion != WorldPackageManifest.EnvironmentFormatVersion
                || manifest.EnvironmentDefinitionDigest != definition.EnvironmentDefinitionDigest
                || manifest.EnvironmentStateDigest != snapshot.EnvironmentState.Digest))
            throw new ArgumentException("Environment package manifest does not match the definition and snapshot state.", nameof(manifest));
    }

    public WorldPackageManifest Manifest { get; }
    public WorldSessionDefinition Definition { get; }
    public WorldSessionSnapshot Snapshot { get; }
    public string PackagePath { get; }
}

public sealed record WorldPackageIssue(string Code, string Summary, string Detail);

public sealed record WorldPackageLoadResult(
    bool Success,
    WorldPackageDocument? Document,
    IReadOnlyList<WorldPackageIssue> Issues);

public sealed record WorldPackageSaveResult(
    bool Success,
    string PackagePath,
    string PackageIdentityDigest,
    string ManifestDigest,
    long PackageBytes,
    IReadOnlyList<WorldPackageIssue> Issues);

internal static class WorldPackagePaths
{
    public const string Extension = ".emergence-world";
    public const string DefinitionEntry = "definition.json";
    public const string SnapshotEntry = "snapshot.json";
    public const string ManifestEntry = "package-manifest.json";
    public static readonly string[] EntryOrder = [DefinitionEntry, SnapshotEntry, ManifestEntry];

    public static bool IsCanonicalFieldChunkPath(string? path)
    {
        if (path is null || path.Length != 61 || !path.StartsWith("regions/", StringComparison.Ordinal)
            || !path.AsSpan(40, 8).SequenceEqual("/fields/")) return false;
        ReadOnlySpan<char> region = path.AsSpan(8, 32);
        ReadOnlySpan<char> y = path.AsSpan(48, 4);
        ReadOnlySpan<char> x = path.AsSpan(53, 4);
        return region.IndexOfAnyExcept("0123456789abcdef") < 0
            && y.IndexOfAnyExcept("0123456789") < 0
            && x.IndexOfAnyExcept("0123456789") < 0
            && path[52] == '-' && path.AsSpan(57).SequenceEqual(".bin");
    }
}

internal static class WorldPackageJson
{
    public static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = WorldPackageTechnicalLimits.MaxJsonDepth,
    };

    public static byte[] SerializeCompact<T>(T value) => StrictUtf8.GetBytes(JsonSerializer.Serialize(value, Emergence.Foundation.JsonDefaults.Compact));

    public static void Exact(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a JSON object.");
        HashSet<string> allowed = new(expected, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
                throw new JsonException($"Unexpected or duplicate property '{property.Name}'.");
        }
        if (!allowed.SetEquals(seen)) throw new JsonException("JSON object is missing required properties.");
    }
}

internal sealed class WorldPackageManifestJsonConverter : JsonConverter<WorldPackageManifest>
{
    private static readonly string[] V1Properties =
    [
        "formatVersion", "worldId", "branchId", "sessionDefinitionDigest", "snapshotDigest", "stateDigest",
        "rulesetRegistryDigest", "runtimeAlgorithmCatalogDigest", "packageIdentityDigest", "entries", "digest",
    ];
    private static readonly string[] V2Properties =
    [
        "formatVersion", "worldId", "branchId", "sessionDefinitionDigest", "snapshotDigest", "stateDigest",
        "rulesetRegistryDigest", "runtimeAlgorithmCatalogDigest", "environmentDefinitionDigest", "environmentStateDigest",
        "packageIdentityDigest", "entries", "digest",
    ];

    public override WorldPackageManifest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        try
        {
            SemanticVersion formatVersion = SemanticVersion.Parse(root.GetProperty("formatVersion").GetString()!);
            WorldPackageJson.Exact(root, formatVersion == WorldPackageManifest.SupportedFormatVersion ? V1Properties : V2Properties);
            WorldPackageFileEntry[] entries = root.GetProperty("entries").EnumerateArray().Select(ParseEntry).ToArray();
            if (formatVersion == WorldPackageManifest.EnvironmentFormatVersion)
                return WorldPackageManifest.CreateEnvironmentValidated(
                    formatVersion,
                    WorldId.Parse(root.GetProperty("worldId").GetString()!),
                    BranchId.Parse(root.GetProperty("branchId").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("sessionDefinitionDigest").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("snapshotDigest").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("stateDigest").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("rulesetRegistryDigest").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("runtimeAlgorithmCatalogDigest").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("environmentDefinitionDigest").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("environmentStateDigest").GetString()!),
                    Sha256Digest.Parse(root.GetProperty("packageIdentityDigest").GetString()!),
                    entries,
                    Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
            return WorldPackageManifest.CreateValidated(
                formatVersion,
                WorldId.Parse(root.GetProperty("worldId").GetString()!),
                BranchId.Parse(root.GetProperty("branchId").GetString()!),
                Sha256Digest.Parse(root.GetProperty("sessionDefinitionDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("snapshotDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("stateDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("rulesetRegistryDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("runtimeAlgorithmCatalogDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("packageIdentityDigest").GetString()!),
                entries,
                Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
        }
        catch (JsonException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException or KeyNotFoundException)
        {
            throw new JsonException("Invalid world package manifest.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, WorldPackageManifest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("formatVersion", value.FormatVersion.ToString());
        writer.WriteString("worldId", value.WorldId.ToString());
        writer.WriteString("branchId", value.BranchId.ToString());
        writer.WriteString("sessionDefinitionDigest", value.SessionDefinitionDigest.ToString());
        writer.WriteString("snapshotDigest", value.SnapshotDigest.ToString());
        writer.WriteString("stateDigest", value.StateDigest.ToString());
        writer.WriteString("rulesetRegistryDigest", value.RulesetRegistryDigest.ToString());
        writer.WriteString("runtimeAlgorithmCatalogDigest", value.RuntimeAlgorithmCatalogDigest.ToString());
        if (value.FormatVersion == WorldPackageManifest.EnvironmentFormatVersion)
        {
            writer.WriteString("environmentDefinitionDigest", value.EnvironmentDefinitionDigest!.Value.ToString());
            writer.WriteString("environmentStateDigest", value.EnvironmentStateDigest!.Value.ToString());
        }
        writer.WriteString("packageIdentityDigest", value.PackageIdentityDigest.ToString());
        writer.WritePropertyName("entries");
        writer.WriteStartArray();
        foreach (WorldPackageFileEntry entry in value.Entries)
        {
            writer.WriteStartObject();
            writer.WriteString("path", entry.Path);
            writer.WriteString("uncompressedByteLength", entry.UncompressedByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteString("sha256", entry.Sha256.ToString());
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }

    private static WorldPackageFileEntry ParseEntry(JsonElement element)
    {
        WorldPackageJson.Exact(element, "path", "uncompressedByteLength", "sha256");
        string length = element.GetProperty("uncompressedByteLength").GetString() ?? throw new JsonException("Missing entry length.");
        if (length.Length == 0 || (length.Length > 1 && length[0] == '0')
            || !ulong.TryParse(length, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong parsed))
            throw new JsonException("Entry length must be a canonical UInt64 string.");
        return new WorldPackageFileEntry(
            element.GetProperty("path").GetString() ?? throw new JsonException("Missing entry path."),
            parsed,
            Sha256Digest.Parse(element.GetProperty("sha256").GetString()!));
    }
}
