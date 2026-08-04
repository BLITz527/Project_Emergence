using Emergence.Foundation.Hashing;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;
using Emergence.Model.Environment;

namespace Emergence.Model;

public static class WorldSessionStateFingerprint
{
    public const string DigestDomainMarker = "ProjectEmergence.WorldSessionState.v1";
    public const string EnvironmentDigestDomainMarker = "ProjectEmergence.WorldSessionState.v2";

    public static Sha256Digest Compute(
        WorldSessionDefinition definition,
        SimulationTick tick,
        WorldSessionStatus status,
        SequenceNumber lastCommand,
        SequenceNumber lastEvent,
        IReadOnlyList<AcceptedSessionCommand> pending,
        IReadOnlyList<FoundationIssue> faults)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(faults);
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteDigest(definition.Digest);
        writer.WriteUInt128(tick.Value);
        writer.WriteString(status.ToString());
        writer.WriteUInt128(lastCommand.Value);
        writer.WriteUInt128(lastEvent.Value);
        AcceptedSessionCommand[] ordered = pending
            .OrderBy(static command => command.ExecuteAtTick)
            .ThenBy(static command => command.SequenceNumber)
            .ToArray();
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

    public static Sha256Digest ComputeEnvironment(
        WorldSessionDefinition definition,
        SimulationTick tick,
        WorldSessionStatus status,
        SequenceNumber lastCommand,
        SequenceNumber lastEvent,
        IReadOnlyList<AcceptedSessionCommand> pending,
        IReadOnlyList<FoundationIssue> faults,
        WorldEnvironmentState environmentState)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(faults);
        ArgumentNullException.ThrowIfNull(environmentState);
        if (definition.FormatVersion != WorldSessionDefinition.EnvironmentFormatVersion
            || definition.EnvironmentDefinition is null
            || !definition.EnvironmentDefinition.Equals(environmentState.Definition))
            throw new ArgumentException("Environment state must match a V3 session definition.", nameof(environmentState));
        using CanonicalHashWriter writer = new();
        writer.WriteString(EnvironmentDigestDomainMarker);
        writer.WriteDigest(definition.Digest);
        writer.WriteUInt128(tick.Value);
        writer.WriteString(status.ToString());
        writer.WriteUInt128(lastCommand.Value);
        writer.WriteUInt128(lastEvent.Value);
        AcceptedSessionCommand[] ordered = pending
            .OrderBy(static command => command.ExecuteAtTick)
            .ThenBy(static command => command.SequenceNumber)
            .ToArray();
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
        writer.WriteDigest(environmentState.Digest);
        return writer.FinalizeDigest();
    }
}
