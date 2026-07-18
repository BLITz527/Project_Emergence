using System.Text.Json.Serialization;
using Emergence.Foundation;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Results;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Persistence.WorldPackages;
using Emergence.Simulation;

namespace Emergence.Cli;

public sealed record PersistenceSelfTestReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] string AlgorithmCatalogDigest,
    [property: JsonPropertyOrder(2)] string CommandProcessorCatalogDigest,
    [property: JsonPropertyOrder(3)] string DefinitionDigest,
    [property: JsonPropertyOrder(4)] string PreSaveStateDigest,
    [property: JsonPropertyOrder(5)] string SnapshotDigest,
    [property: JsonPropertyOrder(6)] string PackageIdentityDigest,
    [property: JsonPropertyOrder(7)] string ManifestDigest,
    [property: JsonPropertyOrder(8)] long PackageBytes,
    [property: JsonPropertyOrder(9)] string LoadedStateDigest,
    [property: JsonPropertyOrder(10)] bool RngContinuationMatched,
    [property: JsonPropertyOrder(11)] SequenceNumber NextCommandSequence,
    [property: JsonPropertyOrder(12)] IReadOnlyList<string> ContinuationEventIds,
    [property: JsonPropertyOrder(13)] string FinalStateDigest,
    [property: JsonPropertyOrder(14)] string PersistenceTraceDigest,
    [property: JsonPropertyOrder(15)] IReadOnlyList<DiagnosticCheck> RecoveryChecks,
    [property: JsonPropertyOrder(16)] IReadOnlyList<DiagnosticCheck> Checks);

public static class PersistenceSelfTest
{
    public const string ExpectedAlgorithmCatalogDigest = "78818c4c6a6a4aeb498a634e4cd77e5854c3fa35be2d075aabb888cb0fe7d9a1";
    public const string ExpectedCommandProcessorCatalogDigest = "e2555f63b5b4c9644229336da1856f35c8dabf3cf54765e224d3c51e19a3d8f6";
    public const string ExpectedDefinitionDigest = "ca024a17b1e0ee02b57d639bea1f57d0f04154e6c3da501fd24af0ebe9798e0e";
    public const string ExpectedPreSaveStateDigest = "9c309262449fa1590750b9c320e853306fa516925bc2e05da606ff8c8e86e6cc";
    public const string ExpectedSnapshotDigest = "33427d66eb92322396cd632ad3971407441e1ca09a72e7136549624213655893";
    public const string ExpectedPackageIdentityDigest = "fcfab8b4e95de5f578330eb0d599e8759ebb62ca6fc37210f36197a88927c3d1";
    public const string ExpectedFinalStateDigest = "fb303204175f2ed6186755e9d8ff8877bcc60892554e4765f52a4224f9f706dd";
    public const string ExpectedPersistenceTraceDigest = "b527e3355bc94f2eef586214f7ecf841b968c380b7427250c7fa06216aae8d0e";
    public static IReadOnlyList<string> ExpectedContinuationEventIds { get; } = Array.AsReadOnly(new[]
    {
        "8adf4015e21a6e9b4d67bf735ca95840",
        "eaf3454d0b583165c89d3d785a483e7b",
        "3ca4b0b1f20eab439cca3a7d874531ef",
        "521e2a0fa467efc0f2fac2601f1194f3",
    });

    public static PersistenceSelfTestReport Run()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "ProjectEmergence", "persistence-self-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            RulesetRegistry registry = new([FoundationReferenceRuleset.Create()]);
            CommandProcessorRegistry processors = FoundationSessionFixture.CreateCommandProcessorRegistry();
            WorldSession original = CreatePausedFixtureSession();

            string preSaveState = original.StateDigest.ToString();
            OperationResult<WorldSessionSnapshot> capture = original.CaptureSnapshot();
            Require(capture.Success, "Snapshot capture failed.");
            WorldSessionSnapshot snapshot = capture.Value;

            RngSampleAddress rngAddress = new(new("foundation.self-test"), RngScopeKey.Parse(FoundationRngSelfTest.Scope), 42);
            string rngBefore = new DeterministicAddressedRng(snapshot.Definition.RootSeed, snapshot.Definition.SelectedRuleset.RngDomains)
                .GenerateBlock(rngAddress).ToString();

            string packagePath = Path.Combine(temporaryRoot, "fixture.emergence-world");
            WorldPackageSaveResult save = new WorldPackageWriter().Save(packagePath, snapshot);
            Require(save.Success, save.Issues.Count == 0 ? "Package save failed." : save.Issues[0].Detail);
            WorldPackageLoadResult load = new WorldPackageReader().Load(packagePath);
            Require(load.Success && load.Document is not null, load.Issues.Count == 0 ? "Package load failed." : load.Issues[0].Detail);
            WorldPackageDocument document = load.Document!;

            OperationResult compatibility = SessionCompatibilityValidator.Validate(document.Snapshot, FoundationSessionFixture.CreateSystems(), processors);
            Require(compatibility.Success, "Loaded package is incompatible with the fixture runtime.");
            OperationResult<WorldSession> restoration = WorldSession.Restore(document.Snapshot, FoundationSessionFixture.CreateSystems(), processors);
            Require(restoration.Success, "Loaded session restoration failed.");
            WorldSession restored = restoration.Value;
            string loadedState = restored.StateDigest.ToString();
            string rngAfter = new DeterministicAddressedRng(restored.Definition.RootSeed, restored.Definition.SelectedRuleset.RngDomains)
                .GenerateBlock(rngAddress).ToString();
            bool rngMatched = rngBefore == rngAfter;

            AcceptedSessionCommand originalNext = Submit(original, 2, "epsilon");
            AcceptedSessionCommand restoredNext = Submit(restored, 2, "epsilon");
            Require(originalNext.SequenceNumber.Value == 5 && restoredNext.SequenceNumber.Value == 5, "Continuation command sequence is not five.");
            Require(original.Resume().Success && restored.Resume().Success, "Continuation sessions did not resume.");
            TickExecutionReceipt originalReceipt = original.StepOneTick();
            TickExecutionReceipt restoredReceipt = restored.StepOneTick();
            Require(originalReceipt.Success && restoredReceipt.Success, "Continuation tick failed.");
            Require(original.Pause().Success && restored.Pause().Success, "Continuation sessions did not pause.");
            Require(JsonDefaults.Serialize(originalReceipt, false) == JsonDefaults.Serialize(restoredReceipt, false), "Continuation receipts differ.");
            string[] continuationIds = originalReceipt.CommittedEvents.Select(static item => item.EventId.ToString()).ToArray();
            string finalState = original.StateDigest.ToString();
            Require(finalState == restored.StateDigest.ToString(), "Continuation final states differ.");

            IReadOnlyList<DiagnosticCheck> recoveryChecks = RunRecoveryChecks(packagePath, temporaryRoot);
            bool sidecarsClean = WorldPackageSidecarNames(packagePath).All(path => !File.Exists(path));
            string trace = ComputeTrace(
                snapshot,
                document.Manifest,
                loadedState,
                originalNext.SequenceNumber,
                continuationIds,
                finalState).ToString();

            List<DiagnosticCheck> checks =
            [
                Check("persistence.algorithm-catalog", AlgorithmCatalog.Phase05.Digest.ToString() == ExpectedAlgorithmCatalogDigest, "Phase 0.5 algorithm catalog", AlgorithmCatalog.Phase05.Digest.ToString()),
                Check("persistence.command-catalog", processors.Catalog.Digest.ToString() == ExpectedCommandProcessorCatalogDigest, "Command processor catalog", processors.Catalog.Digest.ToString()),
                Check("persistence.definition", snapshot.Definition.Digest.ToString() == ExpectedDefinitionDigest, "V2 session definition", snapshot.Definition.Digest.ToString()),
                Check("persistence.pre-save-state", preSaveState == ExpectedPreSaveStateDigest, "Pre-save state", preSaveState),
                Check("persistence.snapshot", snapshot.Digest.ToString() == ExpectedSnapshotDigest, "Session snapshot", snapshot.Digest.ToString()),
                Check("persistence.package-identity", document.Manifest.PackageIdentityDigest.ToString() == ExpectedPackageIdentityDigest, "World package identity", document.Manifest.PackageIdentityDigest.ToString()),
                Check("persistence.loaded-state", loadedState == preSaveState, "Loaded state", loadedState),
                Check("persistence.rng-continuation", rngMatched, "Addressed RNG continuation", rngAfter),
                Check("persistence.command-continuation", originalNext.SequenceNumber.Value == 5 && restoredNext.SequenceNumber.Value == 5, "Next command sequence", originalNext.SequenceNumber.ToString()),
                Check("persistence.event-continuation", continuationIds.SequenceEqual(ExpectedContinuationEventIds), "Continuation EventIds", string.Join(",", continuationIds)),
                Check("persistence.final-state", finalState == ExpectedFinalStateDigest, "Final continuation state", finalState),
                Check("persistence.trace", trace == ExpectedPersistenceTraceDigest, "Persistence trace", trace),
                Check("persistence.sidecars", sidecarsClean, "Successful save sidecars", sidecarsClean ? "none" : "unexpected sidecar"),
            ];
            bool success = checks.All(static check => check.Severity == DiagnosticSeverity.Success)
                && recoveryChecks.All(static check => check.Severity == DiagnosticSeverity.Success);
            return new(
                success,
                AlgorithmCatalog.Phase05.Digest.ToString(),
                processors.Catalog.Digest.ToString(),
                snapshot.Definition.Digest.ToString(),
                preSaveState,
                snapshot.Digest.ToString(),
                document.Manifest.PackageIdentityDigest.ToString(),
                document.Manifest.Digest.ToString(),
                save.PackageBytes,
                loadedState,
                rngMatched,
                originalNext.SequenceNumber,
                Array.AsReadOnly(continuationIds),
                finalState,
                trace,
                recoveryChecks,
                checks.AsReadOnly());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            DiagnosticCheck failure = Check("persistence.self-test-exception", false, "Persistence self-test failed", $"{exception.GetType().Name}: {exception.Message}");
            return new(false, AlgorithmCatalog.Phase05.Digest.ToString(), string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, 0, string.Empty, false, default, Array.Empty<string>(), string.Empty, string.Empty,
                Array.Empty<DiagnosticCheck>(), Array.AsReadOnly([failure]));
        }
        finally
        {
            try { if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    public static WorldSessionSnapshot CreateFixtureSnapshot()
    {
        OperationResult<WorldSessionSnapshot> capture = CreatePausedFixtureSession().CaptureSnapshot();
        return capture.Success ? capture.Value : throw new InvalidOperationException("Phase 0.5 fixture snapshot capture failed.");
    }

    private static WorldSession CreatePausedFixtureSession()
    {
        RulesetRegistry registry = new([FoundationReferenceRuleset.Create()]);
        WorldSession session = FoundationSessionFixture.CreatePhase05PausedSession(registry);
        _ = Submit(session, 0, "gamma");
        _ = Submit(session, 1, "alpha");
        _ = Submit(session, 0, "delta");
        _ = Submit(session, 1, "beta");
        Require(session.Resume().Success, "Fixture session did not resume.");
        Require(session.StepOneTick().Success, "Fixture tick zero failed.");
        Require(session.StepOneTick().Success, "Fixture tick one failed.");
        Require(session.Pause().Success, "Fixture session did not pause.");
        return session;
    }

    private static IReadOnlyList<DiagnosticCheck> RunRecoveryChecks(string validPackage, string root)
    {
        List<DiagnosticCheck> checks = [];
        checks.Add(RecoveryScenario("valid-target", root, target =>
        {
            File.Copy(validPackage, target);
            File.Copy(validPackage, target + ".writing");
            File.Copy(validPackage, target + ".previous");
        }, result => result.Success && !File.Exists(result.TargetPath + ".writing") && !File.Exists(result.TargetPath + ".previous")));
        checks.Add(RecoveryScenario("promote-writing", root, target => File.Copy(validPackage, target + ".writing"),
            result => result.Success && File.Exists(result.TargetPath)));
        checks.Add(RecoveryScenario("restore-previous", root, target => File.Copy(validPackage, target + ".previous"),
            result => result.Success && File.Exists(result.TargetPath)));
        checks.Add(RecoveryScenario("quarantine-restore", root, target =>
        {
            File.WriteAllBytes(target, [0x00, 0x01, 0x02]);
            File.Copy(validPackage, target + ".previous");
        }, result => result.Success && File.Exists(result.TargetPath + ".corrupt")));
        checks.Add(RecoveryScenario("quarantine-promote", root, target =>
        {
            File.WriteAllBytes(target, [0x00, 0x01, 0x02]);
            File.Copy(validPackage, target + ".writing");
        }, result => result.Success && File.Exists(result.TargetPath + ".corrupt")));
        return checks.AsReadOnly();
    }

    private static DiagnosticCheck RecoveryScenario(string id, string root, Action<string> arrange, Func<RecoveryResult, bool> validate)
    {
        string directory = Path.Combine(root, id);
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "scenario.emergence-world");
        arrange(target);
        RecoveryResult result = new WorldPackageRecovery().Recover(target);
        return Check("recovery." + id, validate(result), "Recovery scenario", $"success={result.Success};actions={result.Actions.Count}");
    }

    private static IEnumerable<string> WorldPackageSidecarNames(string packagePath) =>
        [packagePath + ".writing", packagePath + ".previous", packagePath + ".lock", packagePath + ".corrupt"];

    private static AcceptedSessionCommand Submit(WorldSession session, ulong tick, string message)
    {
        OperationResult<AcceptedSessionCommand> result = session.SubmitCommand(new SessionCommandRequest(
            new SimulationTick(tick),
            new SessionCommandTypeId(FoundationSessionFixture.TraceCommandType),
            FoundationSessionFixture.TracePayload(message)));
        Require(result.Success, result.Issues.Count == 0 ? "Command submission failed." : result.Issues[0].Detail);
        return result.Value;
    }

    private static Sha256Digest ComputeTrace(
        WorldSessionSnapshot snapshot,
        WorldPackageManifest manifest,
        string loadedState,
        SequenceNumber nextCommandSequence,
        IReadOnlyList<string> continuationEventIds,
        string finalState)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString("ProjectEmergence.PersistenceTrace.v1");
        writer.WriteDigest(AlgorithmCatalog.Phase05.Digest);
        writer.WriteDigest(snapshot.Definition.CommandProcessorCatalogDigest!.Value);
        writer.WriteDigest(snapshot.Definition.Digest);
        writer.WriteDigest(snapshot.StateDigest);
        writer.WriteDigest(snapshot.Digest);
        writer.WriteDigest(manifest.PackageIdentityDigest);
        writer.WriteDigest(Sha256Digest.Parse(loadedState));
        writer.WriteUInt128(nextCommandSequence.Value);
        writer.WriteUInt64(checked((ulong)continuationEventIds.Count));
        foreach (string eventId in continuationEventIds) writer.WriteString(eventId);
        writer.WriteDigest(Sha256Digest.Parse(finalState));
        return writer.FinalizeDigest();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static DiagnosticCheck Check(string id, bool success, string summary, string detail) =>
        new(id, success ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, summary, detail);
}
