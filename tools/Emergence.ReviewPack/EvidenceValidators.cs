using System.Text.Json;
using System.Text.RegularExpressions;

namespace Emergence.ReviewPack;

public static class AppEvidenceValidator
{
    public static AppEvidence Evaluate(
        string reviewRoot,
        string expectedCommit,
        string expectedFramework,
        string godotVersion,
        string expectedVersion = "0.4.0-dev",
        string expectedPhase = Phase04EvidenceValidator.CorrectionPhase,
        string shellMarker = "FOUNDATION / M0.4R")
    {
        string loadRelative = "app/load.log";
        string smokeRelative = "app/smoke.log";
        string doctorRelative = "app/doctor.json";
        string screenshotRelative = "app/shell-screenshot.png";
        string manualRelative = "app/manual-launch-status.txt";
        List<string> errors = [];

        string load = Path.Combine(reviewRoot, "app", "load.log");
        string smoke = Path.Combine(reviewRoot, "app", "smoke.log");
        string doctor = Path.Combine(reviewRoot, "app", "doctor.json");
        string screenshot = Path.Combine(reviewRoot, "app", "shell-screenshot.png");
        string manual = Path.Combine(reviewRoot, "app", "manual-launch-status.txt");
        string status = Path.Combine(reviewRoot, "app", "app-status.txt");

        if (!Regex.IsMatch(godotVersion, @"^4\.7\.stable\.mono\.", RegexOptions.CultureInvariant))
        {
            errors.Add($"Unsupported Godot version '{godotVersion}'.");
        }
        RequireText(load, text => !text.Contains("ERROR:", StringComparison.OrdinalIgnoreCase), "source load log", errors);
        RequireText(smoke, text => text.Contains("PROJECT_EMERGENCE_SMOKE_OK", StringComparison.Ordinal), "source smoke marker", errors);
        RequireText(status, text => text.StartsWith("PASSED:", StringComparison.Ordinal), "App passed status", errors);
        RequireText(manual, text => text.StartsWith("PASSED:", StringComparison.Ordinal) && text.Contains(shellMarker, StringComparison.Ordinal) && text.Contains("Paused", StringComparison.Ordinal) && text.Contains("no biological state", StringComparison.Ordinal) && (expectedPhase != Phase05EvidenceValidator.Phase || text.Contains("verified", StringComparison.OrdinalIgnoreCase) || text.Contains("loaded", StringComparison.OrdinalIgnoreCase)), "manual launch status", errors);
        if (!File.Exists(screenshot) || new FileInfo(screenshot).Length == 0)
        {
            errors.Add("Normal-shell screenshot is missing or empty.");
        }

        DoctorSummary doctorSummary = DoctorEvidence.Read(doctor, expectedCommit, expectedFramework, expectedVersion, requirePackagedLayout: false, expectedPhase);
        errors.AddRange(doctorSummary.Errors);
        EvidenceStatus evidenceStatus = errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed;
        return new AppEvidence(
            evidenceStatus,
            godotVersion,
            loadRelative,
            smokeRelative,
            doctorRelative,
            screenshotRelative,
            manualRelative,
            doctorSummary.TargetFramework,
            doctorSummary.GitCommit,
            errors.Count == 0 ? "Godot load, smoke, doctor, normal launch, and screenshot evidence passed." : string.Join(" ", errors));
    }

    private static void RequireText(string path, Func<string, bool> predicate, string description, List<string> errors)
    {
        if (!File.Exists(path))
        {
            errors.Add($"Missing {description}: {path}");
            return;
        }
        if (!predicate(File.ReadAllText(path)))
        {
            errors.Add($"Invalid {description}: {path}");
        }
    }
}

public static class PackageEvidenceValidator
{
    public static PackageEvidence Evaluate(
        string reviewRoot,
        string expectedCommit,
        string expectedFramework,
        string expectedVersion = "0.4.0-dev",
        string expectedPhase = Phase04EvidenceValidator.CorrectionPhase)
    {
        const string executableRelative = "package/windows-x86_64/ProjectEmergence.exe";
        const string statusRelative = "package/package-status.txt";
        const string smokeRelative = "package/packaged-smoke.log";
        const string doctorRelative = "package/packaged-doctor.json";
        const string manifestRelative = "package/package-manifest.json";
        string packageRoot = Path.Combine(reviewRoot, "package", "windows-x86_64");
        List<string> errors = [];

        string executable = Path.Combine(reviewRoot, executableRelative.Replace('/', Path.DirectorySeparatorChar));
        string statusFile = Path.Combine(reviewRoot, statusRelative.Replace('/', Path.DirectorySeparatorChar));
        string smoke = Path.Combine(reviewRoot, smokeRelative.Replace('/', Path.DirectorySeparatorChar));
        string doctor = Path.Combine(reviewRoot, doctorRelative.Replace('/', Path.DirectorySeparatorChar));
        string packageManifest = Path.Combine(reviewRoot, manifestRelative.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(executable))
        {
            errors.Add("Expected packaged executable is missing.");
        }
        if (!File.Exists(statusFile) || !File.ReadAllText(statusFile).StartsWith("PASSED:", StringComparison.Ordinal))
        {
            errors.Add("Package status is absent or not explicitly passed.");
        }
        if (!File.Exists(smoke) || !File.ReadAllText(smoke).Contains("PROJECT_EMERGENCE_SMOKE_OK", StringComparison.Ordinal))
        {
            errors.Add("Packaged smoke marker is missing.");
        }

        string ruleset = Path.Combine(packageRoot, "rulesets", "foundation-reference.ruleset.json");
        string[] rulesetFiles = Directory.Exists(packageRoot) ? Directory.EnumerateFiles(packageRoot, "*.ruleset.json", SearchOption.AllDirectories).ToArray() : [];
        if (rulesetFiles.Length != 1 || !string.Equals(rulesetFiles[0], ruleset, StringComparison.OrdinalIgnoreCase)) errors.Add("Package does not contain exactly one rulesets/foundation-reference.ruleset.json.");
        string sourceRuleset = Path.Combine(reviewRoot, "source", "rulesets", "foundation-reference.ruleset.json");
        if (!File.Exists(sourceRuleset) || !File.Exists(ruleset) || !string.Equals(EvidencePaths.HashFile(sourceRuleset), EvidencePaths.HashFile(ruleset), StringComparison.OrdinalIgnoreCase)) errors.Add("Packaged reference ruleset is not byte-equivalent to tracked source.");
        string managedRoot = Path.Combine(packageRoot, "data_Emergence.App_windows_x86_64");
        string[] requiredAssemblies = expectedPhase == Phase05EvidenceValidator.Phase
            ? ["Emergence.Model.dll", "Emergence.Simulation.dll", "Emergence.Persistence.dll", "Emergence.Presentation.Contracts.dll"]
            : ["Emergence.Model.dll", "Emergence.Simulation.dll", "Emergence.Presentation.Contracts.dll"];
        foreach (string assembly in requiredAssemblies)
            if (!File.Exists(Path.Combine(managedRoot, assembly))) errors.Add($"Required packaged assembly is missing: {assembly}.");
        DoctorSummary doctorSummary = DoctorEvidence.Read(doctor, expectedCommit, expectedFramework, expectedVersion, requirePackagedLayout: true, expectedPhase);
        errors.AddRange(doctorSummary.Errors);
        int packageFileCount = ValidatePackageManifest(packageRoot, packageManifest, errors);
        EvidenceStatus evidenceStatus = errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed;
        return new PackageEvidence(
            evidenceStatus,
            executableRelative,
            statusRelative,
            smokeRelative,
            doctorRelative,
            manifestRelative,
            doctorSummary.TargetFramework,
            doctorSummary.GitCommit,
            packageFileCount,
            errors.Count == 0 ? "Executable, smoke, doctor, packaged layout, commit, framework, and exact package manifest passed." : string.Join(" ", errors));
    }

    private static int ValidatePackageManifest(string packageRoot, string manifestPath, List<string> errors)
    {
        if (!Directory.Exists(packageRoot) || !File.Exists(manifestPath))
        {
            errors.Add("Package directory or package manifest is missing.");
            return 0;
        }

        List<PackageFileEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<PackageFileEntry>>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            errors.Add($"Package manifest is not parseable JSON: {exception.Message}");
            return 0;
        }

        if (entries is null)
        {
            errors.Add("Package manifest is empty.");
            return 0;
        }

        Dictionary<string, PackageFileEntry> listed = new(StringComparer.OrdinalIgnoreCase);
        foreach (PackageFileEntry entry in entries)
        {
            if (!EvidencePaths.IsSafeNormalizedRelativePath(entry.Path))
            {
                errors.Add($"Unsafe package-manifest path: '{entry.Path}'.");
                continue;
            }
            if (!listed.TryAdd(entry.Path, entry))
            {
                errors.Add($"Duplicate package-manifest path: '{entry.Path}'.");
            }
        }

        Dictionary<string, string> actual = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(packageRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(packageRoot, path).Replace('\\', '/');
            if (!actual.TryAdd(relative, path))
            {
                errors.Add($"Package directory contains a case-colliding duplicate path: '{relative}'.");
            }
        }
        foreach ((string relative, string path) in actual)
        {
            if (!listed.TryGetValue(relative, out PackageFileEntry? entry))
            {
                errors.Add($"Package manifest does not list package file: '{relative}'.");
                continue;
            }
            FileInfo info = new(path);
            if (info.Length != entry.Length || !string.Equals(EvidencePaths.HashFile(path), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Package manifest mismatch: '{relative}'.");
            }
        }
        foreach (string relative in listed.Keys.Where(relative => !actual.ContainsKey(relative)))
        {
            errors.Add($"Package manifest lists a missing file: '{relative}'.");
        }
        return actual.Count;
    }
}

internal sealed record DoctorSummary(string GitCommit, string TargetFramework, IReadOnlyList<string> Errors);

internal static class DoctorEvidence
{
    public static DoctorSummary Read(string path, string expectedCommit, string expectedFramework, string expectedVersion, bool requirePackagedLayout, string expectedPhase = Phase04EvidenceValidator.CorrectionPhase)
    {
        List<string> errors = [];
        if (!File.Exists(path))
        {
            return new DoctorSummary(string.Empty, string.Empty, [$"Doctor JSON is missing: {path}"]);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            bool success = root.TryGetProperty("success", out JsonElement successElement) && successElement.ValueKind == JsonValueKind.True;
            if (!success)
            {
                errors.Add("Doctor JSON does not report success=true.");
            }

            JsonElement build = root.TryGetProperty("build", out JsonElement buildElement) ? buildElement : default;
            string commit = build.ValueKind == JsonValueKind.Object && build.TryGetProperty("gitCommit", out JsonElement commitElement)
                ? commitElement.GetString() ?? string.Empty
                : string.Empty;
            string framework = build.ValueKind == JsonValueKind.Object && build.TryGetProperty("targetFramework", out JsonElement frameworkElement)
                ? frameworkElement.GetString() ?? string.Empty
                : string.Empty;
            string version = build.ValueKind == JsonValueKind.Object && build.TryGetProperty("semanticVersion", out JsonElement versionElement)
                ? versionElement.GetString() ?? string.Empty
                : string.Empty;
            if (!string.Equals(commit, expectedCommit, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Doctor Git commit '{commit}' does not match reviewed commit '{expectedCommit}'.");
            }
            if (!string.Equals(framework, expectedFramework, StringComparison.Ordinal))
            {
                errors.Add($"Doctor target framework '{framework}' does not match '{expectedFramework}'.");
            }
            if (!string.Equals(version, expectedVersion, StringComparison.Ordinal))
            {
                errors.Add($"Doctor semantic version '{version}' does not match '{expectedVersion}'.");
            }

            if (requirePackagedLayout)
            {
                bool packaged = root.TryGetProperty("checks", out JsonElement checks)
                    && checks.ValueKind == JsonValueKind.Array
                    && checks.EnumerateArray().Any(check =>
                        check.TryGetProperty("id", out JsonElement id)
                        && id.GetString() == "runtime.layout"
                        && check.TryGetProperty("detail", out JsonElement detail)
                        && detail.GetString() == "packaged");
                if (!packaged)
                {
                    errors.Add("Doctor JSON does not report packaged runtime layout.");
                }
            }
            string[] requiredChecks = ["process.architecture", "runtime.dotnet", "runtime.mode", "path.temp", "path.localAppData", "runtime.layout"];
            if (path.Contains($"{Path.DirectorySeparatorChar}app{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || path.Contains($"{Path.DirectorySeparatorChar}package{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                requiredChecks = [.. requiredChecks, "phase.identity", "runtime.godot", "ruleset.registry", "rng.algorithm", "rng.domains", "session.definition", "session.scheduler", "presentation.snapshot", "presentation.nonbiological", "presentation.no-mutation", "session.core-headless"];
            if (expectedPhase == Phase05EvidenceValidator.Phase
                && (path.Contains($"{Path.DirectorySeparatorChar}app{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || path.Contains($"{Path.DirectorySeparatorChar}package{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
                requiredChecks = [.. requiredChecks, "persistence.round-trip", "persistence.rng-continuation", "persistence.sidecars"];
            if (!root.TryGetProperty("checks", out JsonElement allChecks) || allChecks.ValueKind != JsonValueKind.Array)
            {
                errors.Add("Doctor JSON has no structured checks array.");
            }
            else
            {
                JsonElement[] checkArray = allChecks.EnumerateArray().ToArray();
                foreach (string id in requiredChecks)
                {
                    if (!checkArray.Any(check => check.TryGetProperty("id", out JsonElement checkId) && checkId.GetString() == id)) errors.Add($"Doctor JSON is missing required check '{id}'.");
                }
                JsonElement[] phaseChecks = checkArray.Where(check => check.TryGetProperty("id", out JsonElement checkId) && checkId.GetString() == "phase.identity").ToArray();
                if (phaseChecks.Length > 0 && (phaseChecks.Length != 1 || !phaseChecks[0].TryGetProperty("detail", out JsonElement phaseDetail) || phaseDetail.GetString() != expectedPhase)) errors.Add("Doctor JSON has stale phase identity.");
                if (checkArray.Any(check => check.TryGetProperty("severity", out JsonElement severity) && severity.GetString() == "Failure")) errors.Add("Doctor JSON contains a failed structured check.");
            }
            return new DoctorSummary(commit, framework, errors);
        }
        catch (JsonException exception)
        {
            return new DoctorSummary(string.Empty, string.Empty, [$"Doctor JSON is invalid: {exception.Message}"]);
        }
    }
}

public static class ReviewPackFilters
{
    private static readonly HashSet<string> ProhibitedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "TestResults", ".godot", ".nuget", "NuGet", "packages",
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".tar", ".tgz", ".gz", ".tpz",
    };

    public static bool IsProhibitedRelativePath(string relative)
    {
        string normalized = relative.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        if (segments.Any(ProhibitedSegments.Contains))
        {
            return true;
        }
        if (segments.Any(segment => Guid.TryParse(segment, out _)))
        {
            return true;
        }
        string extension = Path.GetExtension(normalized);
        if (extension.Equals(".emergence-world", StringComparison.OrdinalIgnoreCase)) return false;
        return ArchiveExtensions.Contains(extension);
    }

    public static void CopyFilteredTree(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            if (IsProhibitedRelativePath(relative)
                || relative.EndsWith("project.assets.json", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".nuget.g.props", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".nuget.g.targets", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".nuget.dgspec.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string target = Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
