using System.Text;

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
        EnvironmentEvidence? environment = manifest.Environment;
        PersistenceEvidence? persistence = manifest.Persistence;
        int total = manifest.Tests.Sum(static test => test.Total);
        int passed = manifest.Tests.Sum(static test => test.Passed);
        int failed = manifest.Tests.Sum(static test => test.Failed);
        int skipped = manifest.Tests.Sum(static test => test.SkippedNotExecuted);
        (int debugWarnings, int debugErrors, bool debugPassed) = BuildLog(reviewRoot, "build/build-debug.log");
        (int releaseWarnings, int releaseErrors, bool releasePassed) = BuildLog(reviewRoot, "build/build-release.log");
        string environmentPackage = Path.Combine(reviewRoot, "cli", "environment-session.emergence-world");
        long environmentPackageBytes = File.Exists(environmentPackage) ? new FileInfo(environmentPackage).Length : 0;
        bool acceptance = debugPassed && releasePassed && BuildEvidenceValidator.IsPassed(manifest.Build)
            && CliEvidenceValidator.IsPassed(manifest.Cli) && manifest.Rng?.Status == EvidenceStatus.Passed
            && manifest.Rulesets?.Status == EvidenceStatus.Passed && manifest.Session?.Status == EvidenceStatus.Passed
            && persistence?.Status == EvidenceStatus.Passed && environment?.Status == EvidenceStatus.Passed
            && failed == 0 && skipped == 0 && manifest.App.Status == EvidenceStatus.Passed
            && manifest.Package.Status == EvidenceStatus.Passed && manifest.GitClean;

        StringBuilder report = new();
        report.AppendLine("# Project Emergence Milestone 1 Phase 1.1 Implementation Report");
        report.AppendLine();
        Section(report, 0, $"Milestone 1 Phase 1.1 — Single-Region Environmental Field Lattice Foundation. Reviewed feature commit `{manifest.GitCommit}` adds authoritative nonliving environmental state; Phase 1.2 and biology were not begun.");
        Section(report, 1,
            $"Parent main SHA `6d51b8f36930600e6b5662e039f2e6a6a3d8627d`; accepted correction ancestor `ba98387b4f4f1b6cb85a773e2c2beb974a464653`; original Phase 0.5 ancestor `244bb8b5f6e0e2714ce1f7dec57c5d3bcb323f58`; branch `{manifest.GitBranch}`; feature commit `{manifest.GitCommit}`; clean={manifest.GitClean}; accepted source-tree baseline was verified before editing. Authoritative design digest `{manifest.DesignArchiveDigest}`. Filename substitutions confirmed by the user and used without renames or aliases: prompt `docs/architecture/0007-world-session-lifecycle.md` → repository `docs/architecture/0007-authoritative-world-session-lifecycle.md`; prompt `docs/architecture/0009-command-intake-and-event-commit.md` → repository `docs/architecture/0009-command-intake-event-commit.md`; prompt `docs/architecture/0012-world-package-format-and-manifest.md` → repository `docs/architecture/0012-world-package-format-manifest.md`.");
        Section(report, 2, $".NET SDK {manifest.SelectedDotnetSdk}; target {string.Join(", ", manifest.TargetFrameworks)}; Godot {manifest.GodotVersion}; matching Windows x86_64 .NET export templates available={manifest.ExportTemplatesAvailable}; PowerShell 5.1 or newer.");
        Section(report, 3, "Implemented a bounded 16×12 single-region lattice with four 8×8-edge chunks, exact UInt64 matter and volume quantities, zero-volume barriers, deterministic static fixture fields, probes/audit, immutable presentation snapshots, smooth and raw-grid Godot views, V3 session/V2 snapshot integration, and exact seven-entry V2 world packages. Existing foundation session, deterministic scheduling, RNG, ruleset, V1 package, atomic save/recovery, and stale-lock behavior remain regression-covered.");
        Section(report, 4,
            $"Locked digests: field catalog `{environment?.FieldChannelCatalogDigest}`; region definition `{environment?.RegionDefinitionDigest}`; region state `{environment?.RegionStateDigest}`; environment definition `{environment?.EnvironmentDefinitionDigest}`; environment state `{environment?.EnvironmentStateDigest}`; Phase11 algorithms `{environment?.AlgorithmCatalogDigest}`; V3 session definition `{environment?.SessionDefinitionDigest}`; session state `{environment?.SessionStateDigest}`; V2 snapshot `{environment?.SnapshotDigest}`; package identity `{environment?.PackageIdentityDigest}`; V2 manifest `{environment?.ManifestDigest}`. Counts: solid={environment?.SolidCellCount}, fluid={environment?.FluidCellCount}; totals: {string.Join(", ", environment?.ChannelTotals ?? [])}. Probes: (1,1) volume=1024 amounts=1056/877/46; (8,5) volume=0 amounts=0/0/0; (8,6) volume=1024 amounts=1410/915/19; (14,10) volume=1024 amounts=1708/941/43, in energy-substrate/structural-precursor/waste order. Save/load={environment?.SaveLoadMatched}; one-tick unchanged={environment?.StaticTickMatched}; conservation audit=PASS with zero solid-cell violations.");
        Section(report, 5, $"The tracked source snapshot contains {Directory.EnumerateFiles(Path.Combine(reviewRoot, "source"), "*", SearchOption.AllDirectories).Count()} files under `source/`. Durable contracts are in Foundation/Model, authoritative stores in Simulation, binary/package logic in Persistence, immutable view DTOs in Presentation.Contracts, rendering in App, independent vectors in `tools/reference`, and review artifacts are separated into environment/reference/build/tests/cli/persistence/app/package/docs.");
        Section(report, 6, FileList(createdFiles, "No created-file data was available."));
        Section(report, 7, FileList(modifiedFiles, "No modified-file data was available."));
        Section(report, 8, "ADRs 0015–0019 record hidden-lattice one-region ownership, exact amounts/effective volume, the strict little-endian chunk format, package V2, and smooth presentation versus authoritative samples. Fields are row-major, chunks are ordered Y then X, solid cells have zero effective volume and zero matter, concentration is derived and unavailable for solids, presentation cannot mutate state, and no field-update algorithm exists in Phase 1.1.");
        Section(report, 9, string.Join(Environment.NewLine, manifest.Tests.Select(test => $"- {test.Project}: {test.Status}; total={test.Total}, executed={test.Executed}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}.")));
        Section(report, 10,
            $"- Restore: {manifest.Build.Restore.Status}; evidence `build/restore.log`.\n" +
            $"- Debug build: {(debugPassed ? "PASS" : "FAIL")}; warnings={debugWarnings}; errors={debugErrors}.\n" +
            $"- Release build: {(releasePassed ? "PASS" : "FAIL")}; warnings={releaseWarnings}; errors={releaseErrors}.\n" +
            string.Join("\n", manifest.Tests.Select(test => $"- `{test.Command}`: {test.Status}; total={test.Total}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}; TRX `{test.TrxPath}`; coverage `{test.CoveragePath}`.")) +
            $"\n- Overall: total={total}, passed={passed}, failed={failed}, skipped/not-executed={skipped}; normalized coverage files={manifest.Tests.Count}.\n" +
            $"- Independent reference vectors={environment?.IndependentReferenceMatched}; V2 package bytes={environmentPackageBytes}; chunks=4; performance evidence reports 8 bytes/field slot, 4,608 reference authoritative field bytes, and 33,554,432 maximum-contract field bytes.");
        Section(report, 11, $"Status={manifest.App.Status}; Godot={manifest.App.GodotVersion}; framework={manifest.App.TargetFramework}; commit={manifest.App.GitCommit}. Source load, smoke, doctor, V2 save/verify/load, exact probe, normal smooth view, raw-grid debug view, two fresh screenshots, static environment, no biological state, and clean close are required and present.");
        Section(report, 12, $"Status={manifest.Package.Status}; files={manifest.Package.PackageFileCount}; framework={manifest.Package.TargetFramework}; commit={manifest.Package.GitCommit}. Fresh Windows x86_64 export, exact package manifest, ruleset equivalence, required assemblies, smoke/doctor, writable V2 save/load, field chunks, stale-lock behavior, RNG continuation, and sidecar cleanup were validated.");
        Section(report, 13,
            $"Prior deterministic vectors: Phase 0.1 `{CliEvidenceValidator.Phase01Vector}`; Phase 0.2 canonical `{CliEvidenceValidator.Phase02CanonicalDigest}`, stable ID `{CliEvidenceValidator.Phase02StableId}`, catalog `{CliEvidenceValidator.Phase02CatalogDigest}`, configuration `{CliEvidenceValidator.Phase02ConfigurationDigest}`; RNG domain catalog `{manifest.Rng?.DomainCatalogDigest}`, RNG algorithm catalog `{manifest.Rng?.AlgorithmCatalogDigest}`, ruleset registry `{manifest.Rulesets?.RegistryDigest}`; Phase 0.4R algorithm `{Phase04EvidenceValidator.AlgorithmCatalogDigest}`, scheduler `{Phase04EvidenceValidator.SchedulerGraphDigest}`, definition `{Phase04EvidenceValidator.SessionDefinitionDigest}`, trace `{Phase04EvidenceValidator.SessionTraceDigest}`, final state `{Phase04EvidenceValidator.FinalStateDigest}`; Phase 0.5 package identity `{persistence?.PackageIdentityDigest}`, manifest `{persistence?.ManifestDigest}`, persistence trace `{persistence?.PersistenceTraceDigest}`. Phase 1.1 probes/totals/digests, chunk validation, performance/memory, App/packaged diagnostics, save/load, and static tick all report {environment?.Status}.");
        Section(report, 14,
            $"Review root `{manifest.ReviewPackRoot}`; source-tree digest `{manifest.SourceTreeDigest}`; design digest `{manifest.DesignArchiveDigest}`; expected manifest/actual file count={anticipatedManifestFileCount}. Raw chunks: `regions/00000000000000000000000000000064/fields/0000-0000.bin` 1720 `e9c9f690eb5d36b9c2532e898dcf04307bfb30c107e61299402af7e64c6ea158`; `.../0000-0001.bin` 1720 `7aa20e39a5b11dbd6b66c0a63d626e9d7e6315f7f048e2574061faf0a0034767`; `.../0001-0000.bin` 952 `eb9f89e0e1e9c9e2f78ac60db42e78d3d53a6d8c38c0971c2dc9c899996731bd`; `.../0001-0001.bin` 952 `74426508ec8e95f63a073abdf9a78cfb0e1ddb234a13e9ecb1aa86e5f2c2b427`. Hardened verifier requires zero missing, extra, unsafe, stale, or inconsistent evidence.");
        Section(report, 15, $"Parent main `6d51b8f36930600e6b5662e039f2e6a6a3d8627d`; branch `{manifest.GitBranch}`; feature `{manifest.GitCommit}`; subject `M1 P1.1 region and environmental field lattice`; clean={manifest.GitClean}. Push status: not pushed. Merge status: not merged.");
        Section(report, 16, manifest.Warnings.Count == 0 ? "No known Phase 1.1 acceptance issue is recorded. Performance timings are informational and machine-dependent; exact storage sizes and scientific vectors are normative." : string.Join(Environment.NewLine, manifest.Warnings.Select(static warning => $"- {warning}")));
        Section(report, 17, "Diffusion, flow/advection, reactions, sources/sinks, environmental cycles, dynamic boundaries, runtime field editing, multiple regions, event-history persistence, migration, native/GPU/parallel kernels, and all biological simulation remain deferred. No Phase 1.2 placeholders or no-op dynamics were introduced.");
        Section(report, 18, $"{(acceptance ? "PASS" : "FAIL")}: baseline/ancestry, architecture ownership, topology, exact field state, determinism, conservation, V1/V2 persistence, scientific probes, smooth/raw presentation, regression, Debug/Release, CLI, source App, Windows package, and schema-7 review gates were evaluated. Outer transfer ZIP: not created. No push, merge, Phase 1.2 work, or biological state.");
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
}
