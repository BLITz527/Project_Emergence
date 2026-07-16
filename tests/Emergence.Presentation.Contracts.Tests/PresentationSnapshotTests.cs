using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Time;
using Emergence.Model;
using Emergence.Presentation.Contracts;
using Emergence.Simulation;

namespace Emergence.Presentation.Contracts.Tests;

public sealed class PresentationSnapshotTests
{
    [Fact]
    public void SnapshotFieldsMatchPausedSessionAndDeclareNoBiology()
    {
        WorldSession session = Session();
        SessionPresentationSnapshot snapshot = new SessionPresentationSnapshotProducer().Create(session);
        Assert.Equal(session.Definition.WorldIdentity.WorldId, snapshot.WorldId);
        Assert.Equal(session.Definition.BranchIdentity.BranchId, snapshot.BranchId);
        Assert.Equal(session.CurrentTick, snapshot.Tick);
        Assert.Equal(session.Status, snapshot.Status);
        Assert.Equal(session.Definition.Digest, snapshot.SessionDefinitionDigest);
        Assert.Equal(session.StateDigest, snapshot.StateDigest);
        Assert.False(snapshot.HasBiologicalState);
    }

    [Fact]
    public void SnapshotCreationDoesNotMutateAuthoritativeSessionOrSequences()
    {
        WorldSession session = Session();
        string before = session.StateDigest.ToString();
        SimulationTick tick = session.CurrentTick;
        SequenceNumber command = session.LastCommandSequence;
        SequenceNumber worldEvent = session.LastEventSequence;
        SessionPresentationSnapshotProducer producer = new();
        producer.Create(session);
        producer.Create(session);
        Assert.Equal(before, session.StateDigest.ToString());
        Assert.Equal(tick, session.CurrentTick);
        Assert.Equal(command, session.LastCommandSequence);
        Assert.Equal(worldEvent, session.LastEventSequence);
    }

    [Fact]
    public void RepeatedSnapshotsDifferOnlyByPresentationSequence()
    {
        WorldSession session = Session();
        SessionPresentationSnapshotProducer producer = new();
        SessionPresentationSnapshot first = producer.Create(session);
        SessionPresentationSnapshot second = producer.Create(session);
        Assert.Equal((UInt128)1, first.SequenceNumber.Value);
        Assert.Equal((UInt128)2, second.SequenceNumber.Value);
        Assert.Equal(first.StateDigest, second.StateDigest);
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.RecentEvents, second.RecentEvents);
    }

    [Fact]
    public void EventSummariesMatchCommittedReceipt()
    {
        WorldSession session = Session();
        session.SubmitCommand(new(default, new(FoundationSessionFixture.TraceCommandType), FoundationSessionFixture.TracePayload("gamma")));
        session.Resume();
        TickExecutionReceipt receipt = session.StepOneTick();
        SessionPresentationSnapshot snapshot = new SessionPresentationSnapshotProducer().Create(session, receipt);
        Assert.Equal(receipt.CommittedEvents.Count, snapshot.RecentEvents.Count);
        for (int index = 0; index < receipt.CommittedEvents.Count; index++)
        {
            Assert.Equal(receipt.CommittedEvents[index].EventId, snapshot.RecentEvents[index].EventId);
            Assert.Equal(receipt.CommittedEvents[index].Payload.Digest, snapshot.RecentEvents[index].PayloadDigest);
        }
    }

    [Fact]
    public void StaleReceiptCannotBeAttachedToANewerSnapshot()
    {
        WorldSession session = Session();
        session.Resume();
        TickExecutionReceipt stale = session.StepOneTick();
        Assert.True(session.StepOneTick().Success);
        Assert.Throws<ArgumentException>(() => new SessionPresentationSnapshotProducer().Create(session, stale));
    }

    [Fact]
    public void SnapshotDefensivelyCopiesEventSummaries()
    {
        List<PresentationEventSummary> source =
        [
            new(EventId.FromUInt64(1), new(1), default, SimulationPhase.Prepare, new("foundation.event"), new("foundation.system"), default, null),
        ];
        SessionPresentationSnapshot snapshot = new(
            new(1),
            WorldId.FromUInt64(42),
            BranchId.FromUInt64(7),
            default,
            WorldSessionStatus.Paused,
            new(RulesetId.FromUInt64(1), new(1, 0, 0)),
            default,
            default,
            0,
            default,
            default,
            source);
        source.Clear();
        Assert.Single(snapshot.RecentEvents);
        Assert.Throws<NotSupportedException>(() => ((IList<PresentationEventSummary>)snapshot.RecentEvents).Clear());
    }

    [Fact]
    public void SnapshotJsonPropertyOrderIsStableAndDeterministic()
    {
        SessionPresentationSnapshot snapshot = new SessionPresentationSnapshotProducer().Create(Session());
        string first = JsonDefaults.Serialize(snapshot);
        string second = JsonDefaults.Serialize(snapshot);
        Assert.Equal(first, second);
        using JsonDocument document = JsonDocument.Parse(first);
        Assert.Equal(new[]
        {
            "sequenceNumber", "worldId", "branchId", "tick", "status", "rulesetKey", "sessionDefinitionDigest",
            "stateDigest", "pendingCommandCount", "lastCommandSequence", "lastEventSequence", "recentEvents", "hasBiologicalState",
        }, document.RootElement.EnumerateObject().Select(static item => item.Name));
    }

    [Fact]
    public void PresentationContractsHaveNoGodotOrSessionMutationSurface()
    {
        Assert.DoesNotContain(typeof(SessionPresentationSnapshot).Assembly.GetReferencedAssemblies(), static item => item.Name?.StartsWith("Godot", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(typeof(SessionPresentationSnapshot).GetMethods(), static method => method.ReturnType == typeof(WorldSession));
        Assert.All(typeof(SessionPresentationSnapshot).GetProperties(), static property => Assert.False(property.SetMethod?.IsPublic == true));
        Assert.All(typeof(PresentationEventSummary).GetProperties(), static property => Assert.False(property.SetMethod?.IsPublic == true));
    }

    private static WorldSession Session() => FoundationSessionFixture.CreatePausedSession(new RulesetRegistry([FoundationReferenceRuleset.Create()]));
}
