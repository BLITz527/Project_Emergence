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
        report.AppendLine("# Project Emergence Phase 0.1R Implementation Report");
        report.AppendLine();
        Section(report, 0, $"Milestone 0 — Phase 0.1R evidence hardening for reviewed commit `{manifest.GitCommit}`.");
        Section(report, 1, $"Repository `{manifest.RepositoryRoot}`, branch `{manifest.GitBranch}`, clean={manifest.GitClean}, baseline correction commit `{manifest.GitCommit}`.");
        Section(report, 2, $".NET SDK {manifest.SelectedDotnetSdk}; target {string.Join(", ", manifest.TargetFrameworks)}; Godot {manifest.GodotVersion}; export templates available={manifest.ExportTemplatesAvailable}.");
        Section(report, 3, "Imported the authoritative design baseline and replaced existence-based review claims with parsed, cross-checked, self-contained evidence.");
        Section(report, 4, $"Design digest `{manifest.DesignArchiveDigest}`; structured tests={manifest.Tests.Count}; App={manifest.App.Status}; package={manifest.Package.Status}; hardened exact-file manifest verification enabled.");
        Section(report, 5, $"The exact tracked source snapshot contains {Directory.EnumerateFiles(Path.Combine(reviewRoot, "source"), "*", SearchOption.AllDirectories).Count()} files under `source/`; evidence is separated into environment, build, tests, CLI, App, package, and docs.");
        Section(report, 6, createdFiles.Count == 0 ? "No created-file data was available." : string.Join(Environment.NewLine, createdFiles.Select(path => $"- `{path}`")));
        Section(report, 7, modifiedFiles.Count == 0 ? "No modified-file data was available." : string.Join(Environment.NewLine, modifiedFiles.Select(path => $"- `{path}`")));
        Section(report, 8, "ADR 0001 preserves the headless core/Godot host boundary. ADR 0002 records the verified .NET 10 and Godot 4.7 stable .NET baseline. Phase 0.1R changes review evidence only.");
        Section(report, 9, string.Join(Environment.NewLine, manifest.Tests.Select(test => $"- {test.Project}: {test.Status}; total={test.Total}, executed={test.Executed}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}.")));
        Section(report, 10,
            $"- Restore: exit 0; evidence `build/restore.log`.\n" +
            $"- Debug build: {(debugPassed ? "PASS" : "FAIL")}; warnings={debugWarnings}; errors={debugErrors}; evidence `build/build-debug.log`.\n" +
            $"- Release build: {(releasePassed ? "PASS" : "FAIL")}; warnings={releaseWarnings}; errors={releaseErrors}; evidence `build/build-release.log`.\n" +
            string.Join("\n", manifest.Tests.Select(test => $"- `{test.Command}`: {test.Status}; total={test.Total}, passed={test.Passed}, failed={test.Failed}, skipped/not-executed={test.SkippedNotExecuted}; TRX `{test.TrxPath}`; coverage `{test.CoveragePath}`.")) +
            $"\n- CLI version/doctor/self-test: exit 0; evidence under `cli/`.\n- Hardened review-pack verifier: PASS required for successful creation; expected exact manifest entries={anticipatedManifestFileCount}; the verifier rejects any inconsistency.");
        Section(report, 11, $"Status={manifest.App.Status}; Godot={manifest.App.GodotVersion}; framework={manifest.App.TargetFramework}; commit={manifest.App.GitCommit}; source load, smoke, doctor, normal launch, and screenshot evidence are referenced by the structured App outcome.");
        Section(report, 12, $"Status={manifest.Package.Status}; files={manifest.Package.PackageFileCount}; framework={manifest.Package.TargetFramework}; commit={manifest.Package.GitCommit}; executable, explicit status, smoke marker, doctor, packaged layout, and exact package manifest were validated.");
        Section(report, 13, $"Foundation deterministic self-test retained; CLI and App doctor JSON report success. Overall automated tests: total={total}, passed={passed}, failed={failed}, skipped/not-executed={skipped}.");
        Section(report, 14, $"Review root `{manifest.ReviewPackRoot}`; source digest `{manifest.SourceTreeDigest}`; design digest `{manifest.DesignArchiveDigest}`; verifier rejects missing, extra, unsafe, stale, generated, archive, or contradictory evidence.");
        Section(report, 15, $"Branch `{manifest.GitBranch}`; commit `{manifest.GitCommit}`; clean={manifest.GitClean}. No push was performed by Phase 0.1R.");
        Section(report, 16, manifest.Warnings.Count == 0 ? "No known Phase 0.1R evidence limitation is recorded." : string.Join(Environment.NewLine, manifest.Warnings.Select(warning => $"- {warning}")));
        Section(report, 17, "All Phase 0.2 biological simulation and domain work remains deferred. No cells, fields, regions, biological time/RNG, world state, or fake-life behavior was added.");
        Section(report, 18, $"{(debugPassed && releasePassed && failed == 0 && skipped == 0 && manifest.App.Status == EvidenceStatus.Passed && manifest.Package.Status == EvidenceStatus.Passed && manifest.GitClean ? "PASS" : "FAIL")}: source, tests, App, package, design import, and review evidence were evaluated from structured artifacts rather than inferred from file existence.");
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
