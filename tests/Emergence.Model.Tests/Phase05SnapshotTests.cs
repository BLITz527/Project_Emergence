using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

namespace Emergence.Model.Tests;

public sealed class Phase05SnapshotTests
{
    [Fact]
    public void Phase05CatalogExtendsPhase04WithLockedDigest()
    {
        Assert.Equal("bbaebfc88087fc04ab024d2505b9a50ed7e7a2f21cd34a18eb4e83d56cb1a418", AlgorithmCatalog.Phase04.Digest.ToString());
        Assert.Equal("78818c4c6a6a4aeb498a634e4cd77e5854c3fa35be2d075aabb888cb0fe7d9a1", AlgorithmCatalog.Phase05.Digest.ToString());
        Assert.All(AlgorithmCatalog.Phase04.Entries, entry => Assert.Contains(entry, AlgorithmCatalog.Phase05.Entries));
        Assert.Equal(AlgorithmCatalog.Phase05, new AlgorithmCatalog(AlgorithmCatalog.Phase05.Entries.Reverse()));
    }

    [Fact]
    public void CommandProcessorCatalogSortsCopiesHashesAndRoundTripsStrictly()
    {
        SessionCommandTypeId[] source = [new("foundation.z"), new("foundation.trace")];
        CommandProcessorCatalog catalog = new(source);
        source[0] = new("foundation.changed");
        Assert.Equal(["foundation.trace", "foundation.z"], catalog.CommandTypes.Select(static item => item.ToString()));
        Assert.Throws<NotSupportedException>(() => ((IList<SessionCommandTypeId>)catalog.CommandTypes).Clear());
        Assert.Equal(catalog, JsonSerializer.Deserialize<CommandProcessorCatalog>(JsonSerializer.Serialize(catalog, JsonDefaults.Compact), JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CommandProcessorCatalog>(JsonSerializer.Serialize(catalog, JsonDefaults.Compact).Replace("\"digest\":", "\"unknown\":0,\"digest\":", StringComparison.Ordinal), JsonDefaults.Compact));
    }

    [Fact]
    public void CommandProcessorCatalogRejectsDefaultDuplicateAndOneOverLimit()
    {
        Assert.Throws<ArgumentException>(() => new CommandProcessorCatalog([default]));
        Assert.Throws<ArgumentException>(() => new CommandProcessorCatalog([new("foundation.trace"), new("foundation.trace")]));
        SessionCommandTypeId[] tooMany = Enumerable.Range(0, SessionTechnicalLimits.MaxCommandProcessors + 1)
            .Select(index => new SessionCommandTypeId($"foundation.command.c{index:D3}")).ToArray();
        Assert.Throws<ArgumentException>(() => new CommandProcessorCatalog(tooMany));
    }

    [Fact]
    public void FixtureCommandCatalogAndV2DefinitionMatchLockedDigests()
    {
        Assert.Equal("e2555f63b5b4c9644229336da1856f35c8dabf3cf54765e224d3c51e19a3d8f6", Catalog().Digest.ToString());
        WorldSessionDefinition definition = Definition();
        Assert.Equal(new SemanticVersion(2, 0, 0), definition.FormatVersion);
        Assert.Equal("ca024a17b1e0ee02b57d639bea1f57d0f04154e6c3da501fd24af0ebe9798e0e", definition.Digest.ToString());
        Assert.Equal(definition, JsonSerializer.Deserialize<WorldSessionDefinition>(JsonSerializer.Serialize(definition, JsonDefaults.Compact), JsonDefaults.Compact));
    }

    [Fact]
    public void V1AndV2SchemasAreExactAndVersionSelected()
    {
        WorldSessionDefinition v2 = Definition();
        JsonObject root = JsonNode.Parse(JsonSerializer.Serialize(v2, JsonDefaults.Compact))!.AsObject();
        root.Remove("commandProcessorCatalog");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionDefinition>(root.ToJsonString(), JsonDefaults.Compact));

        root = JsonNode.Parse(JsonSerializer.Serialize(v2, JsonDefaults.Compact))!.AsObject();
        root["formatVersion"] = "1.0.0";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionDefinition>(root.ToJsonString(), JsonDefaults.Compact));
        root["formatVersion"] = "9.0.0";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionDefinition>(root.ToJsonString(), JsonDefaults.Compact));
    }

    [Fact]
    public void V2RejectsRuntimeAndCommandCatalogMismatch()
    {
        Assert.Throws<ArgumentException>(() => new WorldSessionDefinition(World(), Branch(), Key(), Registry(), Seed(), AlgorithmCatalog.Phase04, Graph(), Catalog()));
        string json = JsonSerializer.Serialize(Definition(), JsonDefaults.Compact)
            .Replace(Catalog().Digest.ToString(), new string('f', 64), StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionDefinition>(json, JsonDefaults.Compact));
    }

    [Fact]
    public void PausedFixtureSnapshotMatchesStateDigestSnapshotDigestAndPropertyOrder()
    {
        WorldSessionSnapshot snapshot = PausedSnapshot();
        Assert.Equal("9c309262449fa1590750b9c320e853306fa516925bc2e05da606ff8c8e86e6cc", snapshot.StateDigest.ToString());
        Assert.Equal("33427d66eb92322396cd632ad3971407441e1ca09a72e7136549624213655893", snapshot.Digest.ToString());
        string json = JsonSerializer.Serialize(snapshot, JsonDefaults.Compact);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(
            ["formatVersion", "definition", "currentTick", "status", "lastCommandSequence", "lastEventSequence", "pendingCommands", "faultIssues", "stateDigest", "digest"],
            document.RootElement.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(snapshot, JsonSerializer.Deserialize<WorldSessionSnapshot>(json, JsonDefaults.Compact));
    }

    [Fact]
    public void SnapshotRejectsReadyAndInvalidFaultCombinations()
    {
        WorldSessionDefinition definition = Definition();
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Ready, [], []));
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Paused, [], [Fault()]));
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Faulted, [], []));
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Faulted, [], [new(new("snapshot.warning"), IssueSeverity.Warning, "Warning", "No failure.")]));
    }

    [Fact]
    public void FaultedSnapshotPreservesIssueOrderAndIsImmutable()
    {
        FoundationIssue[] issues = [Fault("snapshot.first"), new(new("snapshot.second"), IssueSeverity.Critical, "Second", "Second detail")];
        WorldSessionSnapshot snapshot = Snapshot(Definition(), WorldSessionStatus.Faulted, [], issues);
        issues[0] = Fault("snapshot.changed");
        Assert.Equal(["snapshot.first", "snapshot.second"], snapshot.FaultIssues.Select(static issue => issue.Code.ToString()));
        Assert.Throws<NotSupportedException>(() => ((IList<FoundationIssue>)snapshot.FaultIssues).Clear());
    }

    [Fact]
    public void PendingCommandsCanonicalizeAndSourceMutationCannotAffectSnapshot()
    {
        AcceptedSessionCommand later = Command(2, 2, 4);
        AcceptedSessionCommand earlier = Command(1, 1, 3);
        AcceptedSessionCommand[] source = [later, earlier];
        WorldSessionSnapshot snapshot = Snapshot(Definition(), WorldSessionStatus.Paused, source, [] , lastCommand: 2);
        source[0] = Command(2, 2, 9);
        Assert.Equal([(UInt128)1, (UInt128)2], snapshot.PendingCommands.Select(static item => item.SequenceNumber.Value));
        Assert.Throws<NotSupportedException>(() => ((IList<AcceptedSessionCommand>)snapshot.PendingCommands).Clear());
    }

    [Fact]
    public void PendingCommandInvariantsFailClosed()
    {
        WorldSessionDefinition definition = Definition();
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Paused, [Command(1, 1, 3), Command(1, 1, 4)], [], lastCommand: 1));
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Paused, [Command(2, 1, 3)], [], lastCommand: 1));
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Paused, [Command(1, 3, 3)], [], lastCommand: 1));
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Paused, [Command(1, 1, 1)], [], lastCommand: 1));
        AcceptedSessionCommand unknown = new(new(1), new(1), new(3), new("foundation.unknown"), Payload("x"));
        Assert.Throws<ArgumentException>(() => Snapshot(definition, WorldSessionStatus.Paused, [unknown], [], lastCommand: 1));
    }

    [Fact]
    public void SnapshotJsonRejectsUnsortedUnknownDuplicateAndDigestMismatch()
    {
        WorldSessionSnapshot snapshot = Snapshot(Definition(), WorldSessionStatus.Paused, [Command(1, 1, 3), Command(2, 1, 4)], [], lastCommand: 2);
        JsonObject root = JsonNode.Parse(JsonSerializer.Serialize(snapshot, JsonDefaults.Compact))!.AsObject();
        JsonArray pending = root["pendingCommands"]!.AsArray();
        JsonNode first = pending[0]!.DeepClone();
        JsonNode second = pending[1]!.DeepClone();
        pending[0] = second;
        pending[1] = first;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionSnapshot>(root.ToJsonString(), JsonDefaults.Compact));
        string json = JsonSerializer.Serialize(PausedSnapshot(), JsonDefaults.Compact);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionSnapshot>(json.Replace("\"digest\":", "\"unknown\":0,\"digest\":", StringComparison.Ordinal), JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionSnapshot>(json.Replace("\"digest\":", "\"digest\":\"" + new string('f', 64) + "\",\"digest2\":", StringComparison.Ordinal), JsonDefaults.Compact));
    }

    [Fact]
    public void SnapshotRejectsStateDigestMismatchAndMalformedUnicode()
    {
        WorldSessionDefinition definition = Definition();
        Assert.Throws<ArgumentException>(() => new WorldSessionSnapshot(definition, new(2), WorldSessionStatus.Paused, new(4), new(10), [], [], default));
        string malformed = new(['\ud800']);
        FoundationIssue issue = new(new("snapshot.unicode"), IssueSeverity.Critical, malformed, "detail");
        Assert.Throws<EncoderFallbackException>(() => Snapshot(definition, WorldSessionStatus.Faulted, [], [issue]));
    }

    private static WorldSessionSnapshot PausedSnapshot() => Snapshot(Definition(), WorldSessionStatus.Paused, [], []);
    private static WorldSessionSnapshot Snapshot(WorldSessionDefinition definition, WorldSessionStatus status, IEnumerable<AcceptedSessionCommand> pending, IEnumerable<FoundationIssue> faults, ulong lastCommand = 4)
    {
        AcceptedSessionCommand[] commands = pending.ToArray();
        FoundationIssue[] issues = faults.ToArray();
        Sha256Digest state = WorldSessionStateFingerprint.Compute(definition, new(2), status, new(lastCommand), new(10), commands, issues);
        return new(definition, new(2), status, new(lastCommand), new(10), commands, issues, state);
    }
    private static AcceptedSessionCommand Command(ulong sequence, ulong accepted, ulong execute) =>
        new(new(sequence), new(accepted), new(execute), new("foundation.trace"), Payload("command"));
    private static FoundationIssue Fault(string code = "snapshot.fault") => new(new(code), IssueSeverity.Error, "Fault", "Fault detail");
    private static CommandProcessorCatalog Catalog() => new([new("foundation.trace")]);
    private static WorldSessionDefinition Definition() => new(World(), Branch(), Key(), Registry(), Seed(), AlgorithmCatalog.Phase05, Graph(), Catalog());
    private static WorldIdentity World() => new(WorldId.FromUInt64(42));
    private static BranchIdentity Branch() => new(WorldId.FromUInt64(42), BranchId.FromUInt64(7));
    private static RulesetKey Key() => new(RulesetId.FromUInt64(1), new(1, 0, 0));
    private static RulesetRegistry Registry() => new([FoundationReferenceRuleset.Create()]);
    private static RngSeed256 Seed() => RngSeed256.Parse("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
    private static SchedulerGraph Graph() => new([
        new(new("foundation.trace.command"), SimulationPhase.Commands, []),
        new(new("foundation.trace.prepare-a"), SimulationPhase.Prepare, []),
        new(new("foundation.trace.prepare-b"), SimulationPhase.Prepare, [new("foundation.trace.prepare-a")]),
        new(new("foundation.trace.evaluate"), SimulationPhase.Evaluate, [])]);
    private static ImmutableConfiguration Payload(string value) => new(new("foundation.test"), new(1, 0, 0), [new(new("foundation.value"), ConfigurationValue.FromString(value))]);
}
