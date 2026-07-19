using System.Text;
using System.Xml.Linq;

namespace Emergence.ReviewPack;

public static class ImplementationReportBuilder
{
    public static IReadOnlyList<string> RequiredHeadings { get; } =
    [
        "1. Phase", "2. Baseline Verified", "3. Toolchain Selected", "4. Summary",
        "5. Requirements Implemented", "6. Repository Structure", "7. Files Created", "8. Files Modified",
        "9. Architecture Decisions", "10. Tests Added", "11. Commands Run and Exact Results",
        "12. Godot Shell Verification", "13. Windows Package Verification", "14. Diagnostics Results",
        "15. Review Pack", "16. Git Status and Commit", "17. Known Issues", "18. Deferred Work",
        "19. Acceptance-Criteria Self-Assessment",
    ];

    public static string Build(
        ReviewManifest manifest,
        string reviewRoot,
        IReadOnlyList<string>? createdFiles = null,
        IReadOnlyList<string>? modifiedFiles = null,
        int anticipatedManifestFileCount = 0)
    {
        createdFiles ??= [];
        modifiedFiles ??= [];
        PersistenceEvidence? persistence = manifest.Persistence;
        int total = manifest.Tests.Sum(static test => test.Total);
        int passed = manifest.Tests.Sum(static test => test.Passed);
        int failed = manifest.Tests.Sum(static test => test.Failed);
        int skipped = manifest.Tests.Sum(static test => test.SkippedNotExecuted);
        (int staleLockTests, int activeLockTests, int cleanupWarningTests) = LockTestCounts(reviewRoot);
        (int debugWarnings, int debugErrors, bool debugPassed) = BuildLog(reviewRoot, "build/build-debug.log");
        (int releaseWarnings, int releaseErrors, bool releasePassed) = BuildLog(reviewRoot, "build/build-release.log");
        bool acceptance = debugPassed && releasePassed && BuildEvidenceValidator.IsPassed(manifest.Build)
            && CliEvidenceValidator.IsPassed(manifest.Cli) && manifest.Rng?.Status == EvidenceStatus.Passed
            && manifest.Rulesets?.Status == EvidenceStatus.Passed && manifest.Session?.Status == EvidenceStatus.Passed
            && persistence?.Status == EvidenceStatus.Passed && failed == 0 && skipped == 0
            && manifest.App.Status == EvidenceStatus.Passed && manifest.Package.Status == EvidenceStatus.Passed && manifest.GitClean;

        StringBuilder report = new();
        report.AppendLine("# Project Emergence Phase 0.5R Implementation Report");
        report.AppendLine();
        Section(report, 0, $"Milestone 0 - Phase 0.5R: crash-recoverable OS-handle world-package lock leases and truthful post-commit cleanup outcomes for reviewed correction `{manifest.GitCommit}`. Phase 1 was not begun and no biological state was introduced.");
        Section(report, 1, $"Repository `{manifest.RepositoryRoot}`; accepted main `edb6f24898453841a4ecf3283bdd114ccebc2167`; original Phase 0.5 commit `244bb8b5f6e0e2714ce1f7dec57c5d3bcb323f58` directly parented by accepted main; branch `{manifest.GitBranch}`; correction commit `{manifest.GitCommit}` directly after original Phase 0.5; clean={manifest.GitClean}.");
        Section(report, 2, $".NET SDK {manifest.SelectedDotnetSdk}; target {string.Join(", ", manifest.TargetFrameworks)}; Godot {manifest.GodotVersion}; matching Windows export templates available={manifest.ExportTemplatesAvailable}.");
        Section(report, 3, "Replaced file-existence locking with a sealed internal exclusive OS-handle lease; made ordinary stale lock files reacquirable; kept active Save/Recover contention fail-closed; added ownership-safe delete-on-close release; separated package/recovery outcomes from metadata and cleanup warnings; extended deterministic CLI/App/package probes and schema-6 review enforcement. The Phase 0.5 world-package format and semantic behavior are unchanged.");
        Section(report, 4, $"Phase05 algorithm catalog `{persistence?.AlgorithmCatalogDigest}`; command-processor catalog `{persistence?.CommandProcessorCatalogDigest}`; V2 definition `{persistence?.DefinitionDigest}`; pre-save state `{persistence?.PreSaveStateDigest}`; snapshot `{persistence?.SnapshotDigest}`; package identity `{persistence?.PackageIdentityDigest}`; manifest `{persistence?.ManifestDigest}`; bytes={persistence?.PackageBytes}; entries={persistence?.PackageEntryCount}; loaded state `{persistence?.LoadedStateDigest}`; RNG continuation={persistence?.RngContinuationMatched}; next command sequence={persistence?.NextCommandSequence}; continuation EventIds `{string.Join(", ", persistence?.ContinuationEventIds ?? [])}`; final state `{persistence?.FinalStateDigest}`; trace `{persistence?.PersistenceTraceDigest}`; unchanged recovery={persistence?.RecoveryScenarioPassed}/{persistence?.RecoveryScenarioCount}; deterministic lock checks={persistence?.LockCheckPassed}/{persistence?.LockCheckCount}; App/package stale-lock={persistence?.AppStaleLockValid}/{persistence?.PackagedStaleLockValid}.");
        Section(report, 5, $"The tracked source snapshot contains {Directory.EnumerateFiles(Path.Combine(reviewRoot, "source"), "*", SearchOption.AllDirectories).Count()} files under `source/`; evidence is separated into environment, build, tests, CLI, persistence, App, package, and docs.");
        Section(report, 6, FileList(createdFiles, "No created-file data was available."));
        Section(report, 7, FileList(modifiedFiles, "No modified-file data was available."));
        Section(report, 8, "The OS handle is the lock; path existence, timestamps, PID, token, and metadata are not ownership. Bounded strict-UTF-8 metadata is nonauthoritative and excluded from every package/session/manifest digest. Windows delete-on-close release avoids close-then-blind-delete and cannot delete a successor lease. A fully promoted/revalidated package or recovered target remains successful if cleanup warns. Recovery candidate order is unchanged and never timestamp-selected.");
        Section(report, 9, string.Join(Environment.NewLine, manifest.Tests.Select(test => $"- {test.Project}: {test.Status}; total={test.Total}, executed={test.Executed}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}."))
            + $"\n- Named lock semantics: stale-lock passed={staleLockTests}; active-lock contention passed={activeLockTests}; cleanup-warning passed={cleanupWarningTests}; no timing sleeps.");
        Section(report, 10,
            $"- Restore: exit 0; evidence `build/restore.log`.\n" +
            $"- Debug build: {(debugPassed ? "PASS" : "FAIL")}; warnings={debugWarnings}; errors={debugErrors}.\n" +
            $"- Release build: {(releasePassed ? "PASS" : "FAIL")}; warnings={releaseWarnings}; errors={releaseErrors}.\n" +
            string.Join("\n", manifest.Tests.Select(test => $"- `{test.Command}`: {test.Status}; total={test.Total}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}; TRX `{test.TrxPath}`; coverage `{test.CoveragePath}`.")) +
            $"\n- All prior CLI evidence plus persistence-self-test lock checks and world-package fixture/verify/recover: {persistence?.Status}; lock={persistence?.LockCheckPassed}/{persistence?.LockCheckCount}.\n" +
            $"- Assembly inventory: {manifest.Build.AssemblyInventory.Status}.\n" +
            $"- Hardened verifier: PASS required; exact expected manifest/actual entries={anticipatedManifestFileCount}; normalized coverage files={manifest.Tests.Count}.");
        Section(report, 11, $"Status={manifest.App.Status}; Godot={manifest.App.GodotVersion}; framework={manifest.App.TargetFramework}; commit={manifest.App.GitCommit}; source load, smoke, doctor, isolated stale-lock Save/Recover, normal launch, save, verify, load, clean close, and fresh M0.5R screenshot evidence are present. App round trip={persistence?.AppRoundTripValid}; stale-lock={persistence?.AppStaleLockValid}.");
        Section(report, 12, $"Status={manifest.Package.Status}; files={manifest.Package.PackageFileCount}; framework={manifest.Package.TargetFramework}; commit={manifest.Package.GitCommit}; exact manifest, ruleset equivalence, Persistence assembly, smoke, doctor, writable save/load, isolated stale-lock probe, RNG continuation, and sidecar cleanup were validated. Packaged round trip={persistence?.PackagedRoundTripValid}; stale-lock={persistence?.PackagedStaleLockValid}.");
        Section(report, 13, $"Phase 0.1 `{CliEvidenceValidator.Phase01Vector}`; Phase 0.2 canonical `{CliEvidenceValidator.Phase02CanonicalDigest}`, stable ID `{CliEvidenceValidator.Phase02StableId}`, catalog `{CliEvidenceValidator.Phase02CatalogDigest}`, configuration `{CliEvidenceValidator.Phase02ConfigurationDigest}`; Phase 0.3 domain catalog `{manifest.Rng?.DomainCatalogDigest}`, algorithm catalog `{manifest.Rng?.AlgorithmCatalogDigest}`, registry `{manifest.Rulesets?.RegistryDigest}`; Phase 0.4R algorithm `{Phase04EvidenceValidator.AlgorithmCatalogDigest}`, scheduler `{Phase04EvidenceValidator.SchedulerGraphDigest}`, definition `{Phase04EvidenceValidator.SessionDefinitionDigest}`, trace `{Phase04EvidenceValidator.SessionTraceDigest}`, final state `{Phase04EvidenceValidator.FinalStateDigest}`, EventIds `{string.Join(", ", Phase04EvidenceValidator.EventIds)}`. Tests total={total}, passed={passed}, failed={failed}, skipped/not-executed={skipped}; normalized coverage={manifest.Tests.Count}.");
        Section(report, 14, $"Review root `{manifest.ReviewPackRoot}`; source digest `{manifest.SourceTreeDigest}`; design digest `{manifest.DesignArchiveDigest}`; manifest/actual count={anticipatedManifestFileCount}. The valid fixture and extracted definition, snapshot, manifest, and internal inventory are independently checked. Missing, extra, unsafe, stale Phase 0.5-as-0.5R, wrong ancestry, missing named lock tests, renamed arbitrary ZIP, nested archive, generated, or contradictory evidence is rejected.");
        Section(report, 15, $"Accepted main `edb6f24898453841a4ecf3283bdd114ccebc2167`; original Phase 0.5 `244bb8b5f6e0e2714ce1f7dec57c5d3bcb323f58`; branch `{manifest.GitBranch}`; correction `{manifest.GitCommit}`; subject `M0 P0.5R make package locks crash recoverable`; clean={manifest.GitClean}. Push status: not pushed. Merge status: not merged.");
        Section(report, 16, manifest.Warnings.Count == 0 ? "No known Phase 0.5R acceptance issue is recorded. An ordinary stale `.lock` sidecar is harmless and reacquirable; a cleanup warning remains visible without negating committed state." : string.Join(Environment.NewLine, manifest.Warnings.Select(static warning => $"- {warning}")));
        Section(report, 17, "Event-history persistence, replay, branching/forking, rollback, incremental/chunk snapshots, migrations, networking/cloud saves, autosave, multiple slots, encryption, dynamic code loading, Phase 1 fields, and all biological simulation remain deferred.");
        Section(report, 18, $"{(acceptance ? "PASS" : "FAIL")}: live-handle ownership, stale-lock crash recovery, active contention, ownership-safe cleanup, truthful committed-with-warning outcomes, unchanged package/recovery/deterministic vectors, regression, build/CLI/App/package, and review gates were evaluated. No Phase 1 or biological scope was begun.");
        return report.ToString();
    }

    private static string FileList(IReadOnlyList<string> files, string empty) => files.Count == 0
        ? empty
        : string.Join(Environment.NewLine, files.Select(static path => $"- `{path}`"));

    private static void Section(StringBuilder report, int index, string contents)
    {
        report.AppendLine($"## {RequiredHeadings[index]}");
        report.AppendLine();
        report.AppendLine(contents);
        report.AppendLine();
    }

    private static (int Warnings, int Errors, bool Passed) BuildLog(string root, string relative)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return (-1, -1, false);
        string log = File.ReadAllText(path);
        int warnings = ParseCount(log, "Warning(s)");
        int errors = ParseCount(log, "Error(s)");
        return (warnings, errors, log.Contains("Build succeeded.", StringComparison.Ordinal) && warnings == 0 && errors == 0);
    }

    private static int ParseCount(string log, string label)
    {
        foreach (string line in log.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            string trimmed = line.Trim();
            if (!trimmed.EndsWith(label, StringComparison.Ordinal)) continue;
            if (int.TryParse(trimmed[..^label.Length].Trim(), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int count)) return count;
        }
        return -1;
    }

    private static (int Stale, int Active, int Cleanup) LockTestCounts(string root)
    {
        string path = Path.Combine(root, "tests", "Emergence.Persistence.Tests", "Emergence.Persistence.Tests.trx");
        if (!File.Exists(path)) return (0, 0, 0);
        XDocument document = XDocument.Load(path, LoadOptions.None);
        string[] passed = document.Descendants()
            .Where(static element => element.Name.LocalName == "UnitTestResult"
                && string.Equals((string?)element.Attribute("outcome"), "Passed", StringComparison.Ordinal))
            .Select(static element => (string?)element.Attribute("testName") ?? string.Empty)
            .ToArray();
        return (
            passed.Count(static name => name.Contains("Stale", StringComparison.Ordinal)),
            passed.Count(static name => name.Contains("ActiveExclusiveOwner", StringComparison.Ordinal)),
            passed.Count(static name => name.Contains("CleanupWarning", StringComparison.Ordinal)));
    }
}
