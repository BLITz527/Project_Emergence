using Emergence.Foundation.Time;
using Emergence.Model;
using Emergence.Presentation.Contracts;

namespace Emergence.Simulation;

public sealed class SessionPresentationSnapshotProducer
{
    private readonly CheckedSequenceCounter _sequence = new();

    public SessionPresentationSnapshot Create(WorldSession session, TickExecutionReceipt? mostRecentReceipt = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (mostRecentReceipt is not null
            && (!mostRecentReceipt.Success
                || mostRecentReceipt.ResultingTick != session.CurrentTick
                || (mostRecentReceipt.CommittedEvents.Count > 0
                    && mostRecentReceipt.CommittedEvents[^1].SequenceNumber != session.LastEventSequence)))
        {
            throw new ArgumentException("The receipt does not describe the session's current committed state.", nameof(mostRecentReceipt));
        }
        PresentationEventSummary[] events = mostRecentReceipt is null
            ? []
            : mostRecentReceipt.CommittedEvents.Select(static item => new PresentationEventSummary(
                item.EventId,
                item.SequenceNumber,
                item.Tick,
                item.Phase,
                item.EventType,
                item.SourceSystem,
                item.Payload.Digest,
                item.CausalCommandSequence)).ToArray();
        SequenceNumber snapshotSequence = _sequence.IssueNext();
        return new SessionPresentationSnapshot(
            snapshotSequence,
            session.Definition.WorldIdentity.WorldId,
            session.Definition.BranchIdentity.BranchId,
            session.CurrentTick,
            session.Status,
            session.Definition.RulesetKey,
            session.Definition.Digest,
            session.StateDigest,
            session.PendingCommands.Count,
            session.LastCommandSequence,
            session.LastEventSequence,
            events);
    }
}
