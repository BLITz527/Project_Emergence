using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Versioning;
using Emergence.Model.Environment;

namespace Emergence.Model;

[JsonConverter(typeof(WorldSessionDefinitionJsonConverter))]
public sealed class WorldSessionDefinition : IEquatable<WorldSessionDefinition>
{
    public const string DigestDomainMarker = "ProjectEmergence.WorldSessionDefinition.v1";
    public const string V2DigestDomainMarker = "ProjectEmergence.WorldSessionDefinition.v2";
    public const string V3DigestDomainMarker = "ProjectEmergence.WorldSessionDefinition.v3";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    public static SemanticVersion SaveableFormatVersion { get; } = new(2, 0, 0);
    public static SemanticVersion EnvironmentFormatVersion { get; } = new(3, 0, 0);
    private readonly RulesetRegistry _registry;

    public WorldSessionDefinition(
        WorldIdentity worldIdentity,
        BranchIdentity branchIdentity,
        RulesetKey rulesetKey,
        RulesetRegistry rulesetRegistry,
        RngSeed256 rootSeed,
        AlgorithmCatalog runtimeAlgorithms,
        SchedulerGraph schedulerGraph)
        : this(worldIdentity, branchIdentity, rulesetKey, rulesetRegistry, rootSeed, runtimeAlgorithms, schedulerGraph, null, null, SupportedFormatVersion)
    {
    }

    public WorldSessionDefinition(
        WorldIdentity worldIdentity,
        BranchIdentity branchIdentity,
        RulesetKey rulesetKey,
        RulesetRegistry rulesetRegistry,
        RngSeed256 rootSeed,
        AlgorithmCatalog runtimeAlgorithms,
        SchedulerGraph schedulerGraph,
        CommandProcessorCatalog commandProcessorCatalog)
        : this(worldIdentity, branchIdentity, rulesetKey, rulesetRegistry, rootSeed, runtimeAlgorithms, schedulerGraph, commandProcessorCatalog, null, SaveableFormatVersion)
    {
    }

    public WorldSessionDefinition(
        WorldIdentity worldIdentity,
        BranchIdentity branchIdentity,
        RulesetKey rulesetKey,
        RulesetRegistry rulesetRegistry,
        RngSeed256 rootSeed,
        AlgorithmCatalog runtimeAlgorithms,
        SchedulerGraph schedulerGraph,
        CommandProcessorCatalog commandProcessorCatalog,
        EnvironmentDefinition environmentDefinition)
        : this(worldIdentity, branchIdentity, rulesetKey, rulesetRegistry, rootSeed, runtimeAlgorithms, schedulerGraph,
            commandProcessorCatalog, environmentDefinition, EnvironmentFormatVersion)
    {
    }

    private WorldSessionDefinition(
        WorldIdentity worldIdentity,
        BranchIdentity branchIdentity,
        RulesetKey rulesetKey,
        RulesetRegistry rulesetRegistry,
        RngSeed256 rootSeed,
        AlgorithmCatalog runtimeAlgorithms,
        SchedulerGraph schedulerGraph,
        CommandProcessorCatalog? commandProcessorCatalog,
        EnvironmentDefinition? environmentDefinition,
        SemanticVersion formatVersion)
    {
        WorldIdentity = worldIdentity ?? throw new ArgumentNullException(nameof(worldIdentity));
        BranchIdentity = branchIdentity ?? throw new ArgumentNullException(nameof(branchIdentity));
        if (worldIdentity.WorldId.IsEmpty) throw new ArgumentException("World identity cannot be empty.", nameof(worldIdentity));
        if (branchIdentity.WorldId.IsEmpty || branchIdentity.BranchId.IsEmpty) throw new ArgumentException("Branch identity cannot be empty.", nameof(branchIdentity));
        if (branchIdentity.WorldId != worldIdentity.WorldId) throw new ArgumentException("Branch identity must belong to the selected world.", nameof(branchIdentity));
        if (rulesetKey.IsEmpty) throw new ArgumentException("Ruleset key cannot be empty.", nameof(rulesetKey));
        _registry = rulesetRegistry ?? throw new ArgumentNullException(nameof(rulesetRegistry));
        if (!_registry.TryGet(rulesetKey, out RulesetDescriptor? descriptor) || descriptor is null)
            throw new ArgumentException("The selected ruleset is absent from the supplied registry.", nameof(rulesetKey));

        RuntimeAlgorithms = runtimeAlgorithms ?? throw new ArgumentNullException(nameof(runtimeAlgorithms));
        if (formatVersion == SupportedFormatVersion)
        {
            if (!RuntimeAlgorithms.Equals(AlgorithmCatalog.Phase04) || RuntimeAlgorithms.Digest != AlgorithmCatalog.Phase04.Digest)
                throw new ArgumentException("Runtime algorithms must exactly match the Phase 0.4 catalog.", nameof(runtimeAlgorithms));
            if (commandProcessorCatalog is not null)
                throw new ArgumentException("Definition format 1.0.0 cannot contain a command processor catalog.", nameof(commandProcessorCatalog));
        }
        else if (formatVersion == SaveableFormatVersion)
        {
            if (!RuntimeAlgorithms.Equals(AlgorithmCatalog.Phase05) || RuntimeAlgorithms.Digest != AlgorithmCatalog.Phase05.Digest)
                throw new ArgumentException("Runtime algorithms must exactly match the Phase 0.5 catalog.", nameof(runtimeAlgorithms));
            CommandProcessorCatalog = commandProcessorCatalog ?? throw new ArgumentNullException(nameof(commandProcessorCatalog));
            CommandProcessorCatalogDigest = CommandProcessorCatalog.Digest;
        }
        else if (formatVersion == EnvironmentFormatVersion)
        {
            if (!RuntimeAlgorithms.Equals(AlgorithmCatalog.Phase11) || RuntimeAlgorithms.Digest != AlgorithmCatalog.Phase11.Digest)
                throw new ArgumentException("Runtime algorithms must exactly match the Phase 1.1 catalog.", nameof(runtimeAlgorithms));
            CommandProcessorCatalog = commandProcessorCatalog ?? throw new ArgumentNullException(nameof(commandProcessorCatalog));
            CommandProcessorCatalogDigest = CommandProcessorCatalog.Digest;
            EnvironmentDefinition = environmentDefinition ?? throw new ArgumentNullException(nameof(environmentDefinition));
            if (EnvironmentDefinition.Regions.Count != EnvironmentTechnicalLimits.MaxRegions)
                throw new ArgumentException("Phase 1.1 requires exactly one environment region.", nameof(environmentDefinition));
            EnvironmentDefinitionDigest = EnvironmentDefinition.Digest;
        }
        else
        {
            throw new ArgumentException("Unsupported world-session definition format.", nameof(formatVersion));
        }

        SchedulerGraph = schedulerGraph ?? throw new ArgumentNullException(nameof(schedulerGraph));
        FormatVersion = formatVersion;
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
    public CommandProcessorCatalog? CommandProcessorCatalog { get; }
    public Sha256Digest? CommandProcessorCatalogDigest { get; }
    public EnvironmentDefinition? EnvironmentDefinition { get; }
    public Sha256Digest? EnvironmentDefinitionDigest { get; }
    public Sha256Digest Digest { get; }
    [JsonIgnore] public bool IsSaveable => FormatVersion is var format && (format == SaveableFormatVersion || format == EnvironmentFormatVersion);
    [JsonIgnore] public bool HasEnvironment => FormatVersion == EnvironmentFormatVersion;
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
        && Equals(CommandProcessorCatalog, other.CommandProcessorCatalog)
        && CommandProcessorCatalogDigest == other.CommandProcessorCatalogDigest
        && Equals(EnvironmentDefinition, other.EnvironmentDefinition)
        && EnvironmentDefinitionDigest == other.EnvironmentDefinitionDigest
        && Digest == other.Digest;

    public override bool Equals(object? obj) => obj is WorldSessionDefinition other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputeDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(FormatVersion == SupportedFormatVersion ? DigestDomainMarker
            : FormatVersion == SaveableFormatVersion ? V2DigestDomainMarker : V3DigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteString(WorldIdentity.WorldId.ToString());
        writer.WriteString(BranchIdentity.BranchId.ToString());
        writer.WriteString(RulesetKey.ToString());
        writer.WriteDigest(RulesetDescriptorDigest);
        writer.WriteDigest(RulesetRegistryDigest);
        writer.WriteBytes(RootSeed.ToByteArray());
        writer.WriteDigest(RuntimeAlgorithms.Digest);
        writer.WriteDigest(SchedulerGraphDigest);
        if (FormatVersion != SupportedFormatVersion) writer.WriteDigest(CommandProcessorCatalogDigest!.Value);
        if (FormatVersion == EnvironmentFormatVersion) writer.WriteDigest(EnvironmentDefinitionDigest!.Value);
        return writer.FinalizeDigest();
    }

    internal static WorldSessionDefinition CreateValidated(
        SemanticVersion formatVersion,
        WorldIdentity worldIdentity,
        BranchIdentity branchIdentity,
        RulesetKey rulesetKey,
        RulesetRegistry registry,
        RngSeed256 rootSeed,
        AlgorithmCatalog runtimeAlgorithms,
        SchedulerGraph schedulerGraph,
        CommandProcessorCatalog? commandProcessorCatalog,
        EnvironmentDefinition? environmentDefinition,
        Sha256Digest descriptorDigest,
        Sha256Digest registryDigest,
        Sha256Digest graphDigest,
        Sha256Digest? commandProcessorCatalogDigest,
        Sha256Digest? environmentDefinitionDigest,
        Sha256Digest expectedDigest)
    {
        WorldSessionDefinition definition = formatVersion == SupportedFormatVersion
            ? new(worldIdentity, branchIdentity, rulesetKey, registry, rootSeed, runtimeAlgorithms, schedulerGraph)
            : formatVersion == SaveableFormatVersion
                ? new(worldIdentity, branchIdentity, rulesetKey, registry, rootSeed, runtimeAlgorithms, schedulerGraph,
                    commandProcessorCatalog ?? throw new JsonException("World-session V2 command processor catalog is missing."))
                : formatVersion == EnvironmentFormatVersion
                    ? new(worldIdentity, branchIdentity, rulesetKey, registry, rootSeed, runtimeAlgorithms, schedulerGraph,
                        commandProcessorCatalog ?? throw new JsonException("World-session V3 command processor catalog is missing."),
                        environmentDefinition ?? throw new JsonException("World-session V3 environment definition is missing."))
                : throw new JsonException($"Unsupported world-session definition format '{formatVersion}'.");
        if (definition.RulesetDescriptorDigest != descriptorDigest) throw new JsonException("World-session ruleset descriptor digest mismatch.");
        if (definition.RulesetRegistryDigest != registryDigest) throw new JsonException("World-session ruleset registry digest mismatch.");
        if (definition.SchedulerGraphDigest != graphDigest) throw new JsonException("World-session scheduler graph digest mismatch.");
        if (formatVersion != SupportedFormatVersion && definition.CommandProcessorCatalogDigest != commandProcessorCatalogDigest)
            throw new JsonException("World-session command processor catalog digest mismatch.");
        if (formatVersion == EnvironmentFormatVersion && definition.EnvironmentDefinitionDigest != environmentDefinitionDigest)
            throw new JsonException("World-session environment definition digest mismatch.");
        return definition.Digest == expectedDigest ? definition : throw new JsonException("World-session definition digest mismatch.");
    }
}

internal sealed class WorldSessionDefinitionJsonConverter : JsonConverter<WorldSessionDefinition>
{
    private static readonly string[] V1Properties =
    [
        "formatVersion", "worldIdentity", "branchIdentity", "rulesetKey", "rulesetDescriptorDigest",
        "rulesetRegistry", "rulesetRegistryDigest", "rootSeed", "runtimeAlgorithms", "schedulerGraph",
        "schedulerGraphDigest", "digest",
    ];

    private static readonly string[] V2Properties =
    [
        "formatVersion", "worldIdentity", "branchIdentity", "rulesetKey", "rulesetDescriptorDigest",
        "rulesetRegistry", "rulesetRegistryDigest", "rootSeed", "runtimeAlgorithms", "schedulerGraph",
        "schedulerGraphDigest", "commandProcessorCatalog", "commandProcessorCatalogDigest", "digest",
    ];

    private static readonly string[] V3Properties =
    [
        "formatVersion", "worldIdentity", "branchIdentity", "rulesetKey", "rulesetDescriptorDigest",
        "rulesetRegistry", "rulesetRegistryDigest", "rootSeed", "runtimeAlgorithms", "schedulerGraph",
        "schedulerGraphDigest", "commandProcessorCatalog", "commandProcessorCatalogDigest",
        "environmentDefinition", "environmentDefinitionDigest", "digest",
    ];

    public override WorldSessionDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected world-session definition object.");
        JsonElement formatElement = default;
        int formatCount = 0;
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name == "formatVersion") { formatElement = property.Value; formatCount++; }
        }
        if (formatCount != 1 || formatElement.ValueKind != JsonValueKind.String)
            throw new JsonException("World-session definition requires one formatVersion string.");

        SemanticVersion format;
        try { format = SemanticVersion.Parse(formatElement.GetString()!); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new JsonException("Invalid world-session definition format.", exception);
        }
        if (format == WorldSessionDefinition.SupportedFormatVersion) StrictModelJson.Exact(root, V1Properties);
        else if (format == WorldSessionDefinition.SaveableFormatVersion) StrictModelJson.Exact(root, V2Properties);
        else if (format == WorldSessionDefinition.EnvironmentFormatVersion) StrictModelJson.Exact(root, V3Properties);
        else throw new JsonException($"Unsupported world-session definition format '{format}'.");

        try
        {
            CommandProcessorCatalog? commandCatalog = format != WorldSessionDefinition.SupportedFormatVersion
                ? JsonSerializer.Deserialize<CommandProcessorCatalog>(root.GetProperty("commandProcessorCatalog"), options)
                    ?? throw new JsonException("Missing command processor catalog.")
                : null;
            EnvironmentDefinition? environment = format == WorldSessionDefinition.EnvironmentFormatVersion
                ? JsonSerializer.Deserialize<EnvironmentDefinition>(root.GetProperty("environmentDefinition"), options)
                    ?? throw new JsonException("Missing environment definition.")
                : null;
            return WorldSessionDefinition.CreateValidated(
                format,
                JsonSerializer.Deserialize<WorldIdentity>(root.GetProperty("worldIdentity"), options) ?? throw new JsonException("Missing world identity."),
                JsonSerializer.Deserialize<BranchIdentity>(root.GetProperty("branchIdentity"), options) ?? throw new JsonException("Missing branch identity."),
                RulesetKey.Parse(root.GetProperty("rulesetKey").GetString()!),
                JsonSerializer.Deserialize<RulesetRegistry>(root.GetProperty("rulesetRegistry"), options) ?? throw new JsonException("Missing ruleset registry."),
                JsonSerializer.Deserialize<RngSeed256>(root.GetProperty("rootSeed"), options),
                JsonSerializer.Deserialize<AlgorithmCatalog>(root.GetProperty("runtimeAlgorithms"), options) ?? throw new JsonException("Missing runtime algorithms."),
                JsonSerializer.Deserialize<SchedulerGraph>(root.GetProperty("schedulerGraph"), options) ?? throw new JsonException("Missing scheduler graph."),
                commandCatalog,
                environment,
                Sha256Digest.Parse(root.GetProperty("rulesetDescriptorDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("rulesetRegistryDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("schedulerGraphDigest").GetString()!),
                format != WorldSessionDefinition.SupportedFormatVersion
                    ? Sha256Digest.Parse(root.GetProperty("commandProcessorCatalogDigest").GetString()!)
                    : null,
                format == WorldSessionDefinition.EnvironmentFormatVersion
                    ? Sha256Digest.Parse(root.GetProperty("environmentDefinitionDigest").GetString()!)
                    : null,
                Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
        }
        catch (JsonException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
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
        if (value.FormatVersion != WorldSessionDefinition.SupportedFormatVersion)
        {
            writer.WritePropertyName("commandProcessorCatalog"); JsonSerializer.Serialize(writer, value.CommandProcessorCatalog, options);
            writer.WriteString("commandProcessorCatalogDigest", value.CommandProcessorCatalogDigest!.Value.ToString());
        }
        if (value.FormatVersion == WorldSessionDefinition.EnvironmentFormatVersion)
        {
            writer.WritePropertyName("environmentDefinition"); JsonSerializer.Serialize(writer, value.EnvironmentDefinition, options);
            writer.WriteString("environmentDefinitionDigest", value.EnvironmentDefinitionDigest!.Value.ToString());
        }
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }
}
