using System.Reflection;
using Emergence.Foundation;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Results;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Model.Environment;
using Emergence.Simulation.Fields;

namespace Emergence.Simulation.Tests;

public sealed class EnvironmentSessionIntegrationTests
{
    [Fact]
    public void ReferenceV3SessionOwnsExactStaticEnvironmentAndLockedFingerprints()
    {
        WorldSession session = EnvironmentSessionFixture.CreatePausedSession();

        Assert.True(session.Definition.HasEnvironment);
        Assert.NotNull(session.EnvironmentState);
        Assert.Equal(EnvironmentSessionFixture.ExpectedDefinitionDigest, session.Definition.Digest.ToString());
        Assert.Equal(EnvironmentSessionFixture.ExpectedStateDigest, session.StateDigest.ToString());
        Assert.Equal(ReferenceEnvironmentFixture.ExpectedEnvironmentStateDigest, session.EnvironmentState!.Digest.ToString());

        WorldSessionSnapshot first = session.CaptureSnapshot().Value;
        WorldSessionSnapshot second = session.CaptureSnapshot().Value;
        Assert.Equal(JsonDefaults.Serialize(first, false), JsonDefaults.Serialize(second, false));
        Assert.Equal(EnvironmentSessionFixture.ExpectedSnapshotDigest, first.Digest.ToString());
        Assert.Equal(first.EnvironmentState, session.EnvironmentState);
    }

    [Fact]
    public void RestoreAndOneTickPreserveEveryEnvironmentAmountAndDigest()
    {
        WorldSession original = EnvironmentSessionFixture.CreatePausedSession();
        WorldSessionSnapshot snapshot = original.CaptureSnapshot().Value;
        OperationResult<WorldSession> restoration = WorldSession.Restore(
            snapshot,
            FoundationSessionFixture.CreateSystems(),
            FoundationSessionFixture.CreateCommandProcessorRegistry());

        Assert.True(restoration.Success);
        WorldSession restored = restoration.Value;
        Assert.Equal(snapshot.EnvironmentState, restored.EnvironmentState);
        Assert.Equal(snapshot.StateDigest, restored.StateDigest);

        WorldEnvironmentState before = original.EnvironmentState!;
        Assert.True(original.Resume().Success);
        Assert.True(restored.Resume().Success);
        TickExecutionReceipt first = original.StepOneTick();
        TickExecutionReceipt second = restored.StepOneTick();
        Assert.True(first.Success);
        Assert.Equal(JsonDefaults.Serialize(first, false), JsonDefaults.Serialize(second, false));
        Assert.True(original.Pause().Success);
        Assert.True(restored.Pause().Success);
        Assert.Equal(before, original.EnvironmentState);
        Assert.Equal(before, restored.EnvironmentState);
        Assert.Equal(original.StateDigest, restored.StateDigest);
    }

    [Fact]
    public void FailedTickAndExecutionContextCannotMutateEnvironment()
    {
        FailingSystem failing = new();
        WorldSessionDefinition reference = EnvironmentSessionFixture.CreateDefinition();
        RulesetRegistry rulesets = new([FoundationReferenceRuleset.Create()]);
        WorldSessionDefinition definition = new(
            new WorldIdentity(WorldId.FromUInt64(42)),
            new BranchIdentity(WorldId.FromUInt64(42), BranchId.FromUInt64(7)),
            new RulesetKey(RulesetId.FromUInt64(1), new(1, 0, 0)),
            rulesets,
            RngSeed256.Parse(FoundationSessionFixture.Seed),
            AlgorithmCatalog.Phase11,
            new SchedulerGraph([failing.Descriptor]),
            new CommandProcessorCatalog([]),
            reference.EnvironmentDefinition!);
        WorldSession session = new(definition, [failing], new CommandProcessorRegistry([]), ReferenceEnvironmentFixture.CreateStore());
        WorldEnvironmentState before = session.EnvironmentState!;

        Assert.True(session.Resume().Success);
        TickExecutionReceipt receipt = session.StepOneTick();

        Assert.False(receipt.Success);
        Assert.Equal(WorldSessionStatus.Faulted, session.Status);
        Assert.Equal(before, session.EnvironmentState);
        Assert.NotNull(failing.ObservedEnvironment);
        Assert.DoesNotContain(
            typeof(WorldEnvironmentState).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.Name.Contains("Set", StringComparison.Ordinal)
                || method.Name.Contains("Update", StringComparison.Ordinal)
                || method.ReturnType.IsArray);
    }

    private sealed class FailingSystem : ISimulationSystem
    {
        public SimulationSystemDescriptor Descriptor { get; } = new(
            new SimulationSystemId("environment.failing-system"),
            SimulationPhase.Prepare,
            Array.Empty<SimulationSystemId>());

        public WorldEnvironmentState? ObservedEnvironment { get; private set; }

        public OperationResult<SimulationSystemOutput> Execute(SimulationExecutionContext context)
        {
            ObservedEnvironment = context.EnvironmentState;
            return OperationResult<SimulationSystemOutput>.Failed(new FoundationIssue(
                new("environment.synthetic-failure"),
                IssueSeverity.Error,
                "Synthetic environment failure",
                "Used to prove failed ticks do not mutate Phase 1.1 fields."));
        }
    }
}
