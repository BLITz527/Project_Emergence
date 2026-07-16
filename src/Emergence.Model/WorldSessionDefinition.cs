using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Versioning;

namespace Emergence.Model;

[JsonConverter(typeof(WorldSessionDefinitionJsonConverter))]
public sealed class WorldSessionDefinition : IEquatable<WorldSessionDefinition>
{
    public const string DigestDomainMarker = "ProjectEmergence.WorldSessionDefinition.v1";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    private readonly RulesetRegistry _registry;

    public WorldSessionDefinition(
        WorldIdentity worldIdentity,
        BranchIdentity branchIdentity,
        RulesetKey rulesetKey,
        RulesetRegistry rulesetRegistry,
        RngSeed256 rootSeed,
        AlgorithmCatalog runtimeAlgorithms,
        SchedulerGraph schedulerGraph)
    {
        WorldIdentity = worldIdentity ?? throw new ArgumentNullException(nameof(worldIdentity));
        BranchIdentity = branchIdentity ?? throw new ArgumentNullException(nameof(branchIdentity));
        if (worldIdentity.WorldId.IsEmpty) throw new ArgumentException("World identity cannot be empty.", nameof(worldIdentity));
        if (branchIdentity.WorldId.IsEmpty || branchIdentity.BranchId.IsEmpty) throw new ArgumentException("Branch identity cannot be empty.", nameof(branchIdentity));
        if (branchIdentity.WorldId != worldIdentity.WorldId) throw new ArgumentException("Branch identity must belong to the selected world.", nameof(branchIdentity));
        if (rulesetKey.IsEmpty) throw new ArgumentException("Ruleset key cannot be empty.", nameof(rulesetKey));
        _registry = rulesetRegistry ?? throw new ArgumentNullException(nameof(rulesetRegistry));
        if (!_registry.TryGet(rulesetKey, out RulesetDescriptor? descriptor) || descriptor is null) throw new ArgumentException("The selected ruleset is absent from the supplied registry.", nameof(rulesetKey));
        RuntimeAlgorithms = runtimeAlgorithms ?? throw new ArgumentNullException(nameof(runtimeAlgorithms));
        if (!RuntimeAlgorithms.Equals(AlgorithmCatalog.Phase04) || RuntimeAlgorithms.Digest != AlgorithmCatalog.Phase04.Digest)
            throw new ArgumentException("Runtime algorithms must exactly match the Phase 0.4 catalog.", nameof(runtimeAlgorithms));
        SchedulerGraph = schedulerGraph ?? throw new ArgumentNullException(nameof(schedulerGraph));

        FormatVersion = SupportedFormatVersion;
        RulesetKey = rulesetKey;
        SelectedRuleset = descriptor;
        RulesetDescriptorDigest = descriptor.Digest;
        RulesetRegistryDigest = _registry.Digest;
        RootSeed = rootSeed;
        SchedulerGraphDigest = SchedulerGraph.Digest;
        Digest = ComputeDigest();
    }

    public SemanticVersion FormatVersion { get; }
    public WorldIdentity WorldIdentity { get; }
    public BranchIdentity BranchIdentity { get; }
    public RulesetKey RulesetKey { get; }
    public RulesetDescriptor SelectedRuleset { get; }
    public Sha256Digest RulesetDescriptorDigest { get; }
    public Sha256Digest RulesetRegistryDigest { get; }
    public RngSeed256 RootSeed { get; }
    public AlgorithmCatalog RuntimeAlgorithms { get; }
    public SchedulerGraph SchedulerGraph { get; }
    public Sha256Digest SchedulerGraphDigest { get; }
    public Sha256Digest Digest { get; }
    internal RulesetRegistry RulesetRegistry => _registry;

    public bool Equals(WorldSessionDefinition? other) => other is not null
        && FormatVersion == other.FormatVersion
        && WorldIdentity == other.WorldIdentity
        && BranchIdentity == other.BranchIdentity
        && RulesetKey == other.RulesetKey
        && RulesetDescriptorDigest == other.RulesetDescriptorDigest
        && RulesetRegistryDigest == other.RulesetRegistryDigest
        && RootSeed == other.RootSeed
        && RuntimeAlgorithms.Equals(other.RuntimeAlgorithms)
        && SchedulerGraph.Equals(other.SchedulerGraph)
        && Digest == other.Digest;
    public override bool Equals(object? obj) => obj is WorldSessionDefinition other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputeDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteString(WorldIdentity.WorldId.ToString());
        writer.WriteString(BranchIdentity.BranchId.ToString());
        writer.WriteString(RulesetKey.ToString());
        writer.WriteDigest(RulesetDescriptorDigest);
        writer.WriteDigest(RulesetRegistryDigest);
        writer.WriteBytes(RootSeed.ToByteArray());
        writer.WriteDigest(RuntimeAlgorithms.Digest);
        writer.WriteDigest(SchedulerGraphDigest);
        return writer.FinalizeDigest();
    }

    internal static WorldSessionDefinition CreateValidated(
        WorldIdentity worldIdentity,
        BranchIdentity branchIdentity,
        RulesetKey rulesetKey,
        RulesetRegistry registry,
        RngSeed256 rootSeed,
        AlgorithmCatalog runtimeAlgorithms,
        SchedulerGraph schedulerGraph,
        Sha256Digest descriptorDigest,
        Sha256Digest registryDigest,
        Sha256Digest graphDigest,
        Sha256Digest expectedDigest)
    {
        WorldSessionDefinition definition = new(worldIdentity, branchIdentity, rulesetKey, registry, rootSeed, runtimeAlgorithms, schedulerGraph);
        if (definition.RulesetDescriptorDigest != descriptorDigest) throw new JsonException("World-session ruleset descriptor digest mismatch.");
        if (definition.RulesetRegistryDigest != registryDigest) throw new JsonException("World-session ruleset registry digest mismatch.");
        if (definition.SchedulerGraphDigest != graphDigest) throw new JsonException("World-session scheduler graph digest mismatch.");
        return definition.Digest == expectedDigest ? definition : throw new JsonException("World-session definition digest mismatch.");
    }
}

internal sealed class WorldSessionDefinitionJsonConverter : JsonConverter<WorldSessionDefinition>
{
    private static readonly string[] Properties =
    [
        "formatVersion", "worldIdentity", "branchIdentity", "rulesetKey", "rulesetDescriptorDigest",
        "rulesetRegistry", "rulesetRegistryDigest", "rootSeed", "runtimeAlgorithms", "schedulerGraph",
        "schedulerGraphDigest", "digest",
    ];

    public override WorldSessionDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        Exact(root);
        try
        {
            SemanticVersion format = SemanticVersion.Parse(root.GetProperty("formatVersion").GetString()!);
            if (format != WorldSessionDefinition.SupportedFormatVersion) throw new JsonException("World-session definition format must be exactly 1.0.0.");
            return WorldSessionDefinition.CreateValidated(
                JsonSerializer.Deserialize<WorldIdentity>(root.GetProperty("worldIdentity"), options) ?? throw new JsonException("Missing world identity."),
                JsonSerializer.Deserialize<BranchIdentity>(root.GetProperty("branchIdentity"), options) ?? throw new JsonException("Missing branch identity."),
                RulesetKey.Parse(root.GetProperty("rulesetKey").GetString()!),
                JsonSerializer.Deserialize<RulesetRegistry>(root.GetProperty("rulesetRegistry"), options) ?? throw new JsonException("Missing ruleset registry."),
                JsonSerializer.Deserialize<RngSeed256>(root.GetProperty("rootSeed"), options),
                JsonSerializer.Deserialize<AlgorithmCatalog>(root.GetProperty("runtimeAlgorithms"), options) ?? throw new JsonException("Missing runtime algorithms."),
                JsonSerializer.Deserialize<SchedulerGraph>(root.GetProperty("schedulerGraph"), options) ?? throw new JsonException("Missing scheduler graph."),
                Sha256Digest.Parse(root.GetProperty("rulesetDescriptorDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("rulesetRegistryDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("schedulerGraphDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            throw new JsonException("Invalid world-session definition.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, WorldSessionDefinition value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("formatVersion", value.FormatVersion.ToString());
        writer.WritePropertyName("worldIdentity"); JsonSerializer.Serialize(writer, value.WorldIdentity, options);
        writer.WritePropertyName("branchIdentity"); JsonSerializer.Serialize(writer, value.BranchIdentity, options);
        writer.WriteString("rulesetKey", value.RulesetKey.ToString());
        writer.WriteString("rulesetDescriptorDigest", value.RulesetDescriptorDigest.ToString());
        writer.WritePropertyName("rulesetRegistry"); JsonSerializer.Serialize(writer, value.RulesetRegistry, options);
        writer.WriteString("rulesetRegistryDigest", value.RulesetRegistryDigest.ToString());
        writer.WritePropertyName("rootSeed"); JsonSerializer.Serialize(writer, value.RootSeed, options);
        writer.WritePropertyName("runtimeAlgorithms"); JsonSerializer.Serialize(writer, value.RuntimeAlgorithms, options);
        writer.WritePropertyName("schedulerGraph"); JsonSerializer.Serialize(writer, value.SchedulerGraph, options);
        writer.WriteString("schedulerGraphDigest", value.SchedulerGraphDigest.ToString());
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }

    private static void Exact(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected world-session definition object.");
        HashSet<string> required = new(Properties, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!required.Contains(property.Name) || !seen.Add(property.Name)) throw new JsonException($"Unexpected or duplicate property '{property.Name}'.");
        }
        if (!required.SetEquals(seen)) throw new JsonException("World-session definition is missing required properties.");
    }
}
