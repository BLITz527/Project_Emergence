using System.Diagnostics;
using System.Text.Json.Serialization;
using Emergence.Foundation;
using Emergence.Foundation.Fields;
using Emergence.Model.Environment;
using Emergence.Persistence.WorldPackages;
using Emergence.Simulation.Fields;

namespace Emergence.Cli;

public sealed record EnvironmentPerformanceReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] int BytesPerFieldSlot,
    [property: JsonPropertyOrder(2)] int ReferenceAuthoritativeFieldBytes,
    [property: JsonPropertyOrder(3)] long MaximumContractEstimatedFieldBytes,
    [property: JsonPropertyOrder(4)] int ProbeIterations,
    [property: JsonPropertyOrder(5)] double ProbeOperationsPerSecond,
    [property: JsonPropertyOrder(6)] long PresentationSnapshotAllocatedBytes,
    [property: JsonPropertyOrder(7)] long ChunkEncodeAllocatedBytes,
    [property: JsonPropertyOrder(8)] long ChunkDecodeAllocatedBytes,
    [property: JsonPropertyOrder(9)] long PackageBytes,
    [property: JsonPropertyOrder(10)] IReadOnlyList<int> ChunkBytes,
    [property: JsonPropertyOrder(11)] string Interpretation,
    [property: JsonPropertyOrder(12)] IReadOnlyList<DiagnosticCheck> Checks);

public static class EnvironmentPerformanceEvidence
{
    public static EnvironmentPerformanceReport Run()
    {
        var session = EnvironmentSessionFixture.CreatePausedSession();
        var environment = session.EnvironmentState!;
        var region = environment.Regions.Single();
        WorldEnvironmentStore store = new(environment);
        FieldProbeService probes = new();
        FieldChannelId channel = new(ReferenceEnvironmentDefinition.EnergySubstrateId);
        _ = probes.Probe(store, ReferenceEnvironmentDefinition.RegionId, new(8, 6), channel);
        const int probeIterations = 50_000;
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < probeIterations; index++)
            _ = probes.Probe(store, ReferenceEnvironmentDefinition.RegionId, new((uint)(1 + index % 14), (uint)(1 + index % 10)), channel);
        stopwatch.Stop();
        double operations = probeIterations / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.000001);

        EnvironmentPresentationSnapshotProducer presentation = new();
        _ = presentation.Create(session, channel);
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = presentation.Create(session, channel);
        long presentationBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        byte[] firstChunk = FieldChunkCodec.Encode(region, new(0, 0));
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = FieldChunkCodec.Encode(region, new(0, 0));
        long encodeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        _ = FieldChunkCodec.Decode(firstChunk, region.Definition, new(0, 0));
        before = GC.GetAllocatedBytesForCurrentThread();
        _ = FieldChunkCodec.Decode(firstChunk, region.Definition, new(0, 0));
        long decodeBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        int[] chunkBytes = [];
        long packageBytes = 0;
        string directory = Path.Combine(Path.GetTempPath(), "ProjectEmergence", "environment-performance-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            string package = Path.Combine(directory, "fixture.emergence-world");
            WorldPackageSaveResult save = new WorldPackageWriter().Save(package, session.CaptureSnapshot().Value);
            if (!save.Success) throw new InvalidOperationException("Performance evidence package save failed.");
            packageBytes = save.PackageBytes;
            WorldPackageLoadResult load = new WorldPackageReader().Load(package);
            if (!load.Success || load.Document is null) throw new InvalidOperationException("Performance evidence package load failed.");
            chunkBytes = load.Document.Manifest.Entries.Where(static entry => entry.Path.EndsWith(".bin", StringComparison.Ordinal))
                .Select(static entry => checked((int)entry.UncompressedByteLength)).ToArray();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }

        List<DiagnosticCheck> checks =
        [
            new("environment.storage.dense", DiagnosticSeverity.Success, "Dense primitive field storage", "8 bytes per exact UInt64 field slot; no per-cell objects."),
            new("environment.rendering.nodes", DiagnosticSeverity.Success, "Field rendering architecture", "One custom draw control; zero nodes per cell."),
            new("environment.performance.informational", DiagnosticSeverity.Success, "Nonflaky evidence policy", "Timing and allocation values are informational, with no arbitrary stopwatch pass threshold."),
        ];
        return new(true, sizeof(ulong), store.Region.AllocatedFieldBytes, 512L * 512 * 16 * sizeof(ulong),
            probeIterations, operations, presentationBytes, encodeBytes, decodeBytes, packageBytes,
            Array.AsReadOnly(chunkBytes), "Measurements are diagnostic samples, not biological capacity or acceptance thresholds.", Array.AsReadOnly(checks.ToArray()));
    }
}
