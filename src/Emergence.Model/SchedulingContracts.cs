using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;

namespace Emergence.Model;

[JsonConverter(typeof(SimulationSystemDescriptorJsonConverter))]
public sealed class SimulationSystemDescriptor : IEquatable<SimulationSystemDescriptor>
{
    private readonly ReadOnlyCollection<SimulationSystemId> _runsAfter;

    public SimulationSystemDescriptor(SimulationSystemId id, SimulationPhase phase, IEnumerable<SimulationSystemId> runsAfter)
    {
        if (!id.IsValid) throw new ArgumentException("Simulation system ID must be valid.", nameof(id));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        ArgumentNullException.ThrowIfNull(runsAfter);
        SimulationSystemId[] dependencies = runsAfter.ToArray();
        if (dependencies.Length > SessionTechnicalLimits.MaxDependenciesPerSystem) throw new ArgumentException($"A system cannot exceed {SessionTechnicalLimits.MaxDependenciesPerSystem} dependencies.", nameof(runsAfter));
        if (dependencies.Any(static value => !value.IsValid)) throw new ArgumentException("Dependencies must contain valid system IDs.", nameof(runsAfter));
        if (dependencies.Contains(id)) throw new ArgumentException("A system cannot depend on itself.", nameof(runsAfter));
        Array.Sort(dependencies);
        if (dependencies.Distinct().Count() != dependencies.Length) throw new ArgumentException("Duplicate dependencies are not allowed.", nameof(runsAfter));
        Id = id;
        Phase = phase;
        _runsAfter = Array.AsReadOnly(dependencies);
    }

    [JsonPropertyOrder(0)] public SimulationSystemId Id { get; }
    [JsonPropertyOrder(1)] public SimulationPhase Phase { get; }
    [JsonPropertyOrder(2)] public IReadOnlyList<SimulationSystemId> RunsAfter => _runsAfter;

    public bool Equals(SimulationSystemDescriptor? other) => other is not null && Id == other.Id && Phase == other.Phase && _runsAfter.SequenceEqual(other._runsAfter);
    public override bool Equals(object? obj) => obj is SimulationSystemDescriptor other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Id, Phase, _runsAfter.Count);
}

internal sealed class SimulationSystemDescriptorJsonConverter : JsonConverter<SimulationSystemDescriptor>
{
    public override SimulationSystemDescriptor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected simulation system descriptor object.");
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
            if (property.Name is not ("id" or "phase" or "runsAfter") || !seen.Add(property.Name)) throw new JsonException($"Unexpected or duplicate property '{property.Name}'.");
        if (seen.Count != 3) throw new JsonException("Simulation system descriptor is missing required properties.");
        try
        {
            return new SimulationSystemDescriptor(
                JsonSerializer.Deserialize<SimulationSystemId>(root.GetProperty("id"), options),
                JsonSerializer.Deserialize<SimulationPhase>(root.GetProperty("phase"), options),
                root.GetProperty("runsAfter").EnumerateArray().Select(item => JsonSerializer.Deserialize<SimulationSystemId>(item, options)));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new JsonException("Invalid simulation system descriptor.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, SimulationSystemDescriptor value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WritePropertyName("id"); JsonSerializer.Serialize(writer, value.Id, options);
        writer.WritePropertyName("phase"); JsonSerializer.Serialize(writer, value.Phase, options);
        writer.WritePropertyName("runsAfter"); writer.WriteStartArray();
        foreach (SimulationSystemId dependency in value.RunsAfter) JsonSerializer.Serialize(writer, dependency, options);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(SchedulerGraphJsonConverter))]
public sealed class SchedulerGraph : IEquatable<SchedulerGraph>
{
    public const string DigestDomainMarker = "ProjectEmergence.SchedulerGraph.v1";
    private static readonly SimulationPhase[] PhaseOrder =
    [
        SimulationPhase.Commands,
        SimulationPhase.Prepare,
        SimulationPhase.Evaluate,
        SimulationPhase.Resolve,
        SimulationPhase.Commit,
        SimulationPhase.Finalize,
    ];

    private readonly ReadOnlyCollection<SimulationSystemDescriptor> _systems;
    private readonly ReadOnlyDictionary<SimulationPhase, IReadOnlyList<SimulationSystemDescriptor>> _byPhase;
    private readonly Dictionary<SimulationSystemId, SimulationSystemDescriptor> _byId;

    public SchedulerGraph(IEnumerable<SimulationSystemDescriptor> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);
        SimulationSystemDescriptor?[] source = systems.Cast<SimulationSystemDescriptor?>().ToArray();
        if (source.Length > SessionTechnicalLimits.MaxSystems) throw new ArgumentException($"A scheduler graph cannot exceed {SessionTechnicalLimits.MaxSystems} systems.", nameof(systems));
        if (source.Any(static item => item is null)) throw new ArgumentException("Scheduler systems cannot contain null.", nameof(systems));
        SimulationSystemDescriptor[] sorted = source.Select(static item => item!).OrderBy(static item => item.Id).ToArray();
        if (sorted.Select(static item => item.Id).Distinct().Count() != sorted.Length) throw new ArgumentException("Duplicate simulation system IDs are not allowed.", nameof(systems));

        _byId = sorted.ToDictionary(static item => item.Id);
        foreach (SimulationSystemDescriptor descriptor in sorted)
        {
            foreach (SimulationSystemId dependency in descriptor.RunsAfter)
            {
                if (!_byId.TryGetValue(dependency, out SimulationSystemDescriptor? target)) throw new ArgumentException($"System '{descriptor.Id}' has missing dependency '{dependency}'.", nameof(systems));
                if (target.Phase != descriptor.Phase) throw new ArgumentException($"System '{descriptor.Id}' has a cross-phase dependency.", nameof(systems));
            }
        }

        Dictionary<SimulationPhase, IReadOnlyList<SimulationSystemDescriptor>> byPhase = [];
        List<SimulationSystemDescriptor> execution = [];
        foreach (SimulationPhase phase in PhaseOrder)
        {
            IReadOnlyList<SimulationSystemDescriptor> order = TopologicalOrder(sorted.Where(item => item.Phase == phase).ToArray(), nameof(systems));
            byPhase.Add(phase, order);
            execution.AddRange(order);
        }
        _byPhase = new ReadOnlyDictionary<SimulationPhase, IReadOnlyList<SimulationSystemDescriptor>>(byPhase);
        _systems = Array.AsReadOnly(execution.ToArray());
        Digest = ComputeDigest(sorted);
    }

    public IReadOnlyList<SimulationSystemDescriptor> Systems => _systems;
    public Sha256Digest Digest { get; }
    public IReadOnlyList<SimulationSystemDescriptor> GetSystems(SimulationPhase phase) => Enum.IsDefined(phase) ? _byPhase[phase] : throw new ArgumentOutOfRangeException(nameof(phase));
    public bool TryGet(SimulationSystemId id, out SimulationSystemDescriptor? descriptor) => _byId.TryGetValue(id, out descriptor);

    public bool Equals(SchedulerGraph? other) => other is not null && Digest == other.Digest && _systems.SequenceEqual(other._systems);
    public override bool Equals(object? obj) => obj is SchedulerGraph other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    internal static IReadOnlyList<SimulationPhase> FixedPhaseOrder => Array.AsReadOnly(PhaseOrder);

    private static IReadOnlyList<SimulationSystemDescriptor> TopologicalOrder(SimulationSystemDescriptor[] systems, string parameterName)
    {
        Dictionary<SimulationSystemId, int> indegree = systems.ToDictionary(static item => item.Id, static item => item.RunsAfter.Count);
        Dictionary<SimulationSystemId, List<SimulationSystemId>> dependents = systems.ToDictionary(static item => item.Id, static _ => new List<SimulationSystemId>());
        foreach (SimulationSystemDescriptor descriptor in systems)
        {
            foreach (SimulationSystemId dependency in descriptor.RunsAfter) dependents[dependency].Add(descriptor.Id);
        }
        foreach (List<SimulationSystemId> values in dependents.Values) values.Sort();

        SortedSet<SimulationSystemId> ready = new(indegree.Where(static pair => pair.Value == 0).Select(static pair => pair.Key));
        List<SimulationSystemDescriptor> ordered = new(systems.Length);
        Dictionary<SimulationSystemId, SimulationSystemDescriptor> byId = systems.ToDictionary(static item => item.Id);
        while (ready.Count > 0)
        {
            SimulationSystemId next = ready.Min;
            ready.Remove(next);
            ordered.Add(byId[next]);
            foreach (SimulationSystemId dependent in dependents[next])
            {
                int remaining = checked(indegree[dependent] - 1);
                indegree[dependent] = remaining;
                if (remaining == 0) ready.Add(dependent);
            }
        }
        if (ordered.Count != systems.Length) throw new ArgumentException("Scheduler graph contains a dependency cycle.", parameterName);
        return Array.AsReadOnly(ordered.ToArray());
    }

    private static Sha256Digest ComputeDigest(IReadOnlyList<SimulationSystemDescriptor> systems)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteUInt64(checked((ulong)PhaseOrder.Length));
        foreach (SimulationPhase phase in PhaseOrder) writer.WriteString(phase.ToString());
        writer.WriteUInt64(checked((ulong)systems.Count));
        foreach (SimulationSystemDescriptor system in systems)
        {
            writer.WriteString(system.Id.ToString());
            writer.WriteString(system.Phase.ToString());
            writer.WriteUInt64(checked((ulong)system.RunsAfter.Count));
            foreach (SimulationSystemId dependency in system.RunsAfter) writer.WriteString(dependency.ToString());
        }
        return writer.FinalizeDigest();
    }

    internal static SchedulerGraph CreateValidated(IEnumerable<SimulationSystemDescriptor> systems, Sha256Digest expected)
    {
        SchedulerGraph graph = new(systems);
        return graph.Digest == expected ? graph : throw new JsonException("Scheduler graph digest mismatch.");
    }
}

internal sealed class SchedulerGraphJsonConverter : JsonConverter<SchedulerGraph>
{
    public override SchedulerGraph Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        Exact(root, "systems", "digest");
        SimulationSystemDescriptor[] systems = root.GetProperty("systems").EnumerateArray()
            .Select(item => JsonSerializer.Deserialize<SimulationSystemDescriptor>(item, options) ?? throw new JsonException("Null scheduler system."))
            .ToArray();
        return SchedulerGraph.CreateValidated(systems, Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
    }

    public override void Write(Utf8JsonWriter writer, SchedulerGraph value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WritePropertyName("systems");
        writer.WriteStartArray();
        foreach (SimulationSystemDescriptor descriptor in value.Systems.OrderBy(static item => item.Id)) JsonSerializer.Serialize(writer, descriptor, options);
        writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }

    private static void Exact(JsonElement root, params string[] expected)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected scheduler graph object.");
        HashSet<string> required = new(expected, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!required.Contains(property.Name) || !seen.Add(property.Name)) throw new JsonException($"Unexpected or duplicate property '{property.Name}'.");
        }
        if (!required.SetEquals(seen)) throw new JsonException("Scheduler graph is missing required properties.");
    }
}
