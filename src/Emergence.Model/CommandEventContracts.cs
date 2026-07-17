using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;

namespace Emergence.Model;

public sealed class SessionCommandRequest
{
    [JsonConstructor]
    public SessionCommandRequest(SimulationTick executeAtTick, SessionCommandTypeId commandType, ImmutableConfiguration payload)
    {
        if (!commandType.IsValid) throw new ArgumentException("Command type must be valid.", nameof(commandType));
        ExecuteAtTick = executeAtTick;
        CommandType = commandType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    [JsonPropertyOrder(0)] public SimulationTick ExecuteAtTick { get; }
    [JsonPropertyOrder(1)] public SessionCommandTypeId CommandType { get; }
    [JsonPropertyOrder(2)] public ImmutableConfiguration Payload { get; }
}

public sealed class AcceptedSessionCommand
{
    [JsonConstructor]
    public AcceptedSessionCommand(SequenceNumber sequenceNumber, SimulationTick acceptedAtTick, SimulationTick executeAtTick, SessionCommandTypeId commandType, ImmutableConfiguration payload)
    {
        if (sequenceNumber.Value == UInt128.Zero) throw new ArgumentException("Accepted command sequence must be nonzero.", nameof(sequenceNumber));
        if (!commandType.IsValid) throw new ArgumentException("Command type must be valid.", nameof(commandType));
        SequenceNumber = sequenceNumber;
        AcceptedAtTick = acceptedAtTick;
        ExecuteAtTick = executeAtTick;
        CommandType = commandType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    [JsonPropertyOrder(0)] public SequenceNumber SequenceNumber { get; }
    [JsonPropertyOrder(1)] public SimulationTick AcceptedAtTick { get; }
    [JsonPropertyOrder(2)] public SimulationTick ExecuteAtTick { get; }
    [JsonPropertyOrder(3)] public SessionCommandTypeId CommandType { get; }
    [JsonPropertyOrder(4)] public ImmutableConfiguration Payload { get; }
}

public sealed class WorldEventProposal
{
    [JsonConstructor]
    public WorldEventProposal(
        SimulationPhase phase,
        SimulationSystemId sourceSystem,
        UInt128 proposalOrdinal,
        WorldEventTypeId eventType,
        ImmutableConfiguration payload,
        SequenceNumber? causalCommandSequence = null)
    {
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (!sourceSystem.IsValid) throw new ArgumentException("Event source system must be valid.", nameof(sourceSystem));
        if (!eventType.IsValid) throw new ArgumentException("Event type must be valid.", nameof(eventType));
        if (causalCommandSequence.HasValue && causalCommandSequence.Value.Value == UInt128.Zero) throw new ArgumentException("Causal command sequence must be nonzero.", nameof(causalCommandSequence));
        Phase = phase;
        SourceSystem = sourceSystem;
        ProposalOrdinal = proposalOrdinal;
        EventType = eventType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        CausalCommandSequence = causalCommandSequence;
    }

    [JsonPropertyOrder(0)] public SimulationPhase Phase { get; }
    [JsonPropertyOrder(1)] public SimulationSystemId SourceSystem { get; }
    [JsonPropertyOrder(2)] public UInt128 ProposalOrdinal { get; }
    [JsonPropertyOrder(3)] public WorldEventTypeId EventType { get; }
    [JsonPropertyOrder(4)] public ImmutableConfiguration Payload { get; }
    [JsonPropertyOrder(5)] public SequenceNumber? CausalCommandSequence { get; }
}

public sealed class CommittedWorldEvent
{
    internal CommittedWorldEvent(
        EventId eventId,
        SequenceNumber sequenceNumber,
        SimulationTick tick,
        SimulationPhase phase,
        SimulationSystemId sourceSystem,
        WorldEventTypeId eventType,
        ImmutableConfiguration payload,
        SequenceNumber? causalCommandSequence)
    {
        if (eventId.IsEmpty) throw new ArgumentException("Committed event ID must be nonempty.", nameof(eventId));
        if (sequenceNumber.Value == UInt128.Zero) throw new ArgumentException("Committed event sequence must be nonzero.", nameof(sequenceNumber));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (!sourceSystem.IsValid) throw new ArgumentException("Committed event source must be valid.", nameof(sourceSystem));
        if (!eventType.IsValid) throw new ArgumentException("Committed event type must be valid.", nameof(eventType));
        EventId = eventId;
        SequenceNumber = sequenceNumber;
        Tick = tick;
        Phase = phase;
        SourceSystem = sourceSystem;
        EventType = eventType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        CausalCommandSequence = causalCommandSequence;
    }

    [JsonPropertyOrder(0)] public EventId EventId { get; }
    [JsonPropertyOrder(1)] public SequenceNumber SequenceNumber { get; }
    [JsonPropertyOrder(2)] public SimulationTick Tick { get; }
    [JsonPropertyOrder(3)] public SimulationPhase Phase { get; }
    [JsonPropertyOrder(4)] public SimulationSystemId SourceSystem { get; }
    [JsonPropertyOrder(5)] public WorldEventTypeId EventType { get; }
    [JsonPropertyOrder(6)] public ImmutableConfiguration Payload { get; }
    [JsonPropertyOrder(7)] public SequenceNumber? CausalCommandSequence { get; }
}

public sealed class TickExecutionReceipt
{
    private readonly ReadOnlyCollection<AcceptedSessionCommand> _commandsConsumed;
    private readonly ReadOnlyCollection<SimulationSystemId> _systemsExecuted;
    private readonly ReadOnlyCollection<CommittedWorldEvent> _committedEvents;
    private readonly ReadOnlyCollection<FoundationIssue> _issues;

    private TickExecutionReceipt(
        bool success,
        Sha256Digest sessionDefinitionDigest,
        SimulationTick executedTick,
        SimulationTick resultingTick,
        IEnumerable<AcceptedSessionCommand> commandsConsumed,
        IEnumerable<SimulationSystemId> systemsExecuted,
        IEnumerable<CommittedWorldEvent> committedEvents,
        Sha256Digest resultingStateDigest,
        IEnumerable<FoundationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(commandsConsumed);
        ArgumentNullException.ThrowIfNull(systemsExecuted);
        ArgumentNullException.ThrowIfNull(committedEvents);
        ArgumentNullException.ThrowIfNull(issues);
        AcceptedSessionCommand[] commands = commandsConsumed.ToArray();
        SimulationSystemId[] systems = systemsExecuted.ToArray();
        CommittedWorldEvent[] events = committedEvents.ToArray();
        FoundationIssue[] issueArray = issues.ToArray();
        if (commands.Any(static item => item is null) || events.Any(static item => item is null) || issueArray.Any(static item => item is null)) throw new ArgumentException("Receipt collections cannot contain null.");
        if (systems.Any(static item => !item.IsValid)) throw new ArgumentException("Receipt systems must be valid.");
        bool issueSuccess = !issueArray.Any(static item => item.Severity is IssueSeverity.Error or IssueSeverity.Critical);
        if (success != issueSuccess) throw new ArgumentException("Receipt success must match issue severity.", nameof(success));
        if (!success && (events.Length != 0 || commands.Length != 0 || resultingTick != executedTick)) throw new ArgumentException("Failed receipts cannot expose committed mutation.", nameof(success));
        Success = success;
        SessionDefinitionDigest = sessionDefinitionDigest;
        ExecutedTick = executedTick;
        ResultingTick = resultingTick;
        _commandsConsumed = Array.AsReadOnly(commands);
        _systemsExecuted = Array.AsReadOnly(systems);
        _committedEvents = Array.AsReadOnly(events);
        ResultingStateDigest = resultingStateDigest;
        _issues = Array.AsReadOnly(issueArray);
    }

    [JsonPropertyOrder(0)] public bool Success { get; }
    [JsonPropertyOrder(1)] public Sha256Digest SessionDefinitionDigest { get; }
    [JsonPropertyOrder(2)] public SimulationTick ExecutedTick { get; }
    [JsonPropertyOrder(3)] public SimulationTick ResultingTick { get; }
    [JsonPropertyOrder(4)] public IReadOnlyList<AcceptedSessionCommand> CommandsConsumed => _commandsConsumed;
    [JsonPropertyOrder(5)] public IReadOnlyList<SimulationSystemId> SystemsExecuted => _systemsExecuted;
    [JsonPropertyOrder(6)] public IReadOnlyList<CommittedWorldEvent> CommittedEvents => _committedEvents;
    [JsonPropertyOrder(7)] public Sha256Digest ResultingStateDigest { get; }
    [JsonPropertyOrder(8)] public IReadOnlyList<FoundationIssue> Issues => _issues;

    internal static TickExecutionReceipt Succeeded(
        Sha256Digest sessionDefinitionDigest,
        SimulationTick executedTick,
        SimulationTick resultingTick,
        IEnumerable<AcceptedSessionCommand> commandsConsumed,
        IEnumerable<SimulationSystemId> systemsExecuted,
        IEnumerable<CommittedWorldEvent> committedEvents,
        Sha256Digest resultingStateDigest,
        IEnumerable<FoundationIssue> issues) =>
        new(true, sessionDefinitionDigest, executedTick, resultingTick, commandsConsumed, systemsExecuted, committedEvents, resultingStateDigest, issues);

    internal static TickExecutionReceipt Failed(Sha256Digest sessionDefinitionDigest, SimulationTick unchangedTick, Sha256Digest resultingStateDigest, IEnumerable<FoundationIssue> issues) =>
        new(false, sessionDefinitionDigest, unchangedTick, unchangedTick, [], [], [], resultingStateDigest, issues);
}
