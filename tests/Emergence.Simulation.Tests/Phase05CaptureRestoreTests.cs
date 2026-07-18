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

public sealed class Phase05CaptureRestoreTests
{
    [Fact]
    public void PausedCaptureIsRepeatableAndConsumesNoStateCounterTickOrRng()
    {
        WorldSession session = TickTwoSession();
        string state = session.StateDigest.ToString();
        SequenceNumber command = session.LastCommandSequence;
        SequenceNumber events = session.LastEventSequence;
        string rng = Sample(session);
        OperationResult<WorldSessionSnapshot> first = session.CaptureSnapshot();
        OperationResult<WorldSessionSnapshot> second = session.CaptureSnapshot();
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(state, session.StateDigest.ToString());
        Assert.Equal(command, session.LastCommandSequence);
        Assert.Equal(events, session.LastEventSequence);
        Assert.Equal((UInt128)2, session.CurrentTick.Value);
        Assert.Equal(rng, Sample(session));
    }

    [Fact]
    public async Task ReadyAndWrongThreadCaptureFailWithoutMutation()
    {
        WorldSession ready = FoundationSessionFixture.CreatePhase05PausedSession(Registry());
        Assert.True(ready.Resume().Success);
        string before = ready.StateDigest.ToString();
        Assert.False(ready.CaptureSnapshot().Success);
        Assert.Equal(before, ready.StateDigest.ToString());

        WorldSession paused = TickTwoSession();
        before = paused.StateDigest.ToString();
        OperationResult<WorldSessionSnapshot> wrongThread = await Task.Run(paused.CaptureSnapshot);
        Assert.False(wrongThread.Success);
        Assert.Equal(before, paused.StateDigest.ToString());
    }

    [Fact]
    public void CallbackCapturePoisonsOuterTransactionAndFaultedCaptureSucceeds()
    {
        CapturingProcessor processor = new();
        CommandProcessorRegistry processors = new([processor]);
        WorldSessionDefinition definition = Definition(new SchedulerGraph([]), processors.Catalog);
        WorldSession session = new(definition, [], processors);
        processor.Session = session;
        Assert.True(session.SubmitCommand(new(default, processor.CommandType, Payload("capture"))).Success);
        Assert.True(session.Resume().Success);
        TickExecutionReceipt receipt = session.StepOneTick();
        Assert.False(receipt.Success);
        Assert.Equal(WorldSessionStatus.Faulted, session.Status);
        Assert.Equal(UInt128.Zero, session.CurrentTick.Value);
        Assert.Equal((UInt128)1, session.LastCommandSequence.Value);
        Assert.Equal(UInt128.Zero, session.LastEventSequence.Value);
        OperationResult<WorldSessionSnapshot> capture = session.CaptureSnapshot();
        Assert.True(capture.Success);
        Assert.NotEmpty(capture.Value.FaultIssues);
    }

    [Fact]
    public async Task RestoreReconstructsExactPausedStateQueueCountersAndOwnerThread()
    {
        WorldSession source = TickTwoSession();
        AcceptedSessionCommand future = Submit(source, 4, "future");
        WorldSessionSnapshot snapshot = source.CaptureSnapshot().Value;
        OperationResult<WorldSession> restoration = WorldSession.Restore(snapshot, FoundationSessionFixture.CreateSystems(), FoundationSessionFixture.CreateCommandProcessorRegistry());
        Assert.True(restoration.Success);
        WorldSession restored = restoration.Value;
        Assert.Equal(snapshot.StateDigest, restored.StateDigest);
        Assert.Equal(snapshot.CurrentTick, restored.CurrentTick);
        Assert.Equal(snapshot.Status, restored.Status);
        Assert.Equal(snapshot.LastCommandSequence, restored.LastCommandSequence);
        Assert.Equal(snapshot.LastEventSequence, restored.LastEventSequence);
        Assert.Single(restored.PendingCommands);
        Assert.Equal(future.SequenceNumber, restored.PendingCommands[0].SequenceNumber);

        OperationResult<WorldSession> otherThread = await Task.Run(() => WorldSession.Restore(
            snapshot, FoundationSessionFixture.CreateSystems(), FoundationSessionFixture.CreateCommandProcessorRegistry()));
        Assert.True(otherThread.Success);
        Assert.False(otherThread.Value.Pause().Success);
    }

    [Fact]
    public void RestoreReconstructsFaultIssuesWithoutCallbacks()
    {
        CapturingProcessor processor = new();
        CommandProcessorRegistry processors = new([processor]);
        WorldSession session = new(Definition(new SchedulerGraph([]), processors.Catalog), [], processors);
        processor.Session = session;
        Submit(session, 0, "capture", processor.CommandType);
        session.Resume();
        session.StepOneTick();
        WorldSessionSnapshot snapshot = session.CaptureSnapshot().Value;
        CapturingProcessor replacement = new();
        CommandProcessorRegistry replacementRegistry = new([replacement]);
        OperationResult<WorldSession> restoration = WorldSession.Restore(snapshot, [], replacementRegistry);
        Assert.True(restoration.Success);
        replacement.Session = restoration.Value;
        Assert.Equal(WorldSessionStatus.Faulted, restoration.Value.Status);
        Assert.Equal(snapshot.FaultIssues, restoration.Value.FaultIssues);
        Assert.Equal(0, replacement.InvocationCount);
    }

    [Fact]
    public void RestoreRejectsProcessorAndSchedulerMismatchWithoutPartialSession()
    {
        WorldSessionSnapshot snapshot = TickTwoSession().CaptureSnapshot().Value;
        OperationResult<WorldSession> processors = WorldSession.Restore(snapshot, FoundationSessionFixture.CreateSystems(), new CommandProcessorRegistry([]));
        Assert.False(processors.Success);
        Assert.False(processors.HasValue);
        OperationResult<WorldSession> systems = WorldSession.Restore(snapshot, [], FoundationSessionFixture.CreateCommandProcessorRegistry());
        Assert.False(systems.Success);
        Assert.False(systems.HasValue);
    }

    [Fact]
    public void CompatibilityRequiresV2Phase05CatalogAndRegisteredPendingProcessors()
    {
        WorldSessionSnapshot snapshot = TickTwoSession().CaptureSnapshot().Value;
        Assert.True(SessionCompatibilityValidator.Validate(snapshot, FoundationSessionFixture.CreateSystems(), FoundationSessionFixture.CreateCommandProcessorRegistry()).Success);
        Assert.False(SessionCompatibilityValidator.Validate(snapshot, FoundationSessionFixture.CreateSystems(), new CommandProcessorRegistry([])).Success);
        WorldSessionDefinition v1 = FoundationSessionFixture.CreateDefinition(Registry());
        Assert.Throws<ArgumentException>(() => new WorldSessionSnapshot(v1, default, WorldSessionStatus.Paused, default, default, [], [], default));
    }

    [Fact]
    public void OriginalAndRestoredContinuationMatchEveryLockedVector()
    {
        WorldSession original = TickTwoSession();
        WorldSessionSnapshot snapshot = original.CaptureSnapshot().Value;
        WorldSession restored = WorldSession.Restore(snapshot, FoundationSessionFixture.CreateSystems(), FoundationSessionFixture.CreateCommandProcessorRegistry()).Value;
        string rng = Sample(original);
        Assert.Equal(rng, Sample(restored));
        AcceptedSessionCommand first = Submit(original, 2, "epsilon");
        AcceptedSessionCommand second = Submit(restored, 2, "epsilon");
        Assert.Equal((UInt128)5, first.SequenceNumber.Value);
        Assert.Equal(first.SequenceNumber, second.SequenceNumber);
        original.Resume();
        restored.Resume();
        TickExecutionReceipt firstReceipt = original.StepOneTick();
        TickExecutionReceipt secondReceipt = restored.StepOneTick();
        original.Pause();
        restored.Pause();
        Assert.Equal(JsonDefaults.Serialize(firstReceipt, false), JsonDefaults.Serialize(secondReceipt, false));
        Assert.Equal(new[]
        {
            "8adf4015e21a6e9b4d67bf735ca95840",
            "eaf3454d0b583165c89d3d785a483e7b",
            "3ca4b0b1f20eab439cca3a7d874531ef",
            "521e2a0fa467efc0f2fac2601f1194f3",
        }, firstReceipt.CommittedEvents.Select(static item => item.EventId.ToString()));
        Assert.Equal((UInt128)11, firstReceipt.CommittedEvents[0].SequenceNumber.Value);
        Assert.Equal((UInt128)14, firstReceipt.CommittedEvents[^1].SequenceNumber.Value);
        Assert.Equal("fb303204175f2ed6186755e9d8ff8877bcc60892554e4765f52a4224f9f706dd", original.StateDigest.ToString());
        Assert.Equal(original.StateDigest, restored.StateDigest);
    }

    private static WorldSession TickTwoSession()
    {
        WorldSession session = FoundationSessionFixture.CreatePhase05PausedSession(Registry());
        Submit(session, 0, "gamma");
        Submit(session, 1, "alpha");
        Submit(session, 0, "delta");
        Submit(session, 1, "beta");
        session.Resume();
        Assert.True(session.StepOneTick().Success);
        Assert.True(session.StepOneTick().Success);
        Assert.True(session.Pause().Success);
        return session;
    }

    private static AcceptedSessionCommand Submit(WorldSession session, ulong tick, string message, SessionCommandTypeId? type = null)
    {
        OperationResult<AcceptedSessionCommand> result = session.SubmitCommand(new(new(tick), type ?? new(FoundationSessionFixture.TraceCommandType), Payload(message)));
        Assert.True(result.Success);
        return result.Value;
    }

    private static string Sample(WorldSession session)
    {
        RngSampleAddress address = new(new("foundation.self-test"), RngScopeKey.Parse(FoundationRngSelfTest.Scope), 42);
        return new DeterministicAddressedRng(session.Definition.RootSeed, session.Definition.SelectedRuleset.RngDomains).GenerateBlock(address).ToString();
    }

    private static WorldSessionDefinition Definition(SchedulerGraph graph, CommandProcessorCatalog catalog) => new(
        new WorldIdentity(WorldId.FromUInt64(42)),
        new BranchIdentity(WorldId.FromUInt64(42), BranchId.FromUInt64(7)),
        new RulesetKey(RulesetId.FromUInt64(1), new(1, 0, 0)),
        Registry(),
        RngSeed256.Parse(FoundationSessionFixture.Seed),
        AlgorithmCatalog.Phase05,
        graph,
        catalog);

    private static RulesetRegistry Registry() => new([FoundationReferenceRuleset.Create()]);
    private static ImmutableConfiguration Payload(string value) => FoundationSessionFixture.TracePayload(value);

    private sealed class CapturingProcessor : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = new("foundation.capture");
        public WorldSession? Session { get; set; }
        public int InvocationCount { get; private set; }
        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command)
        {
            InvocationCount++;
            _ = Session!.CaptureSnapshot();
            return OperationResult<CommandProcessorOutput>.Succeeded(CommandProcessorOutput.Empty);
        }
    }
}
