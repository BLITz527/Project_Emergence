using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Model.Environment;

namespace Emergence.Simulation.Fields;

public static class EnvironmentSessionFixture
{
    public const string ExpectedAlgorithmCatalogDigest = "b6339de0044a28aa9af9d1f3dde6d29a70e53742f678e2ee08586250cf431c65";
    public const string ExpectedDefinitionDigest = "3b3cc11fd0c728ee2d18f2f59406ec3b144c258423bdaae719634d735dd048ac";
    public const string ExpectedStateDigest = "ed67529eb33daa70db0ff52ff5d50071aae193222c6c98f26f73839286c827bc";
    public const string ExpectedSnapshotDigest = "710653573b0f996970ea3cd5e9b5632dd822bbae4946702b3624bd84b9c18543";

    public static WorldSessionDefinition CreateDefinition(RulesetRegistry? registry = null)
    {
        registry ??= new([FoundationReferenceRuleset.Create()]);
        CommandProcessorRegistry processors = FoundationSessionFixture.CreateCommandProcessorRegistry();
        return new(
            new WorldIdentity(WorldId.FromUInt64(42)),
            new BranchIdentity(WorldId.FromUInt64(42), BranchId.FromUInt64(7)),
            new RulesetKey(RulesetId.FromUInt64(1), new(1, 0, 0)),
            registry,
            RngSeed256.Parse(FoundationSessionFixture.Seed),
            AlgorithmCatalog.Phase11,
            FoundationSessionFixture.CreateGraph(),
            processors.Catalog,
            ReferenceEnvironmentDefinition.Create());
    }

    public static WorldSession CreatePausedSession(RulesetRegistry? registry = null)
    {
        registry ??= new([FoundationReferenceRuleset.Create()]);
        return new(
            CreateDefinition(registry),
            FoundationSessionFixture.CreateSystems(),
            FoundationSessionFixture.CreateCommandProcessorRegistry(),
            ReferenceEnvironmentFixture.CreateStore());
    }

    public static WorldSessionSnapshot CreateSnapshot()
    {
        var capture = CreatePausedSession().CaptureSnapshot();
        return capture.Success ? capture.Value : throw new InvalidOperationException("Reference environment snapshot capture failed.");
    }
}
