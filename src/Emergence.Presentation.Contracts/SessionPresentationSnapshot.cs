using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Time;
using Emergence.Model;

namespace Emergence.Presentation.Contracts;

public sealed class PresentationEventSummary
{
    public PresentationEventSummary(
        EventId eventId,
        SequenceNumber sequenceNumber,
        SimulationTick tick,
        SimulationPhase phase,
        WorldEventTypeId eventType,
        SimulationSystemId sourceSystem,
        Sha256Digest payloadDigest,
        SequenceNumber? causalCommandSequence)
    {
        if (eventId.IsEmpty) throw new ArgumentException("Presentation event ID must be nonempty.", nameof(eventId));
        if (sequenceNumber.Value == UInt128.Zero) throw new ArgumentException("Presentation event sequence must be nonzero.", nameof(sequenceNumber));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (!eventType.IsValid) throw new ArgumentException("Presentation event type must be valid.", nameof(eventType));
        if (!sourceSystem.IsValid) throw new ArgumentException("Presentation source system must be valid.", nameof(sourceSystem));
        if (causalCommandSequence.HasValue && causalCommandSequence.Value.Value == UInt128.Zero) throw new ArgumentException("Causal command sequence must be nonzero.", nameof(causalCommandSequence));
        EventId = eventId;
        SequenceNumber = sequenceNumber;
        Tick = tick;
        Phase = phase;
        EventType = eventType;
        SourceSystem = sourceSystem;
        PayloadDigest = payloadDigest;
        CausalCommandSequence = causalCommandSequence;
    }

    [JsonPropertyOrder(0)] public EventId EventId { get; }
    [JsonPropertyOrder(1)] public SequenceNumber SequenceNumber { get; }
    [JsonPropertyOrder(2)] public SimulationTick Tick { get; }
    [JsonPropertyOrder(3)] public SimulationPhase Phase { get; }
    [JsonPropertyOrder(4)] public WorldEventTypeId EventType { get; }
    [JsonPropertyOrder(5)] public SimulationSystemId SourceSystem { get; }
    [JsonPropertyOrder(6)] public Sha256Digest PayloadDigest { get; }
    [JsonPropertyOrder(7)] public SequenceNumber? CausalCommandSequence { get; }
}

public sealed class SessionPresentationSnapshot
{
    private readonly ReadOnlyCollection<PresentationEventSummary> _recentEvents;

    public SessionPresentationSnapshot(
        SequenceNumber sequenceNumber,
        WorldId worldId,
        BranchId branchId,
        SimulationTick tick,
        WorldSessionStatus status,
        RulesetKey rulesetKey,
        Sha256Digest sessionDefinitionDigest,
        Sha256Digest stateDigest,
        int pendingCommandCount,
        SequenceNumber lastCommandSequence,
        SequenceNumber lastEventSequence,
        IEnumerable<PresentationEventSummary> recentEvents)
    {
        if (sequenceNumber.Value == UInt128.Zero) throw new ArgumentException("Presentation sequence must be nonzero.", nameof(sequenceNumber));
        if (worldId.IsEmpty) throw new ArgumentException("World ID must be nonempty.", nameof(worldId));
        if (branchId.IsEmpty) throw new ArgumentException("Branch ID must be nonempty.", nameof(branchId));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (rulesetKey.IsEmpty) throw new ArgumentException("Ruleset key must be nonempty.", nameof(rulesetKey));
        if (pendingCommandCount < 0 || pendingCommandCount > SessionTechnicalLimits.MaxPendingCommands) throw new ArgumentOutOfRangeException(nameof(pendingCommandCount));
        ArgumentNullException.ThrowIfNull(recentEvents);
        PresentationEventSummary[] events = recentEvents.ToArray();
        if (events.Any(static item => item is null)) throw new ArgumentException("Presentation events cannot contain null.", nameof(recentEvents));
        SequenceNumber = sequenceNumber;
        WorldId = worldId;
        BranchId = branchId;
        Tick = tick;
        Status = status;
        RulesetKey = rulesetKey;
        SessionDefinitionDigest = sessionDefinitionDigest;
        StateDigest = stateDigest;
        PendingCommandCount = pendingCommandCount;
        LastCommandSequence = lastCommandSequence;
        LastEventSequence = lastEventSequence;
        _recentEvents = Array.AsReadOnly(events);
    }

    [JsonPropertyOrder(0)] public SequenceNumber SequenceNumber { get; }
    [JsonPropertyOrder(1)] public WorldId WorldId { get; }
    [JsonPropertyOrder(2)] public BranchId BranchId { get; }
    [JsonPropertyOrder(3)] public SimulationTick Tick { get; }
    [JsonPropertyOrder(4)] public WorldSessionStatus Status { get; }
    [JsonPropertyOrder(5)] public RulesetKey RulesetKey { get; }
    [JsonPropertyOrder(6)] public Sha256Digest SessionDefinitionDigest { get; }
    [JsonPropertyOrder(7)] public Sha256Digest StateDigest { get; }
    [JsonPropertyOrder(8)] public int PendingCommandCount { get; }
    [JsonPropertyOrder(9)] public SequenceNumber LastCommandSequence { get; }
    [JsonPropertyOrder(10)] public SequenceNumber LastEventSequence { get; }
    [JsonPropertyOrder(11)] public IReadOnlyList<PresentationEventSummary> RecentEvents => _recentEvents;
    [JsonPropertyOrder(12)] public bool HasBiologicalState => false;
}
