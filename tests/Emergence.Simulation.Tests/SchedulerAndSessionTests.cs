using System.Reflection;
using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Results;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Simulation;

namespace Emergence.Simulation.Tests;

public sealed class SchedulerAndSessionTests
{
    [Fact]
    public void GraphInsertionPermutationsHaveIdenticalDigestAndOrder()
    {
        SimulationSystemDescriptor[] systems = FixtureDescriptors();
        SchedulerGraph first = new(systems);
        SchedulerGraph second = new(systems.Reverse());
        Assert.Equal("3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a", first.Digest.ToString());
        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(first.Systems.Select(static item => item.Id), second.Systems.Select(static item => item.Id));
        Assert.Equal(new[] { "foundation.trace.prepare-a", "foundation.trace.prepare-b" }, first.GetSystems(SimulationPhase.Prepare).Select(static item => item.Id.ToString()));
    }

    [Fact]
    public void GraphRejectsMissingCrossPhaseCycleAndDuplicates()
    {
        Assert.Throws<ArgumentException>(() => new SchedulerGraph([new(new("foundation.a"), SimulationPhase.Prepare, [new("foundation.missing")])]));
        Assert.Throws<ArgumentException>(() => new SchedulerGraph([new(new("foundation.a"), SimulationPhase.Prepare, []), new(new("foundation.b"), SimulationPhase.Evaluate, [new("foundation.a")])]));
        Assert.Throws<ArgumentException>(() => new SchedulerGraph([new(new("foundation.a"), SimulationPhase.Prepare, [new("foundation.b")]), new(new("foundation.b"), SimulationPhase.Prepare, [new("foundation.a")])]));
        Assert.Throws<ArgumentException>(() => new SchedulerGraph([new(new("foundation.a"), SimulationPhase.Prepare, []), new(new("foundation.a"), SimulationPhase.Prepare, [])]));
    }

    [Fact]
    public void GraphAndDependencyLimitsAreExact()
    {
        SimulationSystemDescriptor[] exact = Enumerable.Range(0, SessionTechnicalLimits.MaxSystems)
            .Select(index => new SimulationSystemDescriptor(new($"foundation.system.s{index:D3}"), SimulationPhase.Prepare, []))
            .ToArray();
        Assert.Equal(SessionTechnicalLimits.MaxSystems, new SchedulerGraph(exact).Systems.Count);
        Assert.Throws<ArgumentException>(() => new SchedulerGraph(exact.Append(new(new("foundation.system.extra"), SimulationPhase.Prepare, []))));

        SimulationSystemId[] dependencies = Enumerable.Range(0, SessionTechnicalLimits.MaxDependenciesPerSystem)
            .Select(index => new SimulationSystemId($"foundation.dependency.d{index:D2}"))
            .ToArray();
        Assert.Equal(SessionTechnicalLimits.MaxDependenciesPerSystem, new SimulationSystemDescriptor(new("foundation.target"), SimulationPhase.Prepare, dependencies).RunsAfter.Count);
        Assert.Throws<ArgumentException>(() => new SimulationSystemDescriptor(new("foundation.target"), SimulationPhase.Prepare, dependencies.Append(new("foundation.dependency.extra"))));
    }

    [Fact]
    public void SessionLifecycleStartsPausedAndAdvancesOnlyWhenReady()
    {
        WorldSession session = FixtureSession();
        Assert.Equal(WorldSessionStatus.Paused, session.Status);
        Assert.Equal(UInt128.Zero, session.CurrentTick.Value);
        Assert.False(session.StepOneTick().Success);
        Assert.Equal(UInt128.Zero, session.CurrentTick.Value);
        Assert.True(session.Resume().Success);
        Assert.True(session.StepOneTick().Success);
        Assert.Equal((UInt128)1, session.CurrentTick.Value);
        Assert.True(session.Pause().Success);
        Assert.False(session.StepOneTick().Success);
        Assert.Equal((UInt128)1, session.CurrentTick.Value);
    }

    [Fact]
    public void CommandSubmissionAssignsSequencesWithoutConsumingOnFailure()
    {
        WorldSession session = CommandSession();
        Assert.False(session.SubmitCommand(new(default, new("foundation.unknown"), Payload("x"))).Success);
        AcceptedSessionCommand first = session.SubmitCommand(new(default, new("foundation.trace"), Payload("a"))).Value;
        Assert.Equal((UInt128)1, first.SequenceNumber.Value);
        Assert.False(session.SubmitCommand(new(default, new("foundation.unknown"), Payload("x"))).Success);
        AcceptedSessionCommand second = session.SubmitCommand(new(new(1), new("foundation.trace"), Payload("b"))).Value;
        Assert.Equal((UInt128)2, second.SequenceNumber.Value);
    }

    [Fact]
    public void PendingCommandsOrderByTickThenAcceptanceAndFutureRemains()
    {
        WorldSession session = CommandSession();
        AcceptedSessionCommand future = session.SubmitCommand(new(new(1), new("foundation.trace"), Payload("future"))).Value;
        AcceptedSessionCommand nowA = session.SubmitCommand(new(default, new("foundation.trace"), Payload("a"))).Value;
        AcceptedSessionCommand nowB = session.SubmitCommand(new(default, new("foundation.trace"), Payload("b"))).Value;
        Assert.Equal(new[] { nowA.SequenceNumber, nowB.SequenceNumber, future.SequenceNumber }, session.PendingCommands.Select(static item => item.SequenceNumber));
        session.Resume();
        TickExecutionReceipt receipt = session.StepOneTick();
        Assert.Equal(new[] { nowA.SequenceNumber, nowB.SequenceNumber }, receipt.CommandsConsumed.Select(static item => item.SequenceNumber));
        Assert.Single(session.PendingCommands);
        Assert.Equal(future.SequenceNumber, session.PendingCommands[0].SequenceNumber);
    }

    [Fact]
    public void PendingAndDueCommandLimitsFailBeforePartialMutation()
    {
        WorldSession pending = CommandSession();
        for (int index = 0; index < SessionTechnicalLimits.MaxPendingCommands; index++)
            Assert.True(pending.SubmitCommand(new(new(1), new("foundation.trace"), Payload(index.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Success);
        SequenceNumber last = pending.LastCommandSequence;
        Assert.False(pending.SubmitCommand(new(new(1), new("foundation.trace"), Payload("overflow"))).Success);
        Assert.Equal(last, pending.LastCommandSequence);
        Assert.Equal(SessionTechnicalLimits.MaxPendingCommands, pending.PendingCommands.Count);

        WorldSession due = CommandSession();
        for (int index = 0; index <= SessionTechnicalLimits.MaxCommandsPerTick; index++)
            Assert.True(due.SubmitCommand(new(default, new("foundation.trace"), Payload("x"))).Success);
        due.Resume();
        TickExecutionReceipt failure = due.StepOneTick();
        Assert.False(failure.Success);
        Assert.Equal(WorldSessionStatus.Faulted, due.Status);
        Assert.Equal(SessionTechnicalLimits.MaxCommandsPerTick + 1, due.PendingCommands.Count);
        Assert.Equal(UInt128.Zero, due.CurrentTick.Value);
        Assert.Equal(UInt128.Zero, due.LastEventSequence.Value);
    }

    [Fact]
    public void ExactDueCommandLimitExecutesWithoutDroppingOrDeferring()
    {
        WorldSession session = CommandSession();
        for (int index = 0; index < SessionTechnicalLimits.MaxCommandsPerTick; index++)
            Assert.True(session.SubmitCommand(new(default, new("foundation.trace"), Payload(index.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Success);

        session.Resume();
        TickExecutionReceipt receipt = session.StepOneTick();

        Assert.True(receipt.Success);
        Assert.Equal(SessionTechnicalLimits.MaxCommandsPerTick, receipt.CommandsConsumed.Count);
        Assert.Empty(session.PendingCommands);
    }

    [Fact]
    public void CommandProcessorRegistryEnforcesNullDefaultDuplicateAndExactLimit()
    {
        Assert.Throws<ArgumentException>(() => new CommandProcessorRegistry([null!]));
        Assert.Throws<ArgumentException>(() => new CommandProcessorRegistry([new GenericProcessor(default)]));
        Assert.Throws<ArgumentException>(() => new CommandProcessorRegistry([new GenericProcessor(new("foundation.same")), new GenericProcessor(new("foundation.same"))]));
        GenericProcessor[] exact = Enumerable.Range(0, SessionTechnicalLimits.MaxCommandProcessors)
            .Select(index => new GenericProcessor(new SessionCommandTypeId($"foundation.command.c{index:D3}")))
            .ToArray();
        Assert.Equal(SessionTechnicalLimits.MaxCommandProcessors, new CommandProcessorRegistry(exact).Processors.Count);
        Assert.Throws<ArgumentException>(() => new CommandProcessorRegistry(exact.Append(new GenericProcessor(new("foundation.command.extra")))));
    }

    [Fact]
    public void PastDueCommandFaultsWithoutRemovingQueue()
    {
        WorldSession session = CommandSession();
        session.SubmitCommand(new(default, new("foundation.trace"), Payload("x")));
        typeof(WorldSession).GetProperty(nameof(WorldSession.CurrentTick))!.SetValue(session, new SimulationTick(1));
        session.Resume();
        TickExecutionReceipt receipt = session.StepOneTick();
        Assert.False(receipt.Success);
        Assert.Single(session.PendingCommands);
        Assert.Equal((UInt128)1, session.CurrentTick.Value);
        Assert.Equal(WorldSessionStatus.Faulted, session.Status);
    }

    [Fact]
    public void FailedSystemCommitsNothingAndFaultsAtomically()
    {
        SimulationSystemDescriptor descriptor = new(new("foundation.failure"), SimulationPhase.Prepare, []);
        WorldSession session = SessionWithSystems([new FailureSystem(descriptor)]);
        session.Resume();
        string before = session.StateDigest.ToString();
        TickExecutionReceipt receipt = session.StepOneTick();
        Assert.False(receipt.Success);
        Assert.Empty(receipt.CommittedEvents);
        Assert.Equal(UInt128.Zero, session.CurrentTick.Value);
        Assert.Equal(UInt128.Zero, session.LastEventSequence.Value);
        Assert.Equal(WorldSessionStatus.Faulted, session.Status);
        Assert.NotEqual(before, session.StateDigest.ToString());
        Assert.False(session.Resume().Success);
        Assert.False(session.SubmitCommand(new(default, new("foundation.trace"), Payload("x"))).Success);
    }

    [Fact]
    public void TickMaxValueFaultsWithoutWrap()
    {
        WorldSession session = CommandSession();
        typeof(WorldSession).GetProperty(nameof(WorldSession.CurrentTick))!.SetValue(session, SimulationTick.MaxValue);
        session.Resume();
        Assert.False(session.StepOneTick().Success);
        Assert.Equal(SimulationTick.MaxValue, session.CurrentTick);
        Assert.Equal(WorldSessionStatus.Faulted, session.Status);
    }

    [Fact]
    public void EventOutputIsCanonicalIndependentOfRegistrationOrder()
    {
        SimulationSystemDescriptor a = new(new("foundation.system.a"), SimulationPhase.Prepare, []);
        SimulationSystemDescriptor b = new(new("foundation.system.b"), SimulationPhase.Prepare, []);
        WorldSession first = SessionWithSystems([new ProposalSystem(b, "b"), new ProposalSystem(a, "a")]);
        WorldSession second = SessionWithSystems([new ProposalSystem(a, "a"), new ProposalSystem(b, "b")]);
        first.Resume(); second.Resume();
        TickExecutionReceipt left = first.StepOneTick(); TickExecutionReceipt right = second.StepOneTick();
        Assert.Equal(left.CommittedEvents.Select(static item => item.EventId), right.CommittedEvents.Select(static item => item.EventId));
        Assert.Equal(new[] { "foundation.system.a", "foundation.system.b" }, left.CommittedEvents.Select(static item => item.SourceSystem.ToString()));
    }

    [Fact]
    public void EventIdsSeparateBranchTickAndPayload()
    {
        EventId branchA = ExecuteOneEvent(7, "x", 0);
        EventId branchB = ExecuteOneEvent(8, "x", 0);
        EventId payload = ExecuteOneEvent(7, "y", 0);
        EventId tick = ExecuteOneEvent(7, "x", 1);
        Assert.Equal(4, new[] { branchA, branchB, payload, tick }.Distinct().Count());
    }

    [Fact]
    public void DuplicateProposalOrdinalFaults()
    {
        SimulationSystemDescriptor descriptor = new(new("foundation.duplicate"), SimulationPhase.Prepare, []);
        WorldSession session = SessionWithSystems([new DuplicateProposalSystem(descriptor)]);
        session.Resume();
        Assert.False(session.StepOneTick().Success);
        Assert.Equal(WorldSessionStatus.Faulted, session.Status);
        Assert.Equal(UInt128.Zero, session.LastEventSequence.Value);
    }

    [Fact]
    public void CommittedEventLimitIsExactAndOneOverFaultsAtomically()
    {
        ISimulationSystem[] exactSystems = Enumerable.Range(0, 4)
            .Select(index => (ISimulationSystem)new ManyProposalSystem(
                new(new($"foundation.bulk.s{index}"), SimulationPhase.Prepare, []),
                SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick))
            .ToArray();
        WorldSession exact = SessionWithSystems(exactSystems);
        exact.Resume();
        TickExecutionReceipt success = exact.StepOneTick();
        Assert.True(success.Success);
        Assert.Equal(SessionTechnicalLimits.MaxCommittedEventsPerTick, success.CommittedEvents.Count);
        Assert.Equal((UInt128)SessionTechnicalLimits.MaxCommittedEventsPerTick, exact.LastEventSequence.Value);

        ISimulationSystem[] excessiveSystems = exactSystems.Append(
            new ManyProposalSystem(new(new("foundation.bulk.s4"), SimulationPhase.Prepare, []), 1)).ToArray();
        WorldSession excessive = SessionWithSystems(excessiveSystems);
        excessive.Resume();
        TickExecutionReceipt failure = excessive.StepOneTick();
        Assert.False(failure.Success);
        Assert.Empty(failure.CommittedEvents);
        Assert.Equal(UInt128.Zero, excessive.LastEventSequence.Value);
        Assert.Equal(UInt128.Zero, excessive.CurrentTick.Value);
    }

    [Fact]
    public void EventSequenceStartsAtOneAndExhaustionFaultsBeforeCommit()
    {
        SimulationSystemDescriptor descriptor = new(new("foundation.sequence"), SimulationPhase.Prepare, []);
        WorldSession first = SessionWithSystems([new ProposalSystem(descriptor, "one")]);
        first.Resume();
        Assert.Equal((UInt128)1, first.StepOneTick().CommittedEvents.Single().SequenceNumber.Value);

        WorldSession exhausted = SessionWithSystems([new ProposalSystem(descriptor, "overflow")]);
        CheckedSequenceCounter counter = (CheckedSequenceCounter)typeof(WorldSession).GetField("_eventSequences", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(exhausted)!;
        typeof(CheckedSequenceCounter).GetProperty(nameof(CheckedSequenceCounter.LastIssued))!.SetValue(counter, new SequenceNumber(UInt128.MaxValue));
        exhausted.Resume();
        TickExecutionReceipt failure = exhausted.StepOneTick();
        Assert.False(failure.Success);
        Assert.Empty(failure.CommittedEvents);
        Assert.Equal(UInt128.MaxValue, exhausted.LastEventSequence.Value);
        Assert.Equal(UInt128.Zero, exhausted.CurrentTick.Value);
    }

    [Fact]
    public void FaultIssueLimitAcceptsExactAndCollapsesOneOver()
    {
        FoundationIssue[] exactIssues = Enumerable.Range(0, SessionTechnicalLimits.MaxFaultIssues).Select(index => Failure($"synthetic.issue.i{index}")).ToArray();
        SimulationSystemDescriptor exactDescriptor = new(new("foundation.issue.exact"), SimulationPhase.Prepare, []);
        WorldSession exact = SessionWithSystems([new IssueSystem(exactDescriptor, exactIssues)]);
        exact.Resume(); exact.StepOneTick();
        Assert.Equal(SessionTechnicalLimits.MaxFaultIssues, exact.FaultIssues.Count);

        FoundationIssue[] excessiveIssues = exactIssues.Append(Failure("synthetic.issue.extra")).ToArray();
        SimulationSystemDescriptor excessiveDescriptor = new(new("foundation.issue.excessive"), SimulationPhase.Prepare, []);
        WorldSession excessive = SessionWithSystems([new IssueSystem(excessiveDescriptor, excessiveIssues)]);
        excessive.Resume(); excessive.StepOneTick();
        Assert.Single(excessive.FaultIssues);
        Assert.Equal("session.fault-issue-limit", excessive.FaultIssues[0].Code.ToString());
    }

    [Fact]
    public void ReentrantStepAndThrownProcessorFaultWithoutPartialCommit()
    {
        SimulationSystemDescriptor descriptor = new(new("foundation.reentrant"), SimulationPhase.Prepare, []);
        ReentrantSystem system = new(descriptor);
        WorldSession reentrant = SessionWithSystems([system]);
        system.Session = reentrant;
        reentrant.Resume();
        TickExecutionReceipt reentrantFailure = reentrant.StepOneTick();
        Assert.False(reentrantFailure.Success);
        Assert.Equal(WorldSessionStatus.Faulted, reentrant.Status);
        Assert.Equal(UInt128.Zero, reentrant.CurrentTick.Value);

        WorldSession throwing = new(Definition([], 7), [], new CommandProcessorRegistry([new ThrowingProcessor()]));
        Assert.True(throwing.SubmitCommand(new(default, new("foundation.throw"), Payload("x"))).Success);
        throwing.Resume();
        TickExecutionReceipt thrown = throwing.StepOneTick();
        Assert.False(thrown.Success);
        Assert.Single(throwing.PendingCommands);
        Assert.Equal(UInt128.Zero, throwing.LastEventSequence.Value);
        Assert.Equal(UInt128.Zero, throwing.CurrentTick.Value);
    }

    [Fact]
    public void ActiveStepMutationFromCommandProcessorFaultsAtomically()
    {
        Func<WorldSession, OperationResult>[] mutations =
        [
            session => session.SubmitCommand(new(default, new("foundation.mutate"), Payload("current"))),
            session => session.SubmitCommand(new(new(1), new("foundation.mutate"), Payload("future"))),
            session => session.Pause(),
            session => session.Resume(),
        ];

        foreach (Func<WorldSession, OperationResult> mutation in mutations)
        {
            MutationProcessor processor = new(mutation);
            WorldSession session = SessionWithProcessor(processor, []);
            processor.Session = session;
            Assert.True(session.SubmitCommand(new(default, processor.CommandType, Payload("trigger"))).Success);
            Assert.True(session.Resume().Success);

            TickExecutionReceipt receipt = session.StepOneTick();

            Assert.False(processor.MutationResult!.Success);
            AssertTransactionFailure(session, receipt, expectedPendingCommands: 1, expectedLastCommand: 1);
        }
    }

    [Fact]
    public void ActiveStepMutationFromSimulationSystemFaultsAtomically()
    {
        Func<WorldSession, OperationResult>[] mutations =
        [
            session => session.SubmitCommand(new(default, new("foundation.trace"), Payload("current"))),
            session => session.SubmitCommand(new(new(1), new("foundation.trace"), Payload("future"))),
            session => session.Pause(),
            session => session.Resume(),
        ];

        for (int index = 0; index < mutations.Length; index++)
        {
            MutationSystem system = new(new(new($"foundation.mutation.s{index}"), SimulationPhase.Prepare, []), mutations[index]);
            WorldSession session = SessionWithSystems([system]);
            system.Session = session;
            Assert.True(session.Resume().Success);

            TickExecutionReceipt receipt = session.StepOneTick();

            Assert.False(system.MutationResult!.Success);
            AssertTransactionFailure(session, receipt, expectedPendingCommands: 0, expectedLastCommand: 0);
        }
    }

    [Fact]
    public void ReentrantCommandProcessorFaultsWithEmptyOrNonemptyGraph()
    {
        foreach (bool hasSystem in new[] { false, true })
        {
            NestedStepProcessor processor = new();
            ISimulationSystem[] systems = hasSystem
                ? [new DiagnosticSystem(new(new("foundation.inert"), SimulationPhase.Prepare, []), false)]
                : [];
            WorldSession session = SessionWithProcessor(processor, systems);
            processor.Session = session;
            Assert.True(session.SubmitCommand(new(default, processor.CommandType, Payload("trigger"))).Success);
            Assert.True(session.Resume().Success);

            TickExecutionReceipt receipt = session.StepOneTick();

            Assert.False(processor.NestedReceipt!.Success);
            AssertTransactionFailure(session, receipt, expectedPendingCommands: 1, expectedLastCommand: 1);
        }
    }

    [Fact]
    public void SuccessfulCallbackIssuesArePreservedInDeterministicOrder()
    {
        SimulationSystemDescriptor a = new(new("foundation.diagnostic.a"), SimulationPhase.Prepare, []);
        SimulationSystemDescriptor b = new(new("foundation.diagnostic.b"), SimulationPhase.Prepare, [a.Id]);
        DiagnosticProcessor diagnosticProcessor = new(true);
        WorldSession diagnostic = SessionWithProcessor(diagnosticProcessor, [new DiagnosticSystem(b, true), new DiagnosticSystem(a, true)]);
        diagnostic.SubmitCommand(new(default, diagnosticProcessor.CommandType, Payload("first")));
        diagnostic.SubmitCommand(new(default, diagnosticProcessor.CommandType, Payload("second")));
        diagnostic.Resume();

        DiagnosticProcessor quietProcessor = new(false);
        WorldSession quiet = SessionWithProcessor(quietProcessor, [new DiagnosticSystem(b, false), new DiagnosticSystem(a, false)]);
        quiet.SubmitCommand(new(default, quietProcessor.CommandType, Payload("first")));
        quiet.SubmitCommand(new(default, quietProcessor.CommandType, Payload("second")));
        quiet.Resume();

        TickExecutionReceipt receipt = diagnostic.StepOneTick();
        TickExecutionReceipt quietReceipt = quiet.StepOneTick();

        Assert.True(receipt.Success);
        Assert.Equal(
            ["processor.s1.info", "processor.s1.warning", "processor.s2.info", "processor.s2.warning", "system.foundation.diagnostic.a", "system.foundation.diagnostic.b"],
            receipt.Issues.Select(static issue => issue.Code.ToString()));
        Assert.All(receipt.Issues, static issue => Assert.True(issue.Severity is IssueSeverity.Information or IssueSeverity.Warning));
        Assert.Contains(receipt.Issues, static issue => issue.Severity == IssueSeverity.Information);
        Assert.Contains(receipt.Issues, static issue => issue.Severity == IssueSeverity.Warning);
        Assert.Equal(quietReceipt.ResultingStateDigest, receipt.ResultingStateDigest);
        Assert.Equal(quiet.StateDigest, diagnostic.StateDigest);
        Assert.Empty(receipt.CommittedEvents);
    }

    [Fact]
    public void ReceiptIssueLimitIsExactAndOneOverFaultsAtomically()
    {
        FoundationIssue[] exactIssues = Enumerable.Range(0, SessionTechnicalLimits.MaxReceiptIssuesPerTick)
            .Select(index => Diagnostic($"receipt.i{index:D3}", IssueSeverity.Warning))
            .ToArray();
        SimulationSystemDescriptor exactDescriptor = new(new("foundation.receipt.exact"), SimulationPhase.Prepare, []);
        WorldSession exact = SessionWithSystems([new SuccessfulIssueSystem(exactDescriptor, exactIssues)]);
        exact.Resume();
        TickExecutionReceipt success = exact.StepOneTick();
        Assert.True(success.Success);
        Assert.Equal(SessionTechnicalLimits.MaxReceiptIssuesPerTick, success.Issues.Count);

        FoundationIssue[] excessiveIssues = exactIssues.Append(Diagnostic("receipt.extra", IssueSeverity.Information)).ToArray();
        SimulationSystemDescriptor excessiveDescriptor = new(new("foundation.receipt.excessive"), SimulationPhase.Prepare, []);
        WorldSession excessive = SessionWithSystems([new SuccessfulIssueSystem(excessiveDescriptor, excessiveIssues)]);
        excessive.Resume();
        TickExecutionReceipt failure = excessive.StepOneTick();
        AssertTransactionFailure(excessive, failure, expectedPendingCommands: 0, expectedLastCommand: 0, expectedIssueCode: "session.receipt-issue-limit");
        Assert.Equal("session.receipt-issue-limit", failure.Issues.Single().Code.ToString());
    }

    [Fact]
    public void WrongThreadMutationDuringStepIsRejectedWithoutViolatingOwnerTransaction()
    {
        SimulationSystemDescriptor descriptor = new(new("foundation.wrong-thread"), SimulationPhase.Prepare, []);
        WrongThreadMutationSystem system = new(descriptor);
        WorldSession session = SessionWithSystems([system]);
        system.Session = session;
        session.Resume();

        TickExecutionReceipt receipt = session.StepOneTick();

        Assert.True(receipt.Success);
        Assert.All(system.Results, static result => Assert.False(result.Success));
        Assert.Equal(WorldSessionStatus.Ready, session.Status);
        Assert.Equal((UInt128)1, session.CurrentTick.Value);
        Assert.Empty(session.PendingCommands);
        Assert.Equal(UInt128.Zero, session.LastCommandSequence.Value);
    }

    [Fact]
    public void ExecutionContextHasNoMutableSessionSurfaceAndPhaseOrderIsExact()
    {
        Assert.DoesNotContain(typeof(SimulationExecutionContext).GetProperties(), property => property.PropertyType == typeof(WorldSession));
        Assert.DoesNotContain(typeof(SimulationExecutionContext).GetMethods(BindingFlags.Public | BindingFlags.Instance), method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(WorldSession)));
        List<string> order = [];
        ISimulationSystem[] systems = Enum.GetValues<SimulationPhase>()
            .Select(phase => (ISimulationSystem)new RecordingSystem(new(new($"foundation.phase.{phase.ToString().ToLowerInvariant()}"), phase, []), order))
            .Reverse()
            .ToArray();
        WorldSession session = SessionWithSystems(systems);
        session.Resume();
        Assert.True(session.StepOneTick().Success);
        Assert.Equal(new[] { "Commands", "Prepare", "Evaluate", "Resolve", "Commit", "Finalize" }, order);
    }

    [Fact]
    public void SystemOutputLimitAcceptsExactAndRejectsOneOver()
    {
        WorldEventProposal[] exact = Enumerable.Range(0, SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick)
            .Select(index => new WorldEventProposal(SimulationPhase.Prepare, new SimulationSystemId("foundation.system"), (UInt128)(uint)index, new WorldEventTypeId("foundation.event"), Payload("x")))
            .ToArray();
        Assert.Equal(SessionTechnicalLimits.MaxEventProposalsPerSystemPerTick, new SimulationSystemOutput(exact).EventProposals.Count);
        Assert.Throws<ArgumentException>(() => new SimulationSystemOutput(exact.Append(new(SimulationPhase.Prepare, new("foundation.system"), (UInt128)(uint)exact.Length, new("foundation.event"), Payload("x")))));
    }

    [Fact]
    public void FullSelfTestMatchesEveryGoldenVectorAndIsByteStable()
    {
        SessionSelfTestReport first = SessionSelfTest.Run();
        SessionSelfTestReport second = SessionSelfTest.Run();
        Assert.True(first.Success);
        Assert.Equal("bbaebfc88087fc04ab024d2505b9a50ed7e7a2f21cd34a18eb4e83d56cb1a418", first.AlgorithmCatalogDigest);
        Assert.Equal("3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a", first.SchedulerGraphDigest);
        Assert.Equal("fcc91152d376a93f558f44c2e76eb8493ab61fb519d598faa8782992d8cd3456", first.SessionDefinitionDigest);
        Assert.Equal(SessionSelfTest.ExpectedSessionTraceDigest, first.SessionTraceDigest);
        Assert.Equal(SessionSelfTest.ExpectedFinalStateDigest, first.FinalStateDigest);
        Assert.Equal(SessionSelfTest.ExpectedEventIds, first.EventIds);
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
    }

    [Fact]
    public void StateDigestIsRepeatableAndSensitiveToPendingOrderIndependentState()
    {
        WorldSession first = CommandSession(); WorldSession second = CommandSession();
        first.SubmitCommand(new(new(1), new("foundation.trace"), Payload("a")));
        first.SubmitCommand(new(default, new("foundation.trace"), Payload("b")));
        second.SubmitCommand(new(new(1), new("foundation.trace"), Payload("a")));
        second.SubmitCommand(new(default, new("foundation.trace"), Payload("b")));
        Assert.Equal(first.StateDigest, first.StateDigest);
        Assert.Equal(first.StateDigest, second.StateDigest);
        second.Resume();
        Assert.NotEqual(first.StateDigest, second.StateDigest);
    }

    private static SimulationSystemDescriptor[] FixtureDescriptors() =>
    [
        new(new("foundation.trace.command"), SimulationPhase.Commands, []),
        new(new("foundation.trace.prepare-a"), SimulationPhase.Prepare, []),
        new(new("foundation.trace.prepare-b"), SimulationPhase.Prepare, [new("foundation.trace.prepare-a")]),
        new(new("foundation.trace.evaluate"), SimulationPhase.Evaluate, []),
    ];

    private static WorldSession FixtureSession() => FoundationSessionFixture.CreatePausedSession(new([FoundationReferenceRuleset.Create()]));

    private static WorldSession CommandSession()
    {
        WorldSessionDefinition definition = Definition([], 7);
        return new WorldSession(definition, [], new CommandProcessorRegistry([new EmptyProcessor()]));
    }

    private static WorldSession SessionWithSystems(IReadOnlyList<ISimulationSystem> systems, ulong branch = 7)
    {
        WorldSessionDefinition definition = Definition(systems.Select(static item => item.Descriptor), branch);
        return new WorldSession(definition, systems, new CommandProcessorRegistry([new EmptyProcessor()]));
    }

    private static WorldSession SessionWithProcessor(ISessionCommandProcessor processor, IReadOnlyList<ISimulationSystem> systems)
    {
        WorldSessionDefinition definition = Definition(systems.Select(static item => item.Descriptor), 7);
        return new WorldSession(definition, systems, new CommandProcessorRegistry([processor]));
    }

    private static WorldSessionDefinition Definition(IEnumerable<SimulationSystemDescriptor> descriptors, ulong branch) => new(
        new(WorldId.FromUInt64(42)),
        new(WorldId.FromUInt64(42), BranchId.FromUInt64(branch)),
        new(RulesetId.FromUInt64(1), new(1, 0, 0)),
        new([FoundationReferenceRuleset.Create()]),
        RngSeed256.Parse(FoundationSessionFixture.Seed),
        AlgorithmCatalog.Phase04,
        new SchedulerGraph(descriptors));

    private static EventId ExecuteOneEvent(ulong branch, string payload, int initialTick)
    {
        SimulationSystemDescriptor descriptor = new(new("foundation.system"), SimulationPhase.Prepare, []);
        WorldSession session = SessionWithSystems([new ProposalSystem(descriptor, payload)], branch);
        if (initialTick != 0) typeof(WorldSession).GetProperty(nameof(WorldSession.CurrentTick))!.SetValue(session, new SimulationTick((UInt128)(uint)initialTick));
        session.Resume();
        return session.StepOneTick().CommittedEvents.Single().EventId;
    }

    private static ImmutableConfiguration Payload(string value) => new(new("foundation.test"), new(1, 0, 0), [new(new("foundation.value"), ConfigurationValue.FromString(value))]);
    private static FoundationIssue Failure(string code) => new(new(code), IssueSeverity.Critical, "Synthetic failure", "Nonbiological test fixture failure.");
    private static FoundationIssue Diagnostic(string code, IssueSeverity severity) => new(new(code), severity, "Synthetic diagnostic", "Nonbiological test fixture diagnostic.");

    private static void AssertTransactionFailure(WorldSession session, TickExecutionReceipt receipt, int expectedPendingCommands, ulong expectedLastCommand, string expectedIssueCode = "session.transaction-violation")
    {
        Assert.False(receipt.Success);
        Assert.Equal(expectedIssueCode, receipt.Issues.Single().Code.ToString());
        Assert.Equal(IssueSeverity.Critical, receipt.Issues.Single().Severity);
        Assert.Equal(WorldSessionStatus.Faulted, session.Status);
        Assert.Equal(UInt128.Zero, session.CurrentTick.Value);
        Assert.Equal(UInt128.Zero, session.LastEventSequence.Value);
        Assert.Equal((UInt128)expectedLastCommand, session.LastCommandSequence.Value);
        Assert.Equal(expectedPendingCommands, session.PendingCommands.Count);
        Assert.Empty(receipt.CommandsConsumed);
        Assert.Empty(receipt.CommittedEvents);
    }

    private sealed class EmptyProcessor : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = new("foundation.trace");
        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command) => OperationResult<CommandProcessorOutput>.Succeeded(CommandProcessorOutput.Empty);
    }

    private sealed class GenericProcessor(SessionCommandTypeId commandType) : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = commandType;
        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command) => OperationResult<CommandProcessorOutput>.Succeeded(CommandProcessorOutput.Empty);
    }

    private sealed class ThrowingProcessor : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = new("foundation.throw");
        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command) => throw new InvalidOperationException("synthetic command failure");
    }

    private sealed class MutationProcessor(Func<WorldSession, OperationResult> mutation) : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = new("foundation.mutate");
        public WorldSession? Session { get; set; }
        public OperationResult? MutationResult { get; private set; }
        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command)
        {
            MutationResult = mutation(Session!);
            return OperationResult<CommandProcessorOutput>.Succeeded(CommandProcessorOutput.Empty);
        }
    }

    private sealed class NestedStepProcessor : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = new("foundation.nested-step");
        public WorldSession? Session { get; set; }
        public TickExecutionReceipt? NestedReceipt { get; private set; }
        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command)
        {
            NestedReceipt = Session!.StepOneTick();
            return OperationResult<CommandProcessorOutput>.Succeeded(CommandProcessorOutput.Empty);
        }
    }

    private sealed class DiagnosticProcessor(bool emitIssues) : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = new("foundation.diagnostic");
        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command) => emitIssues
            ? OperationResult<CommandProcessorOutput>.Succeeded(
                CommandProcessorOutput.Empty,
                Diagnostic($"processor.s{command.SequenceNumber.Value}.info", IssueSeverity.Information),
                Diagnostic($"processor.s{command.SequenceNumber.Value}.warning", IssueSeverity.Warning))
            : OperationResult<CommandProcessorOutput>.Succeeded(CommandProcessorOutput.Empty);
    }

    private sealed class FailureSystem(SimulationSystemDescriptor descriptor) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context) => OperationResult<SimulationSystemOutput>.Failed(Failure("synthetic.failure"));
    }

    private sealed class ProposalSystem(SimulationSystemDescriptor descriptor, string payload) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context) => OperationResult<SimulationSystemOutput>.Succeeded(new([new(Descriptor.Phase, Descriptor.Id, UInt128.Zero, new("foundation.event"), Payload(payload))]));
    }

    private sealed class DuplicateProposalSystem(SimulationSystemDescriptor descriptor) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context) => OperationResult<SimulationSystemOutput>.Succeeded(new([
            new(Descriptor.Phase, Descriptor.Id, UInt128.Zero, new("foundation.event-a"), Payload("a")),
            new(Descriptor.Phase, Descriptor.Id, UInt128.Zero, new("foundation.event-b"), Payload("b")),
        ]));
    }

    private sealed class ManyProposalSystem(SimulationSystemDescriptor descriptor, int count) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context)
        {
            ImmutableConfiguration payload = Payload("bulk");
            WorldEventProposal[] proposals = Enumerable.Range(0, count)
                .Select(index => new WorldEventProposal(Descriptor.Phase, Descriptor.Id, (UInt128)(uint)index, new WorldEventTypeId("foundation.bulk-event"), payload))
                .ToArray();
            return OperationResult<SimulationSystemOutput>.Succeeded(new(proposals));
        }
    }

    private sealed class IssueSystem(SimulationSystemDescriptor descriptor, FoundationIssue[] issues) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context) => OperationResult<SimulationSystemOutput>.Failed(issues);
    }

    private sealed class SuccessfulIssueSystem(SimulationSystemDescriptor descriptor, FoundationIssue[] issues) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context) => OperationResult<SimulationSystemOutput>.Succeeded(SimulationSystemOutput.Empty, issues);
    }

    private sealed class DiagnosticSystem(SimulationSystemDescriptor descriptor, bool emitIssue) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context) => emitIssue
            ? OperationResult<SimulationSystemOutput>.Succeeded(
                SimulationSystemOutput.Empty,
                Diagnostic($"system.{Descriptor.Id}", Descriptor.Id.ToString().EndsWith(".a", StringComparison.Ordinal) ? IssueSeverity.Information : IssueSeverity.Warning))
            : OperationResult<SimulationSystemOutput>.Succeeded(SimulationSystemOutput.Empty);
    }

    private sealed class MutationSystem(SimulationSystemDescriptor descriptor, Func<WorldSession, OperationResult> mutation) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public WorldSession? Session { get; set; }
        public OperationResult? MutationResult { get; private set; }
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context)
        {
            MutationResult = mutation(Session!);
            return OperationResult<SimulationSystemOutput>.Succeeded(SimulationSystemOutput.Empty);
        }
    }

    private sealed class WrongThreadMutationSystem(SimulationSystemDescriptor descriptor) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public WorldSession? Session { get; set; }
        public IReadOnlyList<OperationResult> Results { get; private set; } = [];

        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context)
        {
            IReadOnlyList<OperationResult>? results = null;
            Thread thread = new(() => results =
            [
                Session!.Pause(),
                Session.SubmitCommand(new(default, new("foundation.trace"), Payload("wrong-thread"))),
            ]);
            thread.Start();
            thread.Join();
            Results = results!;
            return OperationResult<SimulationSystemOutput>.Succeeded(SimulationSystemOutput.Empty);
        }
    }

    private sealed class ReentrantSystem(SimulationSystemDescriptor descriptor) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public WorldSession? Session { get; set; }
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context)
        {
            _ = Session!.StepOneTick();
            return OperationResult<SimulationSystemOutput>.Succeeded(SimulationSystemOutput.Empty);
        }
    }

    private sealed class RecordingSystem(SimulationSystemDescriptor descriptor, List<string> order) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;
        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context)
        {
            order.Add(context.Phase.ToString());
            return OperationResult<SimulationSystemOutput>.Succeeded(SimulationSystemOutput.Empty);
        }
    }
}
