using Emergence.Foundation.Results;
using Emergence.Foundation.Versioning;
using Emergence.Model;

namespace Emergence.Simulation;

public static class SessionCompatibilityValidator
{
    public static OperationResult Validate(
        WorldSessionSnapshot snapshot,
        IEnumerable<ISimulationSystem> systems,
        CommandProcessorRegistry commandProcessors)
    {
        List<FoundationIssue> issues = [];
        if (snapshot is null)
        {
            issues.Add(Issue("compatibility.snapshot", "Missing snapshot", "A validated world-session snapshot is required."));
            return OperationResult.FromIssues(issues);
        }
        if (systems is null)
        {
            issues.Add(Issue("compatibility.systems", "Missing systems", "Explicit simulation systems are required."));
            return OperationResult.FromIssues(issues);
        }
        if (commandProcessors is null)
        {
            issues.Add(Issue("compatibility.processors", "Missing processors", "An explicit command processor registry is required."));
            return OperationResult.FromIssues(issues);
        }

        WorldSessionDefinition definition = snapshot.Definition;
        if (snapshot.FormatVersion != WorldSessionSnapshot.SupportedFormatVersion)
            issues.Add(Issue("compatibility.snapshot-format", "Unsupported snapshot format", snapshot.FormatVersion.ToString()));
        if (definition.FormatVersion != WorldSessionDefinition.SaveableFormatVersion)
            issues.Add(Issue("compatibility.definition-format", "Unsupported definition format", definition.FormatVersion.ToString()));
        if (!definition.RuntimeAlgorithms.Equals(AlgorithmCatalog.Phase05)
            || definition.RuntimeAlgorithms.Digest != AlgorithmCatalog.Phase05.Digest)
            issues.Add(Issue("compatibility.algorithms", "Runtime algorithm mismatch", definition.RuntimeAlgorithms.Digest.ToString()));
        if (definition.CommandProcessorCatalog is null || !definition.CommandProcessorCatalog.Equals(commandProcessors.Catalog))
            issues.Add(Issue("compatibility.command-catalog", "Command processor catalog mismatch", commandProcessors.Catalog.Digest.ToString()));

        ISimulationSystem?[] supplied = systems.Cast<ISimulationSystem?>().ToArray();
        if (supplied.Any(static item => item is null))
        {
            issues.Add(Issue("compatibility.system-null", "Null simulation system", "The supplied system collection contains null."));
        }
        else
        {
            ISimulationSystem[] registered = supplied.Select(static item => item!).OrderBy(static item => item.Descriptor.Id).ToArray();
            if (registered.Select(static item => item.Descriptor.Id).Distinct().Count() != registered.Length
                || registered.Length != definition.SchedulerGraph.Systems.Count
                || registered.Any(system => !definition.SchedulerGraph.TryGet(system.Descriptor.Id, out SimulationSystemDescriptor? expected)
                    || expected is null || !system.Descriptor.Equals(expected)))
                issues.Add(Issue("compatibility.scheduler", "Scheduler system mismatch", definition.SchedulerGraphDigest.ToString()));
        }

        foreach (AcceptedSessionCommand command in snapshot.PendingCommands)
        {
            if (!commandProcessors.Contains(command.CommandType))
                issues.Add(Issue("compatibility.pending-command", "Pending command processor missing", command.CommandType.ToString()));
        }
        if (!definition.RulesetRegistry.TryGet(definition.RulesetKey, out var descriptor)
            || descriptor is null
            || descriptor.Digest != definition.RulesetDescriptorDigest
            || definition.RulesetRegistry.Digest != definition.RulesetRegistryDigest)
            issues.Add(Issue("compatibility.ruleset", "Ruleset compatibility mismatch", definition.RulesetKey.ToString()));

        return OperationResult.FromIssues(issues);
    }

    private static FoundationIssue Issue(string code, string summary, string detail) =>
        new(new(code), IssueSeverity.Error, summary, detail);
}
