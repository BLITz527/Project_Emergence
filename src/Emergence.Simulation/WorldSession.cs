using System.Collections.ObjectModel;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;
using Emergence.Model;

namespace Emergence.Simulation;

/// <summary>Mutable authoritative session state with single-threaded ownership.</summary>
public sealed class WorldSession
{
    public const string StateDigestDomainMarker = "ProjectEmergence.WorldSessionState.v1";
    public const string EventIdDomainMarker = "ProjectEmergence.EventId.v1";

    private readonly int _ownerThreadId;
    private readonly CommandProcessorRegistry _commandProcessors;
    private readonly Dictionary<SimulationSystemId, ISimulationSystem> _systems;
    private readonly List<AcceptedSessionCommand> _pendingCommands = [];
    private readonly CheckedSequenceCounter _commandSequences = new();
    private readonly CheckedSequenceCounter _eventSequences = new();
    private ReadOnlyCollection<FoundationIssue> _faultIssues = Array.AsReadOnly(Array.Empty<FoundationIssue>());
    private int _stepping;
    private int _transactionViolation;

    public WorldSession(WorldSessionDefinition definition, IEnumerable<ISimulationSystem> systems, CommandProcessorRegistry commandProcessors)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ArgumentNullException.ThrowIfNull(systems);
        _commandProcessors = commandProcessors ?? throw new ArgumentNullException(nameof(commandProcessors));
        ISimulationSystem?[] source = systems.Cast<ISimulationSystem?>().ToArray();
        if (source.Any(static item => item is null)) throw new ArgumentException("Simulation systems cannot contain null.", nameof(systems));
        ISimulationSystem[] registered = source.Select(static item => item!).OrderBy(static item => item.Descriptor.Id).ToArray();
        if (registered.Select(static item => item.Descriptor.Id).Distinct().Count() != registered.Length) throw new ArgumentException("Duplicate simulation system IDs are not allowed.", nameof(systems));
        if (registered.Length != definition.SchedulerGraph.Systems.Count) throw new ArgumentException("Registered systems must exactly match the scheduler graph.", nameof(systems));
        foreach (ISimulationSystem system in registered)
        {
            if (!definition.SchedulerGraph.TryGet(system.Descriptor.Id, out SimulationSystemDescriptor? expected) || !system.Descriptor.Equals(expected))
                throw new ArgumentException($"Registered system '{system.Descriptor.Id}' does not match the scheduler graph descriptor.", nameof(systems));
        }
        _systems = registered.ToDictionary(static item => item.Descriptor.Id);
        _ownerThreadId = Environment.CurrentManagedThreadId;
        CurrentTick = default;
        Status = WorldSessionStatus.Paused;
    }

    public WorldSessionDefinition Definition { get; }
    public SimulationTick CurrentTick { get; private set; }
    public WorldSessionStatus Status { get; private set; }
    public SequenceNumber LastCommandSequence => _commandSequences.LastIssued;
    public SequenceNumber LastEventSequence => _eventSequences.LastIssued;
    public IReadOnlyList<AcceptedSessionCommand> PendingCommands => Array.AsReadOnly(_pendingCommands.ToArray());
    public IReadOnlyList<FoundationIssue> FaultIssues => _faultIssues;
    public Sha256Digest StateDigest => ComputeStateDigest(CurrentTick, Status, LastCommandSequence, LastEventSequence, _pendingCommands, _faultIssues);

    public OperationResult Pause()
    {
        if (!IsOwnerThread) return OperationResult.Failed(Issue("session.thread-ownership", IssueSeverity.Error, "Wrong session thread", "Only the owning thread may pause the session."));
        if (Volatile.Read(ref _stepping) != 0)
        {
            Interlocked.Exchange(ref _transactionViolation, 1);
            return OperationResult.Failed(Issue("session.transaction-mutation", IssueSeverity.Critical, "Mutation during active transaction", "Pause cannot mutate a session while StepOneTick is active."));
        }
        if (Status == WorldSessionStatus.Faulted) return OperationResult.Failed(Issue("session.faulted", IssueSeverity.Error, "Session is faulted", "Fault recovery is deferred beyond Phase 0.4."));
        Status = WorldSessionStatus.Paused;
        return OperationResult.Succeeded();
    }

    public OperationResult Resume()
    {
        if (!IsOwnerThread) return OperationResult.Failed(Issue("session.thread-ownership", IssueSeverity.Error, "Wrong session thread", "Only the owning thread may resume the session."));
        if (Volatile.Read(ref _stepping) != 0)
        {
            Interlocked.Exchange(ref _transactionViolation, 1);
            return OperationResult.Failed(Issue("session.transaction-mutation", IssueSeverity.Critical, "Mutation during active transaction", "Resume cannot mutate a session while StepOneTick is active."));
        }
        if (Status == WorldSessionStatus.Faulted) return OperationResult.Failed(Issue("session.faulted", IssueSeverity.Error, "Session is faulted", "Fault recovery is deferred beyond Phase 0.4."));
        Status = WorldSessionStatus.Ready;
        return OperationResult.Succeeded();
    }

    public OperationResult<AcceptedSessionCommand> SubmitCommand(SessionCommandRequest request)
    {
        if (!IsOwnerThread) return OperationResult<AcceptedSessionCommand>.Failed(Issue("session.thread-ownership", IssueSeverity.Error, "Wrong session thread", "Only the owning thread may submit commands."));
        if (Volatile.Read(ref _stepping) != 0)
        {
            Interlocked.Exchange(ref _transactionViolation, 1);
            return OperationResult<AcceptedSessionCommand>.Failed(Issue("session.transaction-mutation", IssueSeverity.Critical, "Mutation during active transaction", "SubmitCommand cannot mutate a session while StepOneTick is active."));
        }
        if (request is null) return OperationResult<AcceptedSessionCommand>.Failed(Issue("command.null", IssueSeverity.Error, "Missing command", "Command request cannot be null."));
        if (Status == WorldSessionStatus.Faulted) return OperationResult<AcceptedSessionCommand>.Failed(Issue("command.session-faulted", IssueSeverity.Error, "Session is faulted", "Faulted sessions cannot accept commands."));
        if (request.ExecuteAtTick.CompareTo(CurrentTick) < 0) return OperationResult<AcceptedSessionCommand>.Failed(Issue("command.past-tick", IssueSeverity.Error, "Command tick is in the past", $"Current tick is {CurrentTick}; requested tick is {request.ExecuteAtTick}."));
        if (!_commandProcessors.Contains(request.CommandType)) return OperationResult<AcceptedSessionCommand>.Failed(Issue("command.unknown-type", IssueSeverity.Error, "Unknown command type", request.CommandType.ToString()));
        if (_pendingCommands.Count >= SessionTechnicalLimits.MaxPendingCommands) return OperationResult<AcceptedSessionCommand>.Failed(Issue("command.pending-limit", IssueSeverity.Error, "Pending command limit reached", $"Limit is {SessionTechnicalLimits.MaxPendingCommands}."));
        if (LastCommandSequence.Value == UInt128.MaxValue) return OperationResult<AcceptedSessionCommand>.Failed(Issue("command.sequence-exhausted", IssueSeverity.Critical, "Command sequence exhausted", "Command sequence cannot wrap."));

        SequenceNumber next = new(checked(LastCommandSequence.Value + UInt128.One));
        AcceptedSessionCommand accepted = new(next, CurrentTick, request.ExecuteAtTick, request.CommandType, request.Payload);
        SequenceNumber issued = _commandSequences.IssueNext();
        if (issued != next) throw new InvalidOperationException("Command sequence prediction mismatch.");
        _pendingCommands.Add(accepted);
        _pendingCommands.Sort(AcceptedCommandComparer.Instance);
        return OperationResult<AcceptedSessionCommand>.Succeeded(accepted);
    }

    public TickExecutionReceipt StepOneTick()
    {
        if (!IsOwnerThread) return NonFaultFailure(Issue("session.thread-ownership", IssueSeverity.Error, "Wrong session thread", "Only the owning thread may step the session."));
        if (Interlocked.CompareExchange(ref _stepping, 1, 0) != 0)
        {
            Interlocked.Exchange(ref _transactionViolation, 1);
            return NonFaultFailure(Issue("session.reentrant-step", IssueSeverity.Critical, "Reentrant step rejected", "StepOneTick cannot be reentered or executed concurrently."));
        }

        try
        {
            Interlocked.Exchange(ref _transactionViolation, 0);
            if (Status == WorldSessionStatus.Paused) return NonFaultFailure(Issue("session.paused", IssueSeverity.Error, "Session is paused", "Resume the session before stepping."));
            if (Status == WorldSessionStatus.Faulted) return TickExecutionReceipt.Failed(Definition.Digest, CurrentTick, StateDigest, _faultIssues);
            if (CurrentTick.Value == UInt128.MaxValue) return Fault([Issue("session.tick-exhausted", IssueSeverity.Critical, "Simulation tick exhausted", "Logical time cannot wrap.")]);

            if (_pendingCommands.Any(command => command.ExecuteAtTick.CompareTo(CurrentTick) < 0))
                return Fault([Issue("command.past-due", IssueSeverity.Critical, "Past-due command invariant", "A pending command is earlier than the current tick.")]);

            AcceptedSessionCommand[] due = _pendingCommands.Where(command => command.ExecuteAtTick == CurrentTick).OrderBy(static command => command.SequenceNumber).ToArray();
            if (due.Length > SessionTechnicalLimits.MaxCommandsPerTick)
                return Fault([Issue("command.due-limit", IssueSeverity.Critical, "Due command limit exceeded", $"Tick {CurrentTick} has {due.Length} commands; limit is {SessionTechnicalLimits.MaxCommandsPerTick}.")]);

            try { return ExecuteTransaction(due); }
            catch (SessionExecutionException exception) { return Fault(exception.Issues); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
            {
                return Fault([Issue("session.execution-exception", IssueSeverity.Critical, "Session execution failed", $"{exception.GetType().Name}: {exception.Message}")]);
            }
        }
        finally
        {
            Volatile.Write(ref _stepping, 0);
        }
    }

    private TickExecutionReceipt ExecuteTransaction(AcceptedSessionCommand[] due)
    {
        List<WorldEventProposal> proposals = [];
        List<SimulationSystemId> executedSystems = [];
        List<FoundationIssue> receiptIssues = [];
        HashSet<SequenceNumber> dueSequences = due.Select(static command => command.SequenceNumber).ToHashSet();

        SimulationExecutionContext commandContext = new(Definition, CurrentTick, SimulationPhase.Commands, due);
        foreach (AcceptedSessionCommand command in due)
        {
            if (!_commandProcessors.TryGet(command.CommandType, out ISessionCommandProcessor? processor) || processor is null)
                throw Failure("command.processor-missing", "Command processor missing", command.CommandType.ToString());
            OperationResult<CommandProcessorOutput> result;
            try { result = processor.Process(commandContext, command); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
            {
                ThrowIfTransactionViolated("A command processor attempted to mutate or reenter the active session transaction.");
                throw Failure("command.processor-exception", "Command processor threw", $"{command.CommandType}: {exception.GetType().Name}: {exception.Message}");
            }
            ThrowIfTransactionViolated("A command processor attempted to mutate or reenter the active session transaction.");
            if (!result.Success) throw new SessionExecutionException(result.Issues);
            AppendReceiptIssues(receiptIssues, result.Issues);
            ValidateProposals(result.Value.EventProposals, SimulationPhase.Commands, null, dueSequences);
            AppendProposals(proposals, result.Value.EventProposals);
        }

        foreach (SimulationPhase phase in SchedulerGraph.FixedPhaseOrder)
        {
            SimulationExecutionContext context = phase == SimulationPhase.Commands
                ? commandContext
                : new SimulationExecutionContext(Definition, CurrentTick, phase, []);
            foreach (SimulationSystemDescriptor descriptor in Definition.SchedulerGraph.GetSystems(phase))
            {
                ISimulationSystem system = _systems[descriptor.Id];
                OperationResult<SimulationSystemOutput> result;
                try { result = system.Execute(context); }
                catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
                {
                    ThrowIfTransactionViolated("A simulation system attempted to mutate or reenter the active session transaction.");
                    throw Failure("scheduler.system-exception", "Simulation system threw", $"{descriptor.Id}: {exception.GetType().Name}: {exception.Message}");
                }
                ThrowIfTransactionViolated("A simulation system attempted to mutate or reenter the active session transaction.");
                if (!result.Success) throw new SessionExecutionException(result.Issues);
                AppendReceiptIssues(receiptIssues, result.Issues);
                ValidateProposals(result.Value.EventProposals, phase, descriptor.Id, dueSequences);
                AppendProposals(proposals, result.Value.EventProposals);
                executedSystems.Add(descriptor.Id);
            }
        }

        ThrowIfTransactionViolated("A callback attempted to mutate or reenter the active session transaction.");
        if (proposals.Count > SessionTechnicalLimits.MaxCommittedEventsPerTick)
            throw Failure("event.commit-limit", "Committed event limit exceeded", $"Tick proposed {proposals.Count} events; limit is {SessionTechnicalLimits.MaxCommittedEventsPerTick}.");

        proposals.Sort(WorldEventProposalComparer.Instance);
        for (int index = 1; index < proposals.Count; index++)
        {
            WorldEventProposal previous = proposals[index - 1];
            WorldEventProposal current = proposals[index];
            if (previous.Phase == current.Phase && previous.SourceSystem == current.SourceSystem && previous.ProposalOrdinal == current.ProposalOrdinal)
                throw Failure("event.duplicate-proposal", "Duplicate event proposal key", $"{current.Phase}/{current.SourceSystem}/{current.ProposalOrdinal}");
        }

        UInt128 count = checked((UInt128)(uint)proposals.Count);
        if (UInt128.MaxValue - LastEventSequence.Value < count) throw Failure("event.sequence-exhausted", "Event sequence exhausted", "Event sequence cannot wrap.");
        List<CommittedWorldEvent> committed = new(proposals.Count);
        HashSet<EventId> eventIds = [];
        for (int index = 0; index < proposals.Count; index++)
        {
            WorldEventProposal proposal = proposals[index];
            SequenceNumber sequence = new(checked(LastEventSequence.Value + (UInt128)(uint)(index + 1)));
            EventId eventId = DeriveEventId(sequence, CurrentTick, proposal);
            if (!eventIds.Add(eventId)) throw Failure("event.id-collision", "Event ID collision", eventId.ToString());
            committed.Add(new CommittedWorldEvent(eventId, sequence, CurrentTick, proposal.Phase, proposal.SourceSystem, proposal.EventType, proposal.Payload, proposal.CausalCommandSequence));
        }

        SimulationTick nextTick = new(checked(CurrentTick.Value + UInt128.One));
        List<AcceptedSessionCommand> remaining = _pendingCommands.Where(command => !dueSequences.Contains(command.SequenceNumber)).ToList();
        SequenceNumber resultingEventSequence = committed.Count == 0 ? LastEventSequence : committed[^1].SequenceNumber;
        Sha256Digest resultingDigest = ComputeStateDigest(nextTick, WorldSessionStatus.Ready, LastCommandSequence, resultingEventSequence, remaining, []);

        _pendingCommands.Clear();
        _pendingCommands.AddRange(remaining);
        foreach (CommittedWorldEvent item in committed)
        {
            SequenceNumber issued = _eventSequences.IssueNext();
            if (issued != item.SequenceNumber) throw new InvalidOperationException("Event sequence prediction mismatch.");
        }
        CurrentTick = nextTick;
        Status = WorldSessionStatus.Ready;
        _faultIssues = Array.AsReadOnly(Array.Empty<FoundationIssue>());
        Interlocked.Exchange(ref _transactionViolation, 0);
        return TickExecutionReceipt.Succeeded(Definition.Digest, new SimulationTick(checked(nextTick.Value - UInt128.One)), nextTick, due, executedSystems, committed, resultingDigest, receiptIssues);
    }

    private void ValidateProposals(
        IReadOnlyList<WorldEventProposal> proposals,
        SimulationPhase expectedPhase,
        SimulationSystemId? expectedSource,
        HashSet<SequenceNumber> dueSequences)
    {
        if (proposals.Count > SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick)
            throw Failure("event.proposal-limit", "Event proposal limit exceeded", $"Limit is {SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick}.");
        HashSet<UInt128> ordinals = [];
        foreach (WorldEventProposal proposal in proposals)
        {
            if (proposal is null) throw Failure("event.null-proposal", "Null event proposal", "System output contained null.");
            if (proposal.Phase != expectedPhase) throw Failure("event.phase-mismatch", "Event phase mismatch", $"Expected {expectedPhase}; received {proposal.Phase}.");
            if (expectedSource.HasValue && proposal.SourceSystem != expectedSource.Value) throw Failure("event.source-mismatch", "Event source mismatch", $"Expected {expectedSource}; received {proposal.SourceSystem}.");
            if (!expectedSource.HasValue && (!Definition.SchedulerGraph.TryGet(proposal.SourceSystem, out SimulationSystemDescriptor? source) || source is null || source.Phase != expectedPhase))
                throw Failure("event.source-unregistered", "Event source is not registered for the phase", proposal.SourceSystem.ToString());
            if (!ordinals.Add(proposal.ProposalOrdinal)) throw Failure("event.duplicate-ordinal", "Duplicate proposal ordinal", proposal.ProposalOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (proposal.CausalCommandSequence.HasValue && !dueSequences.Contains(proposal.CausalCommandSequence.Value))
                throw Failure("event.invalid-cause", "Invalid causal command", proposal.CausalCommandSequence.Value.ToString());
        }
    }

    private static void AppendProposals(List<WorldEventProposal> transaction, IReadOnlyList<WorldEventProposal> additions)
    {
        if (additions.Count > SessionTechnicalLimits.MaxCommittedEventsPerTick - transaction.Count)
            throw Failure("event.commit-limit", "Committed event limit exceeded", $"A tick cannot exceed {SessionTechnicalLimits.MaxCommittedEventsPerTick} committed events.");
        transaction.AddRange(additions);
    }

    private static void AppendReceiptIssues(List<FoundationIssue> transaction, IReadOnlyList<FoundationIssue> additions)
    {
        if (additions.Count > SessionTechnicalLimits.MaxReceiptIssuesPerTick - transaction.Count)
            throw Failure("session.receipt-issue-limit", "Tick receipt issue limit exceeded", $"A successful tick cannot exceed {SessionTechnicalLimits.MaxReceiptIssuesPerTick} informational and warning issues.");
        transaction.AddRange(additions);
    }

    private void ThrowIfTransactionViolated(string detail)
    {
        if (Volatile.Read(ref _transactionViolation) != 0)
            throw Failure("session.transaction-violation", "Active session transaction violated", detail);
    }

    private EventId DeriveEventId(SequenceNumber sequence, SimulationTick tick, WorldEventProposal proposal)
    {
        for (ulong attempt = 0; attempt <= 255; attempt++)
        {
            using CanonicalHashWriter writer = new();
            writer.WriteString(EventIdDomainMarker);
            writer.WriteString(Definition.WorldIdentity.WorldId.ToString());
            writer.WriteString(Definition.BranchIdentity.BranchId.ToString());
            writer.WriteUInt128(sequence.Value);
            writer.WriteUInt128(tick.Value);
            writer.WriteString(proposal.Phase.ToString());
            writer.WriteString(proposal.SourceSystem.ToString());
            writer.WriteString(proposal.EventType.ToString());
            writer.WriteDigest(proposal.Payload.Digest);
            writer.WriteBoolean(proposal.CausalCommandSequence.HasValue);
            if (proposal.CausalCommandSequence.HasValue) writer.WriteUInt128(proposal.CausalCommandSequence.Value.Value);
            writer.WriteUInt64(attempt);
            string digest = writer.FinalizeDigest().ToString();
            StableId128 stable = StableId128.Parse(digest[..32]);
            if (!stable.IsEmpty) return EventId.FromStableId(stable);
        }
        throw Failure("event.id-exhausted", "Event ID derivation exhausted", "All attempts produced an empty identifier.");
    }

    private Sha256Digest ComputeStateDigest(
        SimulationTick tick,
        WorldSessionStatus status,
        SequenceNumber lastCommand,
        SequenceNumber lastEvent,
        IReadOnlyList<AcceptedSessionCommand> pending,
        IReadOnlyList<FoundationIssue> faults)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(StateDigestDomainMarker);
        writer.WriteDigest(Definition.Digest);
        writer.WriteUInt128(tick.Value);
        writer.WriteString(status.ToString());
        writer.WriteUInt128(lastCommand.Value);
        writer.WriteUInt128(lastEvent.Value);
        AcceptedSessionCommand[] ordered = pending.OrderBy(static command => command.ExecuteAtTick).ThenBy(static command => command.SequenceNumber).ToArray();
        writer.WriteUInt64(checked((ulong)ordered.Length));
        foreach (AcceptedSessionCommand command in ordered)
        {
            writer.WriteUInt128(command.SequenceNumber.Value);
            writer.WriteUInt128(command.AcceptedAtTick.Value);
            writer.WriteUInt128(command.ExecuteAtTick.Value);
            writer.WriteString(command.CommandType.ToString());
            writer.WriteDigest(command.Payload.Digest);
        }
        bool faulted = status == WorldSessionStatus.Faulted;
        writer.WriteBoolean(faulted);
        if (faulted)
        {
            writer.WriteUInt64(checked((ulong)faults.Count));
            foreach (FoundationIssue issue in faults)
            {
                writer.WriteString(issue.Code.ToString());
                writer.WriteString(issue.Severity.ToString());
                writer.WriteString(issue.Summary);
                writer.WriteString(issue.Detail);
            }
        }
        return writer.FinalizeDigest();
    }

    private TickExecutionReceipt Fault(IEnumerable<FoundationIssue> issues)
    {
        FoundationIssue[] copy = issues.ToArray();
        if (copy.Length == 0 || !copy.Any(static item => item.Severity is IssueSeverity.Error or IssueSeverity.Critical))
            copy = [Issue("session.unspecified-fault", IssueSeverity.Critical, "Unspecified session fault", "Execution failed without a structured error issue.")];
        if (copy.Length > SessionTechnicalLimits.MaxFaultIssues)
            copy = [Issue("session.fault-issue-limit", IssueSeverity.Critical, "Fault issue limit exceeded", $"More than {SessionTechnicalLimits.MaxFaultIssues} issues were reported.")];
        _faultIssues = Array.AsReadOnly(copy);
        Status = WorldSessionStatus.Faulted;
        return TickExecutionReceipt.Failed(Definition.Digest, CurrentTick, StateDigest, _faultIssues);
    }

    private TickExecutionReceipt NonFaultFailure(FoundationIssue issue) => TickExecutionReceipt.Failed(Definition.Digest, CurrentTick, StateDigest, [issue]);
    private bool IsOwnerThread => Environment.CurrentManagedThreadId == _ownerThreadId;
    private static FoundationIssue Issue(string code, IssueSeverity severity, string summary, string detail) => new(new(code), severity, summary, detail);
    private static SessionExecutionException Failure(string code, string summary, string detail) => new([Issue(code, IssueSeverity.Critical, summary, detail)]);

    private sealed class SessionExecutionException(IEnumerable<FoundationIssue> issues) : Exception
    {
        public IReadOnlyList<FoundationIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
    }

    private sealed class AcceptedCommandComparer : IComparer<AcceptedSessionCommand>
    {
        public static AcceptedCommandComparer Instance { get; } = new();
        public int Compare(AcceptedSessionCommand? x, AcceptedSessionCommand? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            int tick = x.ExecuteAtTick.CompareTo(y.ExecuteAtTick);
            return tick != 0 ? tick : x.SequenceNumber.CompareTo(y.SequenceNumber);
        }
    }

    private sealed class WorldEventProposalComparer : IComparer<WorldEventProposal>
    {
        public static WorldEventProposalComparer Instance { get; } = new();
        public int Compare(WorldEventProposal? x, WorldEventProposal? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            int value = x.Phase.CompareTo(y.Phase); if (value != 0) return value;
            value = x.SourceSystem.CompareTo(y.SourceSystem); if (value != 0) return value;
            value = x.ProposalOrdinal.CompareTo(y.ProposalOrdinal); if (value != 0) return value;
            value = x.EventType.CompareTo(y.EventType); if (value != 0) return value;
            value = x.Payload.Digest.CompareTo(y.Payload.Digest); if (value != 0) return value;
            value = x.CausalCommandSequence.HasValue.CompareTo(y.CausalCommandSequence.HasValue); if (value != 0) return value;
            return x.CausalCommandSequence.GetValueOrDefault().CompareTo(y.CausalCommandSequence.GetValueOrDefault());
        }
    }
}
