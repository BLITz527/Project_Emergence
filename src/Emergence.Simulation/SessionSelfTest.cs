using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Emergence.Foundation;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Results;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;
using Emergence.Model;

namespace Emergence.Simulation;

public static class FoundationSessionFixture
{
    public const string ExpectedAlgorithmCatalogDigest = "bbaebfc88087fc04ab024d2505b9a50ed7e7a2f21cd34a18eb4e83d56cb1a418";
    public const string ExpectedSchedulerGraphDigest = "3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a";
    public const string ExpectedDefinitionDigest = "fcc91152d376a93f558f44c2e76eb8493ab61fb519d598faa8782992d8cd3456";
    public const string Seed = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    public const string TraceCommandType = "foundation.trace";
    public const string CommandTraceEventType = "foundation.command-trace";
    public const string SystemTraceEventType = "foundation.system-trace";
    public const string CommandSystemId = "foundation.trace.command";

    public static SchedulerGraph CreateGraph() => new(
    [
        new(new("foundation.trace.command"), SimulationPhase.Commands, []),
        new(new("foundation.trace.prepare-a"), SimulationPhase.Prepare, []),
        new(new("foundation.trace.prepare-b"), SimulationPhase.Prepare, [new("foundation.trace.prepare-a")]),
        new(new("foundation.trace.evaluate"), SimulationPhase.Evaluate, []),
    ]);

    public static WorldSessionDefinition CreateDefinition(RulesetRegistry registry) => new(
        new WorldIdentity(WorldId.FromUInt64(42)),
        new BranchIdentity(WorldId.FromUInt64(42), BranchId.FromUInt64(7)),
        new RulesetKey(RulesetId.FromUInt64(1), new(1, 0, 0)),
        registry,
        RngSeed256.Parse(Seed),
        AlgorithmCatalog.Phase04,
        CreateGraph());

    public static WorldSession CreatePausedSession(RulesetRegistry registry) => new(
        CreateDefinition(registry),
        CreateSystems(),
        new CommandProcessorRegistry([new TraceCommandProcessor()]));

    public static ImmutableConfiguration TracePayload(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new ImmutableConfiguration(
            new("foundation.session-trace"),
            new(1, 0, 0),
            [new(new("trace.message"), ConfigurationValue.FromString(message))]);
    }

    private static IReadOnlyList<ISimulationSystem> CreateSystems() =>
    [
        new TraceSystem(new(new("foundation.trace.command"), SimulationPhase.Commands, []), null),
        new TraceSystem(new(new("foundation.trace.prepare-a"), SimulationPhase.Prepare, []), "prepare-a"),
        new TraceSystem(new(new("foundation.trace.prepare-b"), SimulationPhase.Prepare, [new("foundation.trace.prepare-a")]), "prepare-b"),
        new TraceSystem(new(new("foundation.trace.evaluate"), SimulationPhase.Evaluate, []), "evaluate"),
    ];

    private sealed class TraceCommandProcessor : ISessionCommandProcessor
    {
        public SessionCommandTypeId CommandType { get; } = new(TraceCommandType);

        public OperationResult<CommandProcessorOutput> Process(SimulationExecutionContext context, AcceptedSessionCommand command)
        {
            if (context.Phase != SimulationPhase.Commands) return OperationResult<CommandProcessorOutput>.Failed(Failure("trace.command-phase", "Trace command processor requires Commands phase."));
            return OperationResult<CommandProcessorOutput>.Succeeded(new CommandProcessorOutput(
            [
                new(
                    SimulationPhase.Commands,
                    new(CommandSystemId),
                    command.SequenceNumber.Value,
                    new(CommandTraceEventType),
                    command.Payload,
                    command.SequenceNumber),
            ]));
        }
    }

    private sealed class TraceSystem(SimulationSystemDescriptor descriptor, string? messagePrefix) : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = descriptor;

        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context)
        {
            if (messagePrefix is null) return OperationResult<SimulationSystemOutput>.Succeeded(SimulationSystemOutput.Empty);
            return OperationResult<SimulationSystemOutput>.Succeeded(new SimulationSystemOutput(
            [
                new(
                    Descriptor.Phase,
                    Descriptor.Id,
                    UInt128.Zero,
                    new(SystemTraceEventType),
                    TracePayload($"{messagePrefix}:{context.Tick}")),
            ]));
        }
    }

    private static FoundationIssue Failure(string code, string detail) => new(new(code), IssueSeverity.Error, "Foundation trace failure", detail);
}

public sealed record SessionSelfTestReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] string Phase,
    [property: JsonPropertyOrder(2)] string Version,
    [property: JsonPropertyOrder(3)] string GitCommit,
    [property: JsonPropertyOrder(4)] string AlgorithmCatalogDigest,
    [property: JsonPropertyOrder(5)] string SchedulerGraphDigest,
    [property: JsonPropertyOrder(6)] string SessionDefinitionDigest,
    [property: JsonPropertyOrder(7)] string SessionTraceDigest,
    [property: JsonPropertyOrder(8)] string FinalStateDigest,
    [property: JsonPropertyOrder(9)] SimulationTick FinalTick,
    [property: JsonPropertyOrder(10)] int AcceptedCommands,
    [property: JsonPropertyOrder(11)] int CommittedEvents,
    [property: JsonPropertyOrder(12)] IReadOnlyList<string> EventIds,
    [property: JsonPropertyOrder(13)] IReadOnlyList<DiagnosticCheck> Checks);

public static class SessionSelfTest
{
    public const string ExpectedSessionTraceDigest = "58f7313342790881b43875ba1bf3461e2aa8b1dd4b23d19278dd32cd973a7491";
    public const string ExpectedFinalStateDigest = "6de0d3bee6901dfdd83b080545ce58efcd86a2b52bf67f21692a947d19fb9ff0";
    public static IReadOnlyList<string> ExpectedEventIds { get; } = Array.AsReadOnly(new[]
    {
        "4598305249711c692a4e067efca02ee9",
        "42abdecf1c4b67c932774281ef342a58",
        "dbd65e20c6c2ea23b9d8a57d51820e3a",
        "21bc5536586ec3ca10486cf27925c6e6",
        "6f91cd4dd0c768ff9d210fef527e978f",
        "d94aa1148c34320c397278b2af0d3e9e",
        "79fa3e441504547adc4f252fed87ade9",
        "4d5a5e7f1c2c38c29e375984d1c9cc93",
        "97bf2d51795ee43365d08aa9e1c085f4",
        "54ace7a336281d6a987af49193031dd6",
    });

    public static SessionSelfTestReport Run()
    {
        RulesetRegistry registry = new([FoundationReferenceRuleset.Create()]);
        WorldSession session = FoundationSessionFixture.CreatePausedSession(registry);
        List<AcceptedSessionCommand> accepted = [];
        accepted.Add(Submit(session, 0, "gamma"));
        accepted.Add(Submit(session, 1, "alpha"));
        accepted.Add(Submit(session, 0, "delta"));
        accepted.Add(Submit(session, 1, "beta"));
        OperationResult resume = session.Resume();
        TickExecutionReceipt tick0 = session.StepOneTick();
        TickExecutionReceipt tick1 = session.StepOneTick();
        OperationResult pause = session.Pause();
        CommittedWorldEvent[] events = tick0.CommittedEvents.Concat(tick1.CommittedEvents).ToArray();
        string traceDigest = ComputeTraceDigest(session, events).ToString();
        string stateDigest = session.StateDigest.ToString();
        string[] eventIds = events.Select(static item => item.EventId.ToString()).ToArray();

        List<DiagnosticCheck> checks =
        [
            Check("session.algorithm-catalog", AlgorithmCatalog.Phase04.Digest.ToString() == FoundationSessionFixture.ExpectedAlgorithmCatalogDigest, "Phase 0.4 algorithm catalog", AlgorithmCatalog.Phase04.Digest.ToString()),
            Check("session.scheduler-graph", session.Definition.SchedulerGraphDigest.ToString() == FoundationSessionFixture.ExpectedSchedulerGraphDigest, "Scheduler graph digest", session.Definition.SchedulerGraphDigest.ToString()),
            Check("session.definition", session.Definition.Digest.ToString() == FoundationSessionFixture.ExpectedDefinitionDigest, "Session definition digest", session.Definition.Digest.ToString()),
            Check("session.command-sequences", accepted.Select(static item => item.SequenceNumber.Value).SequenceEqual(new UInt128[] { 1, 2, 3, 4 }), "Accepted command sequences", string.Join(",", accepted.Select(static item => item.SequenceNumber))),
            Check("session.tick-zero", tick0.Success && tick0.CommittedEvents.Count == 5, "Tick zero committed events", tick0.CommittedEvents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Check("session.tick-one", tick1.Success && tick1.CommittedEvents.Count == 5, "Tick one committed events", tick1.CommittedEvents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Check("session.lifecycle", resume.Success && pause.Success && session.Status == WorldSessionStatus.Paused && session.CurrentTick.Value == 2, "Session lifecycle", $"{session.Status};tick={session.CurrentTick}"),
            Check("session.event-ids", eventIds.SequenceEqual(ExpectedEventIds), "Committed EventIds", string.Join(",", eventIds)),
            Check("session.trace", traceDigest == ExpectedSessionTraceDigest, "Session trace digest", traceDigest),
            Check("session.state", stateDigest == ExpectedFinalStateDigest, "Final state digest", stateDigest),
        ];

        return new SessionSelfTestReport(
            checks.All(static item => item.Severity == DiagnosticSeverity.Success),
            "M0 Phase 0.4R",
            BuildInfo.Current.SemanticVersion,
            BuildInfo.Current.GitCommit,
            AlgorithmCatalog.Phase04.Digest.ToString(),
            session.Definition.SchedulerGraphDigest.ToString(),
            session.Definition.Digest.ToString(),
            traceDigest,
            stateDigest,
            session.CurrentTick,
            accepted.Count,
            events.Length,
            Array.AsReadOnly(eventIds),
            checks.AsReadOnly());
    }

    private static AcceptedSessionCommand Submit(WorldSession session, ulong tick, string message)
    {
        OperationResult<AcceptedSessionCommand> result = session.SubmitCommand(new SessionCommandRequest(new SimulationTick(tick), new(FoundationSessionFixture.TraceCommandType), FoundationSessionFixture.TracePayload(message)));
        return result.Success ? result.Value : throw new InvalidOperationException(string.Join("; ", result.Issues.Select(static item => item.Detail)));
    }

    private static Sha256Digest ComputeTraceDigest(WorldSession session, IReadOnlyList<CommittedWorldEvent> events)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString("ProjectEmergence.SessionTrace.v1");
        writer.WriteDigest(session.Definition.Digest);
        writer.WriteDigest(session.Definition.SchedulerGraphDigest);
        writer.WriteUInt64(checked((ulong)events.Count));
        foreach (CommittedWorldEvent item in events)
        {
            writer.WriteString(item.EventId.ToString());
            writer.WriteUInt128(item.SequenceNumber.Value);
            writer.WriteUInt128(item.Tick.Value);
            writer.WriteString(item.Phase.ToString());
            writer.WriteString(item.SourceSystem.ToString());
            writer.WriteString(item.EventType.ToString());
            writer.WriteDigest(item.Payload.Digest);
            writer.WriteBoolean(item.CausalCommandSequence.HasValue);
            if (item.CausalCommandSequence.HasValue) writer.WriteUInt128(item.CausalCommandSequence.Value.Value);
        }
        writer.WriteUInt128(session.CurrentTick.Value);
        writer.WriteUInt128(session.LastCommandSequence.Value);
        writer.WriteUInt128(session.LastEventSequence.Value);
        writer.WriteUInt64(checked((ulong)session.PendingCommands.Count));
        return writer.FinalizeDigest();
    }

    private static DiagnosticCheck Check(string id, bool success, string summary, string detail) => new(id, success ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, summary, detail);
}
