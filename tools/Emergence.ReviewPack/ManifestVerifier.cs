using System.Text.Json;

namespace Emergence.ReviewPack;

public static class ManifestIntegrityValidator
{
    public static VerificationResult Validate(string reviewRoot, ReviewManifest manifest)
    {
        List<string> errors = [];
        if (manifest.SchemaVersion != 5 || string.IsNullOrWhiteSpace(manifest.Project) || manifest.Phase != Phase04EvidenceValidator.CorrectionPhase)
        {
            errors.Add("Manifest schema header is invalid.");
        }

        Dictionary<string, ReviewFileEntry> listed = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReviewFileEntry entry in manifest.Files)
        {
            if (!EvidencePaths.IsSafeNormalizedRelativePath(entry.Path))
            {
                errors.Add($"Unsafe or non-normalized manifest path: '{entry.Path}'.");
                continue;
            }
            if (!listed.TryAdd(entry.Path, entry))
            {
                errors.Add($"Duplicate manifest path: '{entry.Path}'.");
                continue;
            }
            if (ReviewPackFilters.IsProhibitedRelativePath(entry.Path))
            {
                errors.Add($"Prohibited generated or archive path: '{entry.Path}'.");
            }
        }

        Dictionary<string, string> actual = new(StringComparer.OrdinalIgnoreCase);
        string manifestFile = Path.GetFullPath(Path.Combine(reviewRoot, "MANIFEST.json"));
        foreach (string path in Directory.EnumerateFiles(reviewRoot, "*", SearchOption.AllDirectories)
                     .Where(path => !string.Equals(Path.GetFullPath(path), manifestFile, StringComparison.OrdinalIgnoreCase)))
        {
            string relative = Path.GetRelativePath(reviewRoot, path).Replace('\\', '/');
            if (!actual.TryAdd(relative, path))
            {
                errors.Add($"Actual review pack contains case-colliding duplicate path: '{relative}'.");
            }
        }

        foreach ((string relative, string path) in actual)
        {
            if (ReviewPackFilters.IsProhibitedRelativePath(relative))
            {
                errors.Add($"Actual review pack contains prohibited generated or archive path: '{relative}'.");
            }
            if (!listed.TryGetValue(relative, out ReviewFileEntry? entry))
            {
                errors.Add($"Unlisted extra file: '{relative}'.");
                continue;
            }
            FileInfo info = new(path);
            if (info.Length != entry.Bytes || !string.Equals(EvidencePaths.HashFile(path), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Size or SHA-256 mismatch: '{relative}'.");
            }
        }
        foreach (string relative in listed.Keys.Where(relative => !actual.ContainsKey(relative)))
        {
            errors.Add($"Manifest lists a missing file: '{relative}'.");
        }

        string sourceRoot = Path.Combine(reviewRoot, "source");
        if (!Directory.Exists(sourceRoot))
        {
            errors.Add("Source snapshot directory is missing.");
        }
        else
        {
            string digest = EvidencePaths.DigestTree(sourceRoot);
            if (!string.Equals(digest, manifest.SourceTreeDigest, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Source-tree digest mismatch: manifest={manifest.SourceTreeDigest}, actual={digest}.");
            }
        }

        return new VerificationResult(
            errors.Count == 0,
            errors,
            manifest.Files.Count,
            actual.Count,
            manifest.Tests.Sum(test => test.Total),
            manifest.Tests.Sum(test => test.Passed),
            manifest.Package.PackageFileCount);
    }
}

public static class ReviewPackVerifier
{
    private const string ExpectedDesignDigest = "915f013f26955e1c614bb851a39b83c6966951ee94b73ac13a06167b2ff5fb6c";

    private static readonly string[] ExpectedTestProjects =
    [
        "Emergence.Foundation.Tests",
        "Emergence.Model.Tests",
        "Emergence.Simulation.Tests",
        "Emergence.Presentation.Contracts.Tests",
        "Emergence.Persistence.Tests",
        "Emergence.Architecture.Tests",
        "Emergence.Cli.IntegrationTests",
        "Emergence.ReviewPack.Tests",
    ];

    public static VerificationResult Verify(string manifestPath)
    {
        string fullManifest = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullManifest))
        {
            return VerificationResult.Failure($"Manifest is missing: {fullManifest}");
        }

        ReviewManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ReviewManifest>(
                File.ReadAllText(fullManifest),
                ReviewPackJson.Options);
        }
        catch (JsonException exception)
        {
            return VerificationResult.Failure($"Manifest JSON is invalid: {exception.Message}");
        }
        if (manifest is null)
        {
            return VerificationResult.Failure("Manifest JSON deserialized to null.");
        }

        string reviewRoot = Path.GetDirectoryName(fullManifest)!;
        VerificationResult integrity = ManifestIntegrityValidator.Validate(reviewRoot, manifest);
        List<string> errors = [.. integrity.Errors];
        ValidateRequiredDocuments(reviewRoot, errors);
        ValidateDesignDigest(reviewRoot, manifest, errors);
        ValidateSourceListing(reviewRoot, errors);
        ValidatePreflight(reviewRoot, manifest, errors);
        ValidateCorrectionMetadata(reviewRoot, manifest, errors);
        ValidateTests(reviewRoot, manifest, errors);

        BuildEvidence build = BuildEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, "0.4.0-dev", ".NETCoreApp,Version=v10.0");
        if (!BuildEvidenceValidator.IsPassed(build) || !BuildEvidenceValidator.IsPassed(manifest.Build))
        {
            errors.Add("Build evidence is not passed with zero warnings and errors.");
        }
        if (!Equivalent(build, manifest.Build)) errors.Add("Manifest build outcomes disagree with current build evidence.");

        CliEvidence cli = CliEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, "0.4.0-dev", ".NETCoreApp,Version=v10.0");
        if (!CliEvidenceValidator.IsPassed(cli) || !CliEvidenceValidator.IsPassed(manifest.Cli))
        {
            errors.Add("CLI version, doctor, Phase 0.1/0.2 self-tests, RNG self-test, ruleset validation, or session self-test evidence is not passed.");
        }
        if (!Equivalent(cli, manifest.Cli)) errors.Add("Manifest CLI outcomes disagree with current CLI evidence.");

        (RngEvidence rng, RulesetEvidence rulesets) = Phase03EvidenceValidator.Evaluate(reviewRoot);
        if (rng.Status != EvidenceStatus.Passed || manifest.Rng?.Status != EvidenceStatus.Passed || !Equivalent(rng, manifest.Rng)) errors.Add($"RNG evidence is not passed or disagrees with the manifest: {rng.Detail}");
        if (rulesets.Status != EvidenceStatus.Passed || manifest.Rulesets?.Status != EvidenceStatus.Passed || !Equivalent(rulesets, manifest.Rulesets)) errors.Add($"Ruleset evidence is not passed or disagrees with the manifest: {rulesets.Detail}");
        SessionEvidence session = Phase04EvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit);
        if (session.Status != EvidenceStatus.Passed || manifest.Session?.Status != EvidenceStatus.Passed || !Equivalent(session, manifest.Session)) errors.Add($"Phase 0.4R session evidence is not passed or disagrees with the manifest: {session.Detail}");

        AppEvidence app = AppEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, ".NETCoreApp,Version=v10.0", manifest.GodotVersion);
        if (app.Status != EvidenceStatus.Passed || manifest.App.Status != EvidenceStatus.Passed)
        {
            errors.Add($"App evidence is not passed: {app.Detail}");
        }
        if (!Equivalent(app, manifest.App))
        {
            errors.Add("Manifest App outcome disagrees with current App evidence.");
        }

        PackageEvidence package = PackageEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, ".NETCoreApp,Version=v10.0");
        if (package.Status != EvidenceStatus.Passed || manifest.Package.Status != EvidenceStatus.Passed)
        {
            errors.Add($"Package evidence is not passed: {package.Detail}");
        }
        if (!Equivalent(package, manifest.Package))
        {
            errors.Add("Manifest package outcome disagrees with current package evidence.");
        }

        if (!manifest.GitClean)
        {
            errors.Add("Manifest does not identify a clean reviewed working tree.");
        }
        if (manifest.Tests.Any(test => test.Status != EvidenceStatus.Passed))
        {
            errors.Add("One or more required test outcomes are not Passed.");
        }

        return integrity with
        {
            Success = errors.Count == 0,
            Errors = errors,
            TestTotal = manifest.Tests.Sum(test => test.Total),
            TestPassed = manifest.Tests.Sum(test => test.Passed),
            PackageFileCount = package.PackageFileCount,
        };
    }

    private static void ValidateCorrectionMetadata(string reviewRoot, ReviewManifest manifest, List<string> errors)
    {
        const string originalCommit = "903e15ca60b9d7ba2513ace3468cd7691ec2d660";
        const string acceptedMain = "5f21fc17abcc35843da09efa33a0c7c8abdd7d72";
        string path = Path.Combine(reviewRoot, "git", "correction-metadata.json");
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Dictionary<string, string> values = document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
            string[] required = ["branch", "correctionCommit", "correctionSubject", "correctionParent", "originalPhase04Commit", "originalPhase04Subject", "acceptedMainCommit"];
            if (!values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(required)) errors.Add("Correction commit metadata has missing or unexpected fields.");
            if (!values.TryGetValue("branch", out string? branch) || branch != "milestone-0-phase-0.4" || branch != manifest.GitBranch) errors.Add("Correction commit metadata has the wrong branch.");
            if (!values.TryGetValue("correctionCommit", out string? correction) || !correction.Equals(manifest.GitCommit, StringComparison.OrdinalIgnoreCase)) errors.Add("Correction commit metadata does not identify the reviewed commit.");
            if (!values.TryGetValue("correctionSubject", out string? correctionSubject) || correctionSubject != "M0 P0.4R harden scheduler transaction boundaries") errors.Add("Correction commit subject is wrong.");
            if (!values.TryGetValue("correctionParent", out string? correctionParent) || !correctionParent.Equals(originalCommit, StringComparison.OrdinalIgnoreCase)) errors.Add("Correction commit is not directly based on the reviewed Phase 0.4 commit.");
            if (!values.TryGetValue("originalPhase04Commit", out string? original) || !original.Equals(originalCommit, StringComparison.OrdinalIgnoreCase)) errors.Add("Original Phase 0.4 commit metadata is wrong.");
            if (!values.TryGetValue("originalPhase04Subject", out string? originalSubject) || originalSubject != "M0 P0.4 world session and deterministic scheduler") errors.Add("Original Phase 0.4 commit subject is wrong.");
            if (!values.TryGetValue("acceptedMainCommit", out string? main) || !main.Equals(acceptedMain, StringComparison.OrdinalIgnoreCase)) errors.Add("Accepted main commit metadata is wrong.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or ArgumentException)
        {
            errors.Add($"Correction commit metadata is invalid: {exception.Message}");
        }
    }

    private static void ValidateTests(string reviewRoot, ReviewManifest manifest, List<string> errors)
    {
        string[] recordedProjects = manifest.Tests.Select(test => test.Project).OrderBy(project => project, StringComparer.Ordinal).ToArray();
        string[] expectedProjects = ExpectedTestProjects.OrderBy(project => project, StringComparer.Ordinal).ToArray();
        if (!recordedProjects.SequenceEqual(expectedProjects, StringComparer.Ordinal))
        {
            errors.Add($"Manifest test-project set is incomplete or unexpected: {string.Join(", ", recordedProjects)}.");
        }
        foreach (TestEvidence recorded in manifest.Tests)
        {
            string trx;
            string coverage;
            try
            {
                trx = EvidencePaths.ResolveSafePath(reviewRoot, recorded.TrxPath);
                coverage = EvidencePaths.ResolveSafePath(reviewRoot, recorded.CoveragePath);
            }
            catch (InvalidDataException exception)
            {
                errors.Add(exception.Message);
                continue;
            }

            TestEvidence parsed = TrxEvidenceParser.Parse(
                recorded.Project,
                recorded.Command,
                recorded.Configuration,
                trx,
                coverage,
                recorded.TrxPath,
                recorded.CoveragePath);
            if (parsed.Status != recorded.Status
                || parsed.Total != recorded.Total
                || parsed.Executed != recorded.Executed
                || parsed.Passed != recorded.Passed
                || parsed.Failed != recorded.Failed
                || parsed.SkippedNotExecuted != recorded.SkippedNotExecuted
                || !string.Equals(parsed.TrxSha256, recorded.TrxSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parsed.CoverageSha256, recorded.CoverageSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Manifest test outcome disagrees with TRX/coverage evidence for {recorded.Project}.");
            }
        }
    }

    private static void ValidateDesignDigest(string reviewRoot, ReviewManifest manifest, List<string> errors)
    {
        string[] digestFiles =
        [
            Path.Combine(reviewRoot, "source", "docs", "design", "v1.0", "IMPORTED_ARCHIVE_SHA256.txt"),
            Path.Combine(reviewRoot, "docs", "design", "v1.0", "IMPORTED_ARCHIVE_SHA256.txt"),
        ];
        if (string.IsNullOrWhiteSpace(manifest.DesignArchiveDigest))
        {
            errors.Add("Manifest designArchiveDigest is empty.");
            return;
        }
        if (!string.Equals(manifest.DesignArchiveDigest, ExpectedDesignDigest, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Manifest designArchiveDigest does not match the authoritative Phase 0.1R digest {ExpectedDesignDigest}.");
        }
        foreach (string file in digestFiles)
        {
            if (!File.Exists(file))
            {
                errors.Add($"Imported design digest evidence is missing: {file}");
                continue;
            }
            string value = File.ReadAllText(file).Trim();
            if (!string.Equals(value, manifest.DesignArchiveDigest, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Imported design digest disagrees with manifest: {file}");
            }
        }
    }

    private static void ValidateSourceListing(string reviewRoot, List<string> errors)
    {
        string listing = Path.Combine(reviewRoot, "git", "tracked-files.txt");
        string source = Path.Combine(reviewRoot, "source");
        if (!File.Exists(listing) || !Directory.Exists(source))
        {
            errors.Add("Tracked-file listing or source snapshot is missing.");
            return;
        }
        string[] recorded = File.ReadAllLines(listing)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim().Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string[] actual = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(source, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (!recorded.SequenceEqual(actual, StringComparer.Ordinal))
        {
            errors.Add("Source snapshot paths disagree with git/tracked-files.txt.");
        }
    }

    private static void ValidatePreflight(string reviewRoot, ReviewManifest manifest, List<string> errors)
    {
        string preflight = Path.Combine(reviewRoot, "environment", "preflight.json");
        if (!File.Exists(preflight))
        {
            errors.Add("Preflight JSON is missing.");
            return;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(preflight));
            JsonElement root = document.RootElement;
            string version = root.TryGetProperty("godotVersion", out JsonElement versionElement) ? versionElement.GetString() ?? string.Empty : string.Empty;
            string executable = root.TryGetProperty("godotExecutable", out JsonElement executableElement) ? executableElement.GetString() ?? string.Empty : string.Empty;
            bool templates = root.TryGetProperty("windowsExportTemplatesAvailable", out JsonElement templatesElement) && templatesElement.ValueKind == JsonValueKind.True;
            if (!string.Equals(version, manifest.GodotVersion, StringComparison.Ordinal)
                || !string.Equals(executable, manifest.GodotExecutablePath, StringComparison.OrdinalIgnoreCase)
                || templates != manifest.ExportTemplatesAvailable
                || !templates)
            {
                errors.Add("Manifest Godot/toolchain claims disagree with preflight JSON.");
            }
        }
        catch (JsonException exception)
        {
            errors.Add($"Preflight JSON is invalid: {exception.Message}");
        }
    }

    private static void ValidateRequiredDocuments(string reviewRoot, List<string> errors)
    {
        string[] required =
        [
            "README_REVIEW.md",
            "IMPLEMENTATION_REPORT.md",
            "docs/phase-scope.md",
            "docs/known-issues.md",
            "docs/phase-0.2-traceability.md",
            "docs/phase-0.3-traceability.md",
            "docs/phase-0.4-traceability.md",
            "docs/design/README.md",
        ];
        foreach (string relative in required)
        {
            if (!File.Exists(Path.Combine(reviewRoot, relative.Replace('/', Path.DirectorySeparatorChar))))
            {
                errors.Add($"Required review document is missing: {relative}");
            }
        }
        foreach (string directory in new[] { "docs/architecture", "docs/roadmap", "docs/development" })
        {
            string full = Path.Combine(reviewRoot, directory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full) || !Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).Any())
            {
                errors.Add($"Required review documentation directory is missing or empty: {directory}");
            }
        }

        string report = Path.Combine(reviewRoot, "IMPLEMENTATION_REPORT.md");
        if (File.Exists(report))
        {
            string contents = File.ReadAllText(report);
            foreach (string heading in ImplementationReportBuilder.RequiredHeadings)
            {
                if (!contents.Contains($"## {heading}", StringComparison.Ordinal))
                {
                    errors.Add($"Implementation report is missing heading '{heading}'.");
                }
            }
        }
    }

    private static bool Equivalent(AppEvidence left, AppEvidence right) =>
        left.Status == right.Status
        && left.GodotVersion == right.GodotVersion
        && left.TargetFramework == right.TargetFramework
        && left.GitCommit.Equals(right.GitCommit, StringComparison.OrdinalIgnoreCase);

    private static bool Equivalent(PackageEvidence left, PackageEvidence right) =>
        left.Status == right.Status
        && left.TargetFramework == right.TargetFramework
        && left.GitCommit.Equals(right.GitCommit, StringComparison.OrdinalIgnoreCase)
        && left.PackageFileCount == right.PackageFileCount;

    private static bool Equivalent(BuildEvidence left, BuildEvidence right) =>
        new[] { left.Restore, left.Debug, left.Release, left.AssemblyInventory }
            .Zip(new[] { right.Restore, right.Debug, right.Release, right.AssemblyInventory })
            .All(pair => pair.First.Name == pair.Second.Name
                && pair.First.Command == pair.Second.Command
                && pair.First.Status == pair.Second.Status
                && pair.First.Configuration == pair.Second.Configuration
                && pair.First.LogPath == pair.Second.LogPath
                && pair.First.WarningCount == pair.Second.WarningCount
                && pair.First.ErrorCount == pair.Second.ErrorCount
                && pair.First.ExpectedGitCommit.Equals(pair.Second.ExpectedGitCommit, StringComparison.OrdinalIgnoreCase));

    private static bool Equivalent(CliEvidence left, CliEvidence right) =>
        new[] { left.Version, left.Doctor, left.Phase01SelfTest, left.Phase02DomainSelfTest, left.RngSelfTest, left.RulesetValidation, left.SessionSelfTest }
            .Zip(new[] { right.Version, right.Doctor, right.Phase01SelfTest, right.Phase02DomainSelfTest, right.RngSelfTest, right.RulesetValidation, right.SessionSelfTest })
            .All(pair => pair.First is not null && pair.Second is not null && pair.First.Name == pair.Second.Name
                && pair.First.Command == pair.Second.Command
                && pair.First.Status == pair.Second.Status
                && pair.First.DataPath == pair.Second.DataPath
                && pair.First.LogPath == pair.Second.LogPath
                && pair.First.Success == pair.Second.Success
                && pair.First.Version == pair.Second.Version
                && pair.First.GitCommit.Equals(pair.Second.GitCommit, StringComparison.OrdinalIgnoreCase)
                && pair.First.TargetFramework == pair.Second.TargetFramework);

    private static bool Equivalent(RngEvidence left, RngEvidence? right) => right is not null
        && left.Status == right.Status && left.SeedFixture == right.SeedFixture && left.Domain == right.Domain && left.Scope == right.Scope
        && left.SampleIndex == right.SampleIndex && left.EncodedBytes == right.EncodedBytes && left.PrimaryBlock == right.PrimaryBlock
        && left.Lane0 == right.Lane0 && left.BoundedResult == right.BoundedResult && left.DomainCatalogDigest == right.DomainCatalogDigest
        && left.AlgorithmCatalogDigest == right.AlgorithmCatalogDigest && left.EvidencePaths.SequenceEqual(right.EvidencePaths, StringComparer.Ordinal);

    private static bool Equivalent(RulesetEvidence left, RulesetEvidence? right) => right is not null
        && left.Status == right.Status && left.SourceDirectoryRole == right.SourceDirectoryRole && left.DiscoveredFileCount == right.DiscoveredFileCount
        && left.LoadedDescriptorCount == right.LoadedDescriptorCount && left.Keys.SequenceEqual(right.Keys, StringComparer.Ordinal)
        && left.AlgorithmCatalogDigest == right.AlgorithmCatalogDigest && left.DomainCatalogDigest == right.DomainCatalogDigest
        && left.ConfigurationDigest == right.ConfigurationDigest && left.DescriptorDigest == right.DescriptorDigest
        && left.RegistryDigest == right.RegistryDigest && left.EvidencePaths.SequenceEqual(right.EvidencePaths, StringComparer.Ordinal);

    private static bool Equivalent(SessionEvidence left, SessionEvidence? right) => right is not null
        && left.Status == right.Status && left.Phase == right.Phase && left.Version == right.Version
        && left.GitCommit.Equals(right.GitCommit, StringComparison.OrdinalIgnoreCase)
        && left.AlgorithmCatalogDigest == right.AlgorithmCatalogDigest && left.SchedulerGraphDigest == right.SchedulerGraphDigest
        && left.SessionDefinitionDigest == right.SessionDefinitionDigest && left.SessionTraceDigest == right.SessionTraceDigest
        && left.FinalStateDigest == right.FinalStateDigest && left.FinalTick == right.FinalTick
        && left.AcceptedCommandCount == right.AcceptedCommandCount && left.CommittedEventCount == right.CommittedEventCount
        && left.EventIds.SequenceEqual(right.EventIds, StringComparer.Ordinal)
        && left.PresentationSnapshotValid == right.PresentationSnapshotValid && left.AppSessionStatus == right.AppSessionStatus
        && left.EvidencePaths.SequenceEqual(right.EvidencePaths, StringComparer.Ordinal);
}

public static class ReviewPackJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter<EvidenceStatus>());
        return options;
    }
}
