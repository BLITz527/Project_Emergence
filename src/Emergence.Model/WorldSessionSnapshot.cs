using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;
using Emergence.Model.Environment;

namespace Emergence.Model;

[JsonConverter(typeof(WorldSessionSnapshotJsonConverter))]
public sealed class WorldSessionSnapshot : IEquatable<WorldSessionSnapshot>
{
    public const string DigestDomainMarker = "ProjectEmergence.WorldSessionSnapshot.v1";
    public const string EnvironmentDigestDomainMarker = "ProjectEmergence.WorldSessionSnapshot.v2";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    public static SemanticVersion EnvironmentFormatVersion { get; } = new(2, 0, 0);
    private readonly ReadOnlyCollection<AcceptedSessionCommand> _pendingCommands;
    private readonly ReadOnlyCollection<FoundationIssue> _faultIssues;

    public WorldSessionSnapshot(
        WorldSessionDefinition definition,
        SimulationTick currentTick,
        WorldSessionStatus status,
        SequenceNumber lastCommandSequence,
        SequenceNumber lastEventSequence,
        IEnumerable<AcceptedSessionCommand> pendingCommands,
        IEnumerable<FoundationIssue> faultIssues,
        Sha256Digest stateDigest,
        WorldEnvironmentState? environmentState = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (environmentState is null)
        {
            if (definition.FormatVersion != WorldSessionDefinition.SaveableFormatVersion
                || !definition.RuntimeAlgorithms.Equals(AlgorithmCatalog.Phase05)
                || definition.RuntimeAlgorithms.Digest != AlgorithmCatalog.Phase05.Digest
                || definition.CommandProcessorCatalog is null)
                throw new ArgumentException("A V1 snapshot requires a Phase 0.5 V2 session definition.", nameof(definition));
        }
        else if (definition.FormatVersion != WorldSessionDefinition.EnvironmentFormatVersion
            || !definition.RuntimeAlgorithms.Equals(AlgorithmCatalog.Phase11)
            || definition.RuntimeAlgorithms.Digest != AlgorithmCatalog.Phase11.Digest
            || definition.CommandProcessorCatalog is null
            || definition.EnvironmentDefinition is null
            || !definition.EnvironmentDefinition.Equals(environmentState.Definition))
        {
            throw new ArgumentException("A V2 snapshot requires a matching Phase 1.1 V3 environment definition and state.", nameof(definition));
        }
        if (status is not (WorldSessionStatus.Paused or WorldSessionStatus.Faulted))
            throw new ArgumentException("Only Paused or Faulted sessions are persistable.", nameof(status));

        ArgumentNullException.ThrowIfNull(pendingCommands);
        AcceptedSessionCommand?[] pendingSource = pendingCommands.Cast<AcceptedSessionCommand?>().ToArray();
        if (pendingSource.Length > SessionTechnicalLimits.MaxPendingCommands)
            throw new ArgumentException($"A snapshot cannot exceed {SessionTechnicalLimits.MaxPendingCommands} pending commands.", nameof(pendingCommands));
        if (pendingSource.Any(static item => item is null))
            throw new ArgumentException("Pending commands cannot contain null.", nameof(pendingCommands));
        AcceptedSessionCommand[] pending = pendingSource.Select(static item => item!)
            .OrderBy(static item => item.ExecuteAtTick)
            .ThenBy(static item => item.SequenceNumber)
            .ToArray();
        if (pending.Select(static item => item.SequenceNumber).Distinct().Count() != pending.Length)
            throw new ArgumentException("Pending command sequences must be unique.", nameof(pendingCommands));
        foreach (AcceptedSessionCommand command in pending)
        {
            if (command.SequenceNumber.Value == UInt128.Zero || command.SequenceNumber.CompareTo(lastCommandSequence) > 0)
                throw new ArgumentException("Pending command sequences must be nonzero and no greater than the last command sequence.", nameof(pendingCommands));
            if (command.AcceptedAtTick.CompareTo(currentTick) > 0)
                throw new ArgumentException("Pending commands cannot be accepted after the snapshot tick.", nameof(pendingCommands));
            if (command.ExecuteAtTick.CompareTo(currentTick) < 0)
                throw new ArgumentException("Pending commands cannot execute before the snapshot tick.", nameof(pendingCommands));
            if (!definition.CommandProcessorCatalog.Contains(command.CommandType))
                throw new ArgumentException($"Pending command type '{command.CommandType}' is absent from the session definition.", nameof(pendingCommands));
        }

        ArgumentNullException.ThrowIfNull(faultIssues);
        FoundationIssue?[] faultSource = faultIssues.Cast<FoundationIssue?>().ToArray();
        if (faultSource.Length > SessionTechnicalLimits.MaxFaultIssues)
            throw new ArgumentException($"A snapshot cannot exceed {SessionTechnicalLimits.MaxFaultIssues} fault issues.", nameof(faultIssues));
        if (faultSource.Any(static item => item is null))
            throw new ArgumentException("Fault issues cannot contain null.", nameof(faultIssues));
        FoundationIssue[] faults = faultSource.Select(static item => item!).ToArray();
        if (faults.Any(static issue => issue.Code.IsEmpty || !Enum.IsDefined(issue.Severity)))
            throw new ArgumentException("Fault issues must be valid and use a defined severity.", nameof(faultIssues));
        if (status == WorldSessionStatus.Paused && faults.Length != 0)
            throw new ArgumentException("Paused snapshots cannot contain fault issues.", nameof(faultIssues));
        if (status == WorldSessionStatus.Faulted
            && (faults.Length == 0 || !faults.Any(static issue => issue.Severity is IssueSeverity.Error or IssueSeverity.Critical)))
            throw new ArgumentException("Faulted snapshots require at least one Error or Critical issue.", nameof(faultIssues));

        FormatVersion = environmentState is null ? SupportedFormatVersion : EnvironmentFormatVersion;
        EnvironmentState = environmentState?.Capture();
        CurrentTick = currentTick;
        Status = status;
        LastCommandSequence = lastCommandSequence;
        LastEventSequence = lastEventSequence;
        _pendingCommands = Array.AsReadOnly(pending);
        _faultIssues = Array.AsReadOnly(faults);

        Sha256Digest computedState = EnvironmentState is null
            ? WorldSessionStateFingerprint.Compute(
                definition, currentTick, status, lastCommandSequence, lastEventSequence, _pendingCommands, _faultIssues)
            : WorldSessionStateFingerprint.ComputeEnvironment(
                definition, currentTick, status, lastCommandSequence, lastEventSequence, _pendingCommands, _faultIssues, EnvironmentState);
        if (computedState != stateDigest) throw new ArgumentException("World-session snapshot state digest mismatch.", nameof(stateDigest));
        StateDigest = stateDigest;
        Digest = ComputeDigest();
    }

    public SemanticVersion FormatVersion { get; }
    public WorldSessionDefinition Definition { get; }
    public SimulationTick CurrentTick { get; }
    public WorldSessionStatus Status { get; }
    public SequenceNumber LastCommandSequence { get; }
    public SequenceNumber LastEventSequence { get; }
    public IReadOnlyList<AcceptedSessionCommand> PendingCommands => _pendingCommands;
    public IReadOnlyList<FoundationIssue> FaultIssues => _faultIssues;
    public WorldEnvironmentState? EnvironmentState { get; }
    public Sha256Digest StateDigest { get; }
    public Sha256Digest Digest { get; }

    public bool Equals(WorldSessionSnapshot? other) => other is not null
        && FormatVersion == other.FormatVersion
        && Definition.Equals(other.Definition)
        && CurrentTick == other.CurrentTick
        && Status == other.Status
        && LastCommandSequence == other.LastCommandSequence
        && LastEventSequence == other.LastEventSequence
        && PendingCommands.SequenceEqual(other.PendingCommands, AcceptedCommandValueComparer.Instance)
        && FaultIssues.SequenceEqual(other.FaultIssues)
        && Equals(EnvironmentState, other.EnvironmentState)
        && StateDigest == other.StateDigest
        && Digest == other.Digest;

    public override bool Equals(object? obj) => obj is WorldSessionSnapshot other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputeDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(FormatVersion == SupportedFormatVersion ? DigestDomainMarker : EnvironmentDigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteDigest(Definition.Digest);
        writer.WriteUInt128(CurrentTick.Value);
        writer.WriteString(Status.ToString());
        writer.WriteUInt128(LastCommandSequence.Value);
        writer.WriteUInt128(LastEventSequence.Value);
        writer.WriteUInt64(checked((ulong)PendingCommands.Count));
        foreach (AcceptedSessionCommand command in PendingCommands)
        {
            writer.WriteUInt128(command.SequenceNumber.Value);
            writer.WriteUInt128(command.AcceptedAtTick.Value);
            writer.WriteUInt128(command.ExecuteAtTick.Value);
            writer.WriteString(command.CommandType.ToString());
            writer.WriteDigest(command.Payload.Digest);
        }
        writer.WriteUInt64(checked((ulong)FaultIssues.Count));
        foreach (FoundationIssue issue in FaultIssues)
        {
            writer.WriteString(issue.Code.ToString());
            writer.WriteString(issue.Severity.ToString());
            writer.WriteString(issue.Summary);
            writer.WriteString(issue.Detail);
        }
        if (EnvironmentState is not null) writer.WriteDigest(EnvironmentState.Digest);
        writer.WriteDigest(StateDigest);
        return writer.FinalizeDigest();
    }

    internal static WorldSessionSnapshot CreateValidated(
        SemanticVersion formatVersion,
        WorldSessionDefinition definition,
        SimulationTick currentTick,
        WorldSessionStatus status,
        SequenceNumber lastCommandSequence,
        SequenceNumber lastEventSequence,
        IEnumerable<AcceptedSessionCommand> pendingCommands,
        IEnumerable<FoundationIssue> faultIssues,
        Sha256Digest stateDigest,
        Sha256Digest expectedDigest)
    {
        if (formatVersion != SupportedFormatVersion) throw new JsonException($"Unsupported world-session snapshot format '{formatVersion}'.");
        WorldSessionSnapshot snapshot = new(definition, currentTick, status, lastCommandSequence, lastEventSequence, pendingCommands, faultIssues, stateDigest);
        return snapshot.Digest == expectedDigest ? snapshot : throw new JsonException("World-session snapshot digest mismatch.");
    }

    internal static WorldSessionSnapshot CreateEnvironmentValidated(
        SemanticVersion formatVersion,
        WorldSessionDefinition definition,
        SimulationTick currentTick,
        WorldSessionStatus status,
        SequenceNumber lastCommandSequence,
        SequenceNumber lastEventSequence,
        IEnumerable<AcceptedSessionCommand> pendingCommands,
        IEnumerable<FoundationIssue> faultIssues,
        WorldEnvironmentState environmentState,
        Sha256Digest stateDigest,
        Sha256Digest expectedDigest)
    {
        if (formatVersion != EnvironmentFormatVersion) throw new JsonException($"Unsupported environment snapshot format '{formatVersion}'.");
        WorldSessionSnapshot snapshot = new(definition, currentTick, status, lastCommandSequence, lastEventSequence,
            pendingCommands, faultIssues, stateDigest, environmentState);
        return snapshot.Digest == expectedDigest ? snapshot : throw new JsonException("Environment world-session snapshot digest mismatch.");
    }

    internal sealed class AcceptedCommandValueComparer : IEqualityComparer<AcceptedSessionCommand>
    {
        public static AcceptedCommandValueComparer Instance { get; } = new();
        public bool Equals(AcceptedSessionCommand? x, AcceptedSessionCommand? y) => ReferenceEquals(x, y)
            || (x is not null && y is not null
                && x.SequenceNumber == y.SequenceNumber
                && x.AcceptedAtTick == y.AcceptedAtTick
                && x.ExecuteAtTick == y.ExecuteAtTick
                && x.CommandType == y.CommandType
                && x.Payload.Equals(y.Payload));
        public int GetHashCode(AcceptedSessionCommand obj) => HashCode.Combine(obj.SequenceNumber, obj.Payload.Digest);
    }
}

internal sealed class WorldSessionSnapshotJsonConverter : JsonConverter<WorldSessionSnapshot>
{
    private static readonly string[] V1Properties =
    [
        "formatVersion", "definition", "currentTick", "status", "lastCommandSequence", "lastEventSequence",
        "pendingCommands", "faultIssues", "stateDigest", "digest",
    ];
    private static readonly string[] V2Properties =
    [
        "formatVersion", "definition", "currentTick", "status", "lastCommandSequence", "lastEventSequence",
        "pendingCommands", "faultIssues", "environmentStateDigest", "stateDigest", "digest",
    ];

    public override WorldSessionSnapshot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        SemanticVersion format;
        try { format = SemanticVersion.Parse(root.GetProperty("formatVersion").GetString()!); }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new JsonException("World-session snapshot requires a valid formatVersion.", exception);
        }
        if (format == WorldSessionSnapshot.SupportedFormatVersion) StrictModelJson.Exact(root, V1Properties);
        else if (format == WorldSessionSnapshot.EnvironmentFormatVersion)
        {
            StrictModelJson.Exact(root, V2Properties);
            throw new JsonException("Environment snapshot hydration requires validated binary field chunks through Persistence.");
        }
        else throw new JsonException($"Unsupported world-session snapshot format '{format}'.");
        try
        {
            AcceptedSessionCommand[] pending = root.GetProperty("pendingCommands").EnumerateArray()
                .Select(item => ParseCommand(item, options)).ToArray();
            AcceptedSessionCommand[] canonical = pending.OrderBy(static item => item.ExecuteAtTick).ThenBy(static item => item.SequenceNumber).ToArray();
            if (!pending.SequenceEqual(canonical, WorldSessionSnapshot.AcceptedCommandValueComparer.Instance))
                throw new JsonException("Pending commands are not in canonical order.");
            FoundationIssue[] faults = root.GetProperty("faultIssues").EnumerateArray()
                .Select(item => ParseIssue(item, options)).ToArray();
            return WorldSessionSnapshot.CreateValidated(
                format,
                JsonSerializer.Deserialize<WorldSessionDefinition>(root.GetProperty("definition"), options) ?? throw new JsonException("Missing session definition."),
                JsonSerializer.Deserialize<SimulationTick>(root.GetProperty("currentTick"), options),
                JsonSerializer.Deserialize<WorldSessionStatus>(root.GetProperty("status"), options),
                JsonSerializer.Deserialize<SequenceNumber>(root.GetProperty("lastCommandSequence"), options),
                JsonSerializer.Deserialize<SequenceNumber>(root.GetProperty("lastEventSequence"), options),
                pending,
                faults,
                Sha256Digest.Parse(root.GetProperty("stateDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
        }
        catch (JsonException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new JsonException("Invalid world-session snapshot.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, WorldSessionSnapshot value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("formatVersion", value.FormatVersion.ToString());
        writer.WritePropertyName("definition"); JsonSerializer.Serialize(writer, value.Definition, options);
        writer.WritePropertyName("currentTick"); JsonSerializer.Serialize(writer, value.CurrentTick, options);
        writer.WritePropertyName("status"); JsonSerializer.Serialize(writer, value.Status, options);
        writer.WritePropertyName("lastCommandSequence"); JsonSerializer.Serialize(writer, value.LastCommandSequence, options);
        writer.WritePropertyName("lastEventSequence"); JsonSerializer.Serialize(writer, value.LastEventSequence, options);
        writer.WritePropertyName("pendingCommands");
        writer.WriteStartArray();
        foreach (AcceptedSessionCommand command in value.PendingCommands) WriteCommand(writer, command, options);
        writer.WriteEndArray();
        writer.WritePropertyName("faultIssues");
        writer.WriteStartArray();
        foreach (FoundationIssue issue in value.FaultIssues) WriteIssue(writer, issue, options);
        writer.WriteEndArray();
        if (value.EnvironmentState is not null) writer.WriteString("environmentStateDigest", value.EnvironmentState.Digest.ToString());
        writer.WriteString("stateDigest", value.StateDigest.ToString());
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }

    private static AcceptedSessionCommand ParseCommand(JsonElement element, JsonSerializerOptions options)
    {
        StrictModelJson.Exact(element, "sequenceNumber", "acceptedAtTick", "executeAtTick", "commandType", "payload");
        return new AcceptedSessionCommand(
            JsonSerializer.Deserialize<SequenceNumber>(element.GetProperty("sequenceNumber"), options),
            JsonSerializer.Deserialize<SimulationTick>(element.GetProperty("acceptedAtTick"), options),
            JsonSerializer.Deserialize<SimulationTick>(element.GetProperty("executeAtTick"), options),
            JsonSerializer.Deserialize<SessionCommandTypeId>(element.GetProperty("commandType"), options),
            JsonSerializer.Deserialize<ImmutableConfiguration>(element.GetProperty("payload"), options) ?? throw new JsonException("Missing command payload."));
    }

    private static FoundationIssue ParseIssue(JsonElement element, JsonSerializerOptions options)
    {
        StrictModelJson.Exact(element, "code", "severity", "summary", "detail");
        return new FoundationIssue(
            IssueCode.Parse(element.GetProperty("code").GetString()!),
            JsonSerializer.Deserialize<IssueSeverity>(element.GetProperty("severity"), options),
            element.GetProperty("summary").GetString() ?? throw new JsonException("Missing issue summary."),
            element.GetProperty("detail").GetString() ?? throw new JsonException("Missing issue detail."));
    }

    private static void WriteCommand(Utf8JsonWriter writer, AcceptedSessionCommand command, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("sequenceNumber"); JsonSerializer.Serialize(writer, command.SequenceNumber, options);
        writer.WritePropertyName("acceptedAtTick"); JsonSerializer.Serialize(writer, command.AcceptedAtTick, options);
        writer.WritePropertyName("executeAtTick"); JsonSerializer.Serialize(writer, command.ExecuteAtTick, options);
        writer.WritePropertyName("commandType"); JsonSerializer.Serialize(writer, command.CommandType, options);
        writer.WritePropertyName("payload"); JsonSerializer.Serialize(writer, command.Payload, options);
        writer.WriteEndObject();
    }

    private static void WriteIssue(Utf8JsonWriter writer, FoundationIssue issue, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("code", issue.Code.ToString());
        writer.WritePropertyName("severity"); JsonSerializer.Serialize(writer, issue.Severity, options);
        writer.WriteString("summary", issue.Summary);
        writer.WriteString("detail", issue.Detail);
        writer.WriteEndObject();
    }
}
