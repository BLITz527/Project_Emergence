using System.Reflection;
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

public sealed class ModelContractTests
{
    public static TheoryData<string, Type> LexicalCases => new()
    {
        { "foundation.trace.system-a", typeof(SimulationSystemId) },
        { "foundation.trace", typeof(SessionCommandTypeId) },
        { "foundation.system-trace", typeof(WorldEventTypeId) },
    };

    [Theory, MemberData(nameof(LexicalCases))]
    public void LexicalSessionTypesValidateCompareAndRoundTrip(string text, Type type)
    {
        object value = Activator.CreateInstance(type, text)!;
        string json = JsonSerializer.Serialize(value, type, JsonDefaults.Compact);
        object copy = JsonSerializer.Deserialize(json, type, JsonDefaults.Compact)!;
        Assert.Equal(value, copy);
        Assert.Equal($"\"{text}\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Foundation.trace")]
    [InlineData("foundation..trace")]
    [InlineData("foundation.trace ")]
    [InlineData("foundation._trace")]
    public void LexicalSessionTypesRejectMalformedValues(string text)
    {
        Assert.Throws<ArgumentException>(() => new SimulationSystemId(text));
        Assert.Throws<ArgumentException>(() => new SessionCommandTypeId(text));
        Assert.Throws<ArgumentException>(() => new WorldEventTypeId(text));
    }

    [Fact]
    public void DefaultLexicalTypesCannotWriteJson()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(SimulationSystemId), JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(SessionCommandTypeId), JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(WorldEventTypeId), JsonDefaults.Compact));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Foundation.trace")]
    [InlineData("foundation.trace ")]
    [InlineData("foundation..trace")]
    public void LexicalSessionTypesRejectMalformedJsonStrings(string value)
    {
        string json = JsonSerializer.Serialize(value);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SimulationSystemId>(json, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SessionCommandTypeId>(json, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldEventTypeId>(json, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SimulationSystemId>("1", JsonDefaults.Compact));
    }

    [Fact]
    public void SimulationPhaseOrderIsExactAndStrictJson()
    {
        Assert.Equal(new[] { "Commands", "Prepare", "Evaluate", "Resolve", "Commit", "Finalize" }, Enum.GetValues<SimulationPhase>().Select(static item => item.ToString()));
        Assert.Equal("\"Commands\"", JsonSerializer.Serialize(SimulationPhase.Commands, JsonDefaults.Compact));
        foreach (string invalid in new[] { "0", "\"commands\"", "\" Commands\"", "\"Unknown\"" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SimulationPhase>(invalid, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((SimulationPhase)99, JsonDefaults.Compact));
    }

    [Fact]
    public void WorldSessionStatusUsesExactStringJson()
    {
        Assert.Equal("\"Paused\"", JsonSerializer.Serialize(WorldSessionStatus.Paused, JsonDefaults.Compact));
        foreach (string invalid in new[] { "0", "\"paused\"", "\"Paused \"", "\"Unknown\"" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionStatus>(invalid, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((WorldSessionStatus)9, JsonDefaults.Compact));
    }

    [Fact]
    public void SystemDescriptorDefensivelyCopiesAndSortsDependencies()
    {
        SimulationSystemId[] source = [new("foundation.trace.z"), new("foundation.trace.a")];
        SimulationSystemDescriptor descriptor = new(new("foundation.trace.target"), SimulationPhase.Prepare, source);
        source[0] = new("foundation.changed");
        Assert.Equal(new[] { "foundation.trace.a", "foundation.trace.z" }, descriptor.RunsAfter.Select(static item => item.ToString()));
        Assert.Throws<NotSupportedException>(() => ((IList<SimulationSystemId>)descriptor.RunsAfter).Add(new("foundation.x")));
    }

    [Fact]
    public void SystemDescriptorRejectsInvalidDuplicateAndSelfDependencies()
    {
        Assert.Throws<ArgumentException>(() => new SimulationSystemDescriptor(default, SimulationPhase.Prepare, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationSystemDescriptor(new("foundation.trace.a"), (SimulationPhase)99, []));
        Assert.Throws<ArgumentException>(() => new SimulationSystemDescriptor(new("foundation.trace.a"), SimulationPhase.Prepare, [default]));
        Assert.Throws<ArgumentException>(() => new SimulationSystemDescriptor(new("foundation.trace.a"), SimulationPhase.Prepare, [new("foundation.trace.a")]));
        Assert.Throws<ArgumentException>(() => new SimulationSystemDescriptor(new("foundation.trace.a"), SimulationPhase.Prepare, [new("foundation.trace.b"), new("foundation.trace.b")]));
    }

    [Fact]
    public void SessionDefinitionMatchesGoldenDigestAndJsonRoundTrips()
    {
        WorldSessionDefinition definition = FixtureDefinition();
        Assert.Equal("fcc91152d376a93f558f44c2e76eb8493ab61fb519d598faa8782992d8cd3456", definition.Digest.ToString());
        string json = JsonSerializer.Serialize(definition, JsonDefaults.Indented);
        WorldSessionDefinition copy = JsonSerializer.Deserialize<WorldSessionDefinition>(json, JsonDefaults.Indented)!;
        Assert.Equal(definition, copy);
        string tampered = json.Replace(definition.Digest.ToString(), new string('f', 64), StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionDefinition>(tampered, JsonDefaults.Indented));
    }

    [Theory]
    [InlineData("rulesetDescriptorDigest")]
    [InlineData("rulesetRegistryDigest")]
    [InlineData("schedulerGraphDigest")]
    public void SessionDefinitionRejectsContradictoryRedundantDigest(string property)
    {
        string json = JsonSerializer.Serialize(FixtureDefinition(), JsonDefaults.Indented);
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        root[property] = new string('0', 64);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionDefinition>(root.ToJsonString(), JsonDefaults.Indented));
    }

    [Fact]
    public void SessionDefinitionRejectsWorldBranchRulesetAndAlgorithmMismatch()
    {
        RulesetRegistry registry = Registry();
        SchedulerGraph graph = Graph();
        Assert.Throws<ArgumentException>(() => new WorldSessionDefinition(new(WorldId.FromUInt64(42)), new(WorldId.FromUInt64(41), BranchId.FromUInt64(7)), Key(), registry, Seed(), AlgorithmCatalog.Phase04, graph));
        Assert.Throws<ArgumentException>(() => new WorldSessionDefinition(new(WorldId.FromUInt64(42)), new(WorldId.FromUInt64(42), BranchId.FromUInt64(7)), new(RulesetId.FromUInt64(2), new(1, 0, 0)), registry, Seed(), AlgorithmCatalog.Phase04, graph));
        Assert.Throws<ArgumentException>(() => new WorldSessionDefinition(new(WorldId.FromUInt64(42)), new(WorldId.FromUInt64(42), BranchId.FromUInt64(7)), Key(), registry, Seed(), AlgorithmCatalog.Phase03, graph));
    }

    [Fact]
    public void CommandAndEventContractsValidateAndAreImmutable()
    {
        ImmutableConfiguration payload = Payload("x");
        Assert.Throws<ArgumentException>(() => new SessionCommandRequest(default, default, payload));
        Assert.Throws<ArgumentNullException>(() => new SessionCommandRequest(default, new("foundation.trace"), null!));
        AcceptedSessionCommand accepted = new(new(1), default, new(1), new("foundation.trace"), payload);
        Assert.Same(payload, accepted.Payload);
        Assert.Throws<ArgumentException>(() => new AcceptedSessionCommand(default, default, default, new("foundation.trace"), payload));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventProposal((SimulationPhase)99, new("foundation.trace.a"), 0, new("foundation.event"), payload));
        Assert.Throws<ArgumentException>(() => new WorldEventProposal(SimulationPhase.Prepare, default, 0, new("foundation.event"), payload));
        Assert.Throws<ArgumentException>(() => new WorldEventProposal(SimulationPhase.Prepare, new("foundation.trace.a"), 0, default, payload));
        Assert.Throws<ArgumentNullException>(() => new WorldEventProposal(SimulationPhase.Prepare, new("foundation.trace.a"), 0, new("foundation.event"), null!));
        Assert.Throws<ArgumentException>(() => new WorldEventProposal(SimulationPhase.Prepare, new("foundation.trace.a"), 0, new("foundation.event"), payload, default(SequenceNumber)));
        Assert.All(typeof(AcceptedSessionCommand).GetProperties(), static property => Assert.False(property.SetMethod?.IsPublic == true));
    }

    [Fact]
    public void CommittedEventCannotBePubliclyForged()
    {
        ConstructorInfo[] publicConstructors = typeof(CommittedWorldEvent).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(publicConstructors);
    }

    [Fact]
    public void TickReceiptCollectionsAreDefensive()
    {
        List<AcceptedSessionCommand> commands = [new(new(1), default, default, new("foundation.trace"), Payload("x"))];
        Sha256Digest definitionDigest = FixtureDefinition().Digest;
        TickExecutionReceipt receipt = TickExecutionReceipt.Succeeded(definitionDigest, default, new(1), commands, [new("foundation.trace.command")], [], default, []);
        commands.Clear();
        Assert.Single(receipt.CommandsConsumed);
        Assert.Equal(definitionDigest, receipt.SessionDefinitionDigest);
        Assert.Throws<NotSupportedException>(() => ((IList<AcceptedSessionCommand>)receipt.CommandsConsumed).Clear());
    }

    [Fact]
    public void TickReceiptDefinitionIdentityIsImmutableAndJsonStable()
    {
        Sha256Digest definitionDigest = FixtureDefinition().Digest;
        TickExecutionReceipt receipt = TickExecutionReceipt.Succeeded(definitionDigest, default, new(1), [], [], [], default, []);

        Assert.Null(typeof(TickExecutionReceipt).GetProperty(nameof(TickExecutionReceipt.SessionDefinitionDigest))!.SetMethod);
        using JsonDocument document = JsonDocument.Parse(JsonDefaults.Serialize(receipt, false));
        Assert.Equal(
            ["success", "sessionDefinitionDigest", "executedTick", "resultingTick", "commandsConsumed", "systemsExecuted", "committedEvents", "resultingStateDigest", "issues"],
            document.RootElement.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(definitionDigest.ToString(), document.RootElement.GetProperty("sessionDefinitionDigest").GetString());
    }

    [Fact]
    public void TickReceiptIssuesAreDefensivelyCopied()
    {
        FoundationIssue[] source = [new(new("receipt.warning"), IssueSeverity.Warning, "Warning", "Synthetic receipt warning.")];
        TickExecutionReceipt receipt = TickExecutionReceipt.Succeeded(FixtureDefinition().Digest, default, new(1), [], [], [], default, source);
        source[0] = new(new("receipt.changed"), IssueSeverity.Information, "Changed", "Changed source array.");

        Assert.Equal("receipt.warning", receipt.Issues.Single().Code.ToString());
        Assert.Throws<NotSupportedException>(() => ((IList<FoundationIssue>)receipt.Issues).Clear());
    }

    [Fact]
    public void Phase04CatalogExtendsWithoutChangingPhase03()
    {
        Assert.Equal("77ebbb568d4c72fcb1cdc7ace7dbc29b3d9e38f5e65e4a44f4a7d8eb9e050b20", AlgorithmCatalog.Phase03.Digest.ToString());
        Assert.Equal("bbaebfc88087fc04ab024d2505b9a50ed7e7a2f21cd34a18eb4e83d56cb1a418", AlgorithmCatalog.Phase04.Digest.ToString());
        Assert.Equal(AlgorithmCatalog.Phase03.Entries, AlgorithmCatalog.Phase04.Entries.Take(AlgorithmCatalog.Phase03.Entries.Count));
    }

    private static WorldSessionDefinition FixtureDefinition() => new(new(WorldId.FromUInt64(42)), new(WorldId.FromUInt64(42), BranchId.FromUInt64(7)), Key(), Registry(), Seed(), AlgorithmCatalog.Phase04, Graph());
    private static RulesetRegistry Registry() => new([FoundationReferenceRuleset.Create()]);
    private static RulesetKey Key() => new(RulesetId.FromUInt64(1), new(1, 0, 0));
    private static RngSeed256 Seed() => RngSeed256.Parse("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
    private static SchedulerGraph Graph() => new([new(new("foundation.trace.command"), SimulationPhase.Commands, []), new(new("foundation.trace.prepare-a"), SimulationPhase.Prepare, []), new(new("foundation.trace.prepare-b"), SimulationPhase.Prepare, [new("foundation.trace.prepare-a")]), new(new("foundation.trace.evaluate"), SimulationPhase.Evaluate, [])]);
    private static ImmutableConfiguration Payload(string value) => new(new("foundation.test"), new(1, 0, 0), [new(new("foundation.value"), ConfigurationValue.FromString(value))]);
}
