using System.Text.Json.Serialization;
using Emergence.Foundation;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Results;
using Emergence.Model;
using Emergence.Model.Environment;
using Emergence.Persistence.WorldPackages;
using Emergence.Simulation;
using Emergence.Simulation.Fields;

namespace Emergence.Cli;

public sealed record EnvironmentChannelTotal(
    [property: JsonPropertyOrder(0)] string ChannelId,
    [property: JsonPropertyOrder(1)] string Total);

public sealed record EnvironmentProbeVector(
    [property: JsonPropertyOrder(0)] uint X,
    [property: JsonPropertyOrder(1)] uint Y,
    [property: JsonPropertyOrder(2)] bool Solid,
    [property: JsonPropertyOrder(3)] string EffectiveVolume,
    [property: JsonPropertyOrder(4)] string EnergySubstrate,
    [property: JsonPropertyOrder(5)] string StructuralPrecursor,
    [property: JsonPropertyOrder(6)] string Waste,
    [property: JsonPropertyOrder(7)] bool ConcentrationAvailable);

public sealed record EnvironmentChunkVector(
    [property: JsonPropertyOrder(0)] string Path,
    [property: JsonPropertyOrder(1)] int UncompressedByteLength,
    [property: JsonPropertyOrder(2)] string Sha256);

public sealed record EnvironmentSelfTestReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] string FieldChannelCatalogDigest,
    [property: JsonPropertyOrder(2)] string RegionDefinitionDigest,
    [property: JsonPropertyOrder(3)] string RegionStateDigest,
    [property: JsonPropertyOrder(4)] string EnvironmentDefinitionDigest,
    [property: JsonPropertyOrder(5)] string EnvironmentStateDigest,
    [property: JsonPropertyOrder(6)] string AlgorithmCatalogDigest,
    [property: JsonPropertyOrder(7)] string SessionDefinitionDigest,
    [property: JsonPropertyOrder(8)] string SessionStateDigest,
    [property: JsonPropertyOrder(9)] string SnapshotDigest,
    [property: JsonPropertyOrder(10)] string PackageIdentityDigest,
    [property: JsonPropertyOrder(11)] string ManifestDigest,
    [property: JsonPropertyOrder(12)] int SolidCellCount,
    [property: JsonPropertyOrder(13)] int FluidCellCount,
    [property: JsonPropertyOrder(14)] IReadOnlyList<EnvironmentChannelTotal> ChannelTotals,
    [property: JsonPropertyOrder(15)] IReadOnlyList<EnvironmentProbeVector> Probes,
    [property: JsonPropertyOrder(16)] IReadOnlyList<EnvironmentChunkVector> ChunkEntries,
    [property: JsonPropertyOrder(17)] bool SaveLoadMatched,
    [property: JsonPropertyOrder(18)] bool OneTickEnvironmentUnchanged,
    [property: JsonPropertyOrder(19)] IReadOnlyList<DiagnosticCheck> Checks);

public static class EnvironmentSelfTest
{
    public const string ExpectedPackageIdentityDigest = "a05a5eb93c9a098dc446f1315a75da1d31b118b2fbe12f203ea6a37476e1f685";
    public const string ExpectedManifestDigest = "b8516ab7ddfbe889c2a8f38c3acb3f0b84a3d60922e85b29d3bc2199bb8bcdee";

    public static EnvironmentSelfTestReport Run()
    {
        WorldSession original = EnvironmentSessionFixture.CreatePausedSession();
        WorldEnvironmentState environment = original.EnvironmentState!;
        RegionFieldState region = environment.Regions.Single();
        WorldSessionSnapshot snapshot = original.CaptureSnapshot().Value;
        WorldEnvironmentStore store = new(environment);
        EnvironmentConservationAuditReport audit = new EnvironmentConservationAudit().Run(store);

        EnvironmentChannelTotal[] totals = region.Definition.FieldChannels.Definitions.Select((channel, slot) =>
            new EnvironmentChannelTotal(channel.Id.ToString(), region.GetChannelTotal(slot).ToString())).ToArray();
        EnvironmentProbeVector[] probes =
        [
            Probe(store, 1, 1),
            Probe(store, 8, 5),
            Probe(store, 8, 6),
            Probe(store, 14, 10),
        ];
        EnvironmentChunkVector[] chunks = CreateChunkVectors(region);

        bool saveLoadMatched = false;
        bool oneTickUnchanged = false;
        string packageIdentity = string.Empty;
        string manifestDigest = string.Empty;
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "ProjectEmergence", "environment-self-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string packagePath = Path.Combine(temporaryRoot, "reference.emergence-world");
            WorldPackageSaveResult save = new WorldPackageWriter().Save(packagePath, snapshot);
            packageIdentity = save.PackageIdentityDigest;
            manifestDigest = save.ManifestDigest;
            WorldPackageLoadResult load = save.Success ? new WorldPackageReader().Load(packagePath) : new(false, null, save.Issues);
            OperationResult<WorldSession> restoration = load.Success && load.Document is not null
                ? WorldSession.Restore(load.Document.Snapshot, FoundationSessionFixture.CreateSystems(), FoundationSessionFixture.CreateCommandProcessorRegistry())
                : OperationResult<WorldSession>.Failed(new FoundationIssue(new("environment.self-test-load"), IssueSeverity.Error, "Environment load failed", "The V2 fixture did not load."));
            saveLoadMatched = restoration.Success
                && load.Document!.Snapshot.Equals(snapshot)
                && restoration.Value.EnvironmentState!.Equals(environment)
                && restoration.Value.StateDigest == snapshot.StateDigest;
            if (saveLoadMatched)
            {
                WorldSession restored = restoration.Value;
                Sha256Digest beforeOriginal = original.EnvironmentState!.Digest;
                Sha256Digest beforeRestored = restored.EnvironmentState!.Digest;
                bool resumed = original.Resume().Success && restored.Resume().Success;
                if (resumed)
                {
                    TickExecutionReceipt first = original.StepOneTick();
                    TickExecutionReceipt second = restored.StepOneTick();
                    bool paused = original.Pause().Success && restored.Pause().Success;
                    oneTickUnchanged = first.Success && second.Success && paused
                        && original.EnvironmentState!.Digest == beforeOriginal
                        && restored.EnvironmentState!.Digest == beforeRestored
                        && original.EnvironmentState.Equals(restored.EnvironmentState)
                        && original.StateDigest == restored.StateDigest;
                }
            }
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }

        List<DiagnosticCheck> checks =
        [
            Check("environment.catalog", region.Definition.FieldChannels.Digest.ToString() == ReferenceEnvironmentDefinition.ExpectedFieldChannelCatalogDigest, "Field channel catalog", region.Definition.FieldChannels.Digest.ToString()),
            Check("environment.region-definition", region.Definition.Digest.ToString() == ReferenceEnvironmentDefinition.ExpectedRegionDefinitionDigest, "Region definition", region.Definition.Digest.ToString()),
            Check("environment.region-state", region.Digest.ToString() == ReferenceEnvironmentFixture.ExpectedRegionStateDigest, "Region state", region.Digest.ToString()),
            Check("environment.definition", environment.Definition.Digest.ToString() == ReferenceEnvironmentDefinition.ExpectedEnvironmentDefinitionDigest, "Environment definition", environment.Definition.Digest.ToString()),
            Check("environment.state", environment.Digest.ToString() == ReferenceEnvironmentFixture.ExpectedEnvironmentStateDigest, "Environment state", environment.Digest.ToString()),
            Check("environment.algorithms", original.Definition.RuntimeAlgorithms.Digest.ToString() == EnvironmentSessionFixture.ExpectedAlgorithmCatalogDigest, "Phase 1.1 algorithms", original.Definition.RuntimeAlgorithms.Digest.ToString()),
            Check("environment.session-definition", original.Definition.Digest.ToString() == EnvironmentSessionFixture.ExpectedDefinitionDigest, "V3 session definition", original.Definition.Digest.ToString()),
            Check("environment.session-state", snapshot.StateDigest.ToString() == EnvironmentSessionFixture.ExpectedStateDigest, "Session state", snapshot.StateDigest.ToString()),
            Check("environment.snapshot", snapshot.Digest.ToString() == EnvironmentSessionFixture.ExpectedSnapshotDigest, "V2 snapshot", snapshot.Digest.ToString()),
            Check("environment.package-identity", packageIdentity == ExpectedPackageIdentityDigest, "V2 package identity", packageIdentity),
            Check("environment.manifest", manifestDigest == ExpectedManifestDigest, "V2 manifest", manifestDigest),
            Check("environment.conservation", audit.Success && audit.Channels.All(static channel => channel.SolidCellViolationCount == 0), "Exact conservation audit", "No solid-cell matter and exact channel totals."),
            Check("environment.save-load", saveLoadMatched, "V2 save/load restoration", saveLoadMatched.ToString()),
            Check("environment.static-tick", oneTickUnchanged, "Static environment tick", oneTickUnchanged.ToString()),
        ];
        bool success = checks.All(static check => check.Severity == DiagnosticSeverity.Success);
        return new(
            success,
            region.Definition.FieldChannels.Digest.ToString(),
            region.Definition.Digest.ToString(),
            region.Digest.ToString(),
            environment.Definition.Digest.ToString(),
            environment.Digest.ToString(),
            original.Definition.RuntimeAlgorithms.Digest.ToString(),
            snapshot.Definition.Digest.ToString(),
            snapshot.StateDigest.ToString(),
            snapshot.Digest.ToString(),
            packageIdentity,
            manifestDigest,
            region.Definition.SolidCellCount,
            region.Definition.FluidCellCount,
            Array.AsReadOnly(totals),
            Array.AsReadOnly(probes),
            Array.AsReadOnly(chunks),
            saveLoadMatched,
            oneTickUnchanged,
            Array.AsReadOnly(checks.ToArray()));
    }

    private static EnvironmentProbeVector Probe(WorldEnvironmentStore store, uint x, uint y)
    {
        FieldProbeService service = new();
        LatticeCoordinate coordinate = new(x, y);
        FieldProbeResult energy = service.Probe(store, ReferenceEnvironmentDefinition.RegionId, coordinate, new(ReferenceEnvironmentDefinition.EnergySubstrateId));
        FieldProbeResult structural = service.Probe(store, ReferenceEnvironmentDefinition.RegionId, coordinate, new(ReferenceEnvironmentDefinition.StructuralPrecursorId));
        FieldProbeResult waste = service.Probe(store, ReferenceEnvironmentDefinition.RegionId, coordinate, new(ReferenceEnvironmentDefinition.WasteId));
        if (!energy.Success || !structural.Success || !waste.Success) throw new InvalidOperationException("Reference environment probe failed.");
        return new(x, y, energy.IsSolid, energy.EffectiveVolume.ToString(), energy.Amount.ToString(), structural.Amount.ToString(), waste.Amount.ToString(), energy.Concentration.HasValue);
    }

    private static EnvironmentChunkVector[] CreateChunkVectors(RegionFieldState region)
    {
        List<EnvironmentChunkVector> chunks = [];
        for (uint y = 0; y < region.Definition.ChunkRows; y++)
        for (uint x = 0; x < region.Definition.ChunkColumns; x++)
        {
            FieldChunkCoordinate coordinate = new(x, y);
            byte[] bytes = FieldChunkCodec.Encode(region, coordinate);
            chunks.Add(new(FieldChunkCodec.GetPath(region.Definition, coordinate), bytes.Length, Sha256Digest.Compute(bytes).ToString()));
        }
        return chunks.ToArray();
    }

    private static DiagnosticCheck Check(string id, bool success, string summary, string detail) =>
        new(id, success ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, summary, detail);
}
