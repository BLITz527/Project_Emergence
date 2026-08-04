using System.Collections.ObjectModel;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Results;
using Emergence.Model.Environment;

namespace Emergence.Simulation.Fields;

public sealed record ChannelConservationResult(
    FieldChannelId ChannelId,
    UInt128 Total,
    UInt128 FluidCellTotal,
    UInt128 SolidCellTotal,
    int SolidCellViolationCount,
    Sha256Digest RegionStateDigest,
    IReadOnlyList<FoundationIssue> Issues);

public sealed class EnvironmentConservationAuditReport
{
    internal EnvironmentConservationAuditReport(IEnumerable<ChannelConservationResult> channels)
    {
        Channels = Array.AsReadOnly(channels.ToArray());
        Success = Channels.All(static channel => channel.SolidCellTotal == UInt128.Zero
            && channel.SolidCellViolationCount == 0
            && !channel.Issues.Any(static issue => issue.Severity is IssueSeverity.Error or IssueSeverity.Critical));
    }
    public bool Success { get; }
    public IReadOnlyList<ChannelConservationResult> Channels { get; }
}

public sealed class EnvironmentConservationAudit
{
    public EnvironmentConservationAuditReport Run(WorldEnvironmentStore environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        RegionFieldStore region = environment.Region;
        List<ChannelConservationResult> results = [];
        for (int slot = 0; slot < region.Definition.FieldChannels.Definitions.Count; slot++)
        {
            FieldChannelId channel = region.Definition.FieldChannels.Definitions[slot].Id;
            UInt128 fluid = UInt128.Zero;
            UInt128 solid = UInt128.Zero;
            int violations = 0;
            List<FoundationIssue> issues = [];
            try
            {
                for (int index = 0; index < region.Definition.CellCount; index++)
                {
                    ulong amount = region.GetAmount(slot, index).Quanta;
                    if (region.Definition.EffectiveVolumes[index].Quanta == 0)
                    {
                        solid = checked(solid + amount);
                        if (amount != 0) violations++;
                    }
                    else fluid = checked(fluid + amount);
                }
            }
            catch (OverflowException)
            {
                issues.Add(new(new("environment.audit-overflow"), IssueSeverity.Critical, "Conservation audit overflow", "Exact channel accumulation overflowed UInt128."));
            }
            UInt128 total = checked(fluid + solid);
            if (total != region.GetChannelTotal(slot))
                issues.Add(new(new("environment.audit-total"), IssueSeverity.Critical, "Conservation total mismatch", "The scanned amount total differs from the authoritative store total."));
            results.Add(new(channel, total, fluid, solid, violations, region.Digest, Array.AsReadOnly(issues.ToArray())));
        }
        return new(results);
    }
}
