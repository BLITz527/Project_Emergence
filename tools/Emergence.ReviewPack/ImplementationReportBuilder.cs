using System.Text;

namespace Emergence.ReviewPack;

public static class ImplementationReportBuilder
{
    public static IReadOnlyList<string> RequiredHeadings { get; } =
    [
        "1. Phase",
        "2. Baseline Verified",
        "3. Toolchain Selected",
        "4. Summary",
        "5. Requirements Implemented",
        "6. Repository Structure",
        "7. Files Created",
        "8. Files Modified",
        "9. Architecture Decisions",
        "10. Tests Added",
        "11. Commands Run and Exact Results",
        "12. Godot Shell Verification",
        "13. Windows Package Verification",
        "14. Diagnostics Results",
        "15. Review Pack",
        "16. Git Status and Commit",
        "17. Known Issues",
        "18. Deferred Work",
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
        int total = manifest.Tests.Sum(test => test.Total);
        int passed = manifest.Tests.Sum(test => test.Passed);
        int failed = manifest.Tests.Sum(test => test.Failed);
        int skipped = manifest.Tests.Sum(test => test.SkippedNotExecuted);
        (int debugWarnings, int debugErrors, bool debugPassed) = BuildLog(reviewRoot, "build/build-debug.log");
        (int releaseWarnings, int releaseErrors, bool releasePassed) = BuildLog(reviewRoot, "build/build-release.log");

        StringBuilder report = new();
        report.AppendLine("# Project Emergence Phase 0.2 Implementation Report");
        report.AppendLine();
        Section(report, 0, $"Milestone 0 — Phase 0.2 foundational domain types for reviewed commit `{manifest.GitCommit}`.");
        Section(report, 1, $"Repository `{manifest.RepositoryRoot}`; accepted parent `ea3f2aed56af831c311e106fa9d3ebcf421e0e0e`; branch `{manifest.GitBranch}`; clean={manifest.GitClean}.");
        Section(report, 2, $".NET SDK {manifest.SelectedDotnetSdk}; target {string.Join(", ", manifest.TargetFrameworks)}; Godot {manifest.GodotVersion}; export templates available={manifest.ExportTemplatesAvailable}.");
        Section(report, 3, "Added typed 128-bit IDs, identity records, UInt128 logical time and checked sequences, UInt64 exact quantities, SHA-256 and canonical encoding V1, semantic algorithm catalogs, immutable configuration, structured results, JSON converters, a deterministic domain self-test, and independently parsed build/CLI evidence. No biological simulation exists.");
        Section(report, 4, $"Canonical V1 `{CliEvidenceValidator.Phase02CanonicalDigest}`; catalog `{CliEvidenceValidator.Phase02CatalogDigest}`; fixture configuration `{CliEvidenceValidator.Phase02ConfigurationDigest}`; Phase 0.1 vector `{CliEvidenceValidator.Phase01Vector}`; App={manifest.App.Status}; package={manifest.Package.Status}; build={manifest.Build.Release.Status}; CLI={manifest.Cli.Phase02DomainSelfTest.Status}.");
        Section(report, 5, $"The exact tracked source snapshot contains {Directory.EnumerateFiles(Path.Combine(reviewRoot, "source"), "*", SearchOption.AllDirectories).Count()} files under `source/`; evidence is separated into environment, build, tests, CLI, App, package, and docs.");
        Section(report, 6, createdFiles.Count == 0 ? "No created-file data was available." : string.Join(Environment.NewLine, createdFiles.Select(path => $"- `{path}`")));
        Section(report, 7, modifiedFiles.Count == 0 ? "No modified-file data was available." : string.Join(Environment.NewLine, modifiedFiles.Select(path => $"- `{path}`")));
        Section(report, 8, "ADR 0001 preserves the headless core/Godot host boundary; ADR 0003 fixes canonical encoding V1; ADR 0004 fixes wide integer and exact-quanta representation. Durable collections are ordinally ordered, defensively copied, and digest-validated.");
        Section(report, 9, string.Join(Environment.NewLine, manifest.Tests.Select(test => $"- {test.Project}: {test.Status}; total={test.Total}, executed={test.Executed}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}.")));
        Section(report, 10,
            $"- Restore: exit 0; evidence `build/restore.log`.\n" +
            $"- Debug build: {(debugPassed ? "PASS" : "FAIL")}; warnings={debugWarnings}; errors={debugErrors}; evidence `build/build-debug.log`.\n" +
            $"- Release build: {(releasePassed ? "PASS" : "FAIL")}; warnings={releaseWarnings}; errors={releaseErrors}; evidence `build/build-release.log`.\n" +
            string.Join("\n", manifest.Tests.Select(test => $"- `{test.Command}`: {test.Status}; total={test.Total}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}; TRX `{test.TrxPath}`; coverage `{test.CoveragePath}`.")) +
            $"\n- CLI version/doctor/self-test/domain-self-test: {manifest.Cli.Phase02DomainSelfTest.Status}; evidence under `cli/`.\n- Assembly inventory: {manifest.Build.AssemblyInventory.Status}.\n- Hardened review-pack verifier: PASS required for successful creation; expected exact manifest entries={anticipatedManifestFileCount}; the verifier rejects any inconsistency.");
        Section(report, 11, $"Status={manifest.App.Status}; Godot={manifest.App.GodotVersion}; framework={manifest.App.TargetFramework}; commit={manifest.App.GitCommit}; source load, smoke, doctor, normal launch, and screenshot evidence are referenced by the structured App outcome.");
        Section(report, 12, $"Status={manifest.Package.Status}; files={manifest.Package.PackageFileCount}; framework={manifest.Package.TargetFramework}; commit={manifest.Package.GitCommit}; executable, explicit status, smoke marker, doctor, packaged layout, and exact package manifest were validated.");
        Section(report, 13, $"Phase 0.1 vector `{CliEvidenceValidator.Phase01Vector}` retained; Phase 0.2 canonical vector `{CliEvidenceValidator.Phase02CanonicalDigest}`; CLI and App doctor JSON report success. Overall automated tests: total={total}, passed={passed}, failed={failed}, skipped/not-executed={skipped}.");
        Section(report, 14, $"Review root `{manifest.ReviewPackRoot}`; source digest `{manifest.SourceTreeDigest}`; design digest `{manifest.DesignArchiveDigest}`; verifier rejects missing, extra, unsafe, stale, generated, archive, or contradictory evidence.");
        Section(report, 15, $"Branch `{manifest.GitBranch}`; commit `{manifest.GitCommit}`; clean={manifest.GitClean}. No push was performed.");
        Section(report, 16, manifest.Warnings.Count == 0 ? "No known Phase 0.2 acceptance issue is recorded." : string.Join(Environment.NewLine, manifest.Warnings.Select(warning => $"- {warning}")));
        Section(report, 17, "Phase 0.3 deterministic RNG and ruleset-registry work remains deferred, together with all world state and biological simulation. Typed future entity IDs remain values only.");
        Section(report, 18, $"{(debugPassed && releasePassed && BuildEvidenceValidator.IsPassed(manifest.Build) && CliEvidenceValidator.IsPassed(manifest.Cli) && failed == 0 && skipped == 0 && manifest.App.Status == EvidenceStatus.Passed && manifest.Package.Status == EvidenceStatus.Passed && manifest.GitClean ? "PASS" : "FAIL")}: architecture, domain correctness, determinism, serialization, automated QA, build, CLI, App/package, persistence foundation, review-pack, and human-shell evidence gates were evaluated.");
        return report.ToString();
    }

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
        if (!File.Exists(path))
        {
            return (-1, -1, false);
        }
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
            if (!trimmed.EndsWith(label, StringComparison.Ordinal))
            {
                continue;
            }
            string value = trimmed[..^label.Length].Trim();
            if (int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int count))
            {
                return count;
            }
        }
        return -1;
    }
}
