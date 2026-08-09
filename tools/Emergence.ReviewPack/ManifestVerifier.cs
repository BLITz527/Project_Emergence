using System.Text.Json;
using System.Xml.Linq;

namespace Emergence.ReviewPack;

public static class ManifestIntegrityValidator
{
    public static VerificationResult Validate(string reviewRoot, ReviewManifest manifest)
    {
        List<string> errors = [];
        bool supportedHeader = (manifest.SchemaVersion == 5 && manifest.Phase == Phase04EvidenceValidator.CorrectionPhase)
            || (manifest.SchemaVersion == 6 && manifest.Phase == Phase05EvidenceValidator.Phase)
            || (manifest.SchemaVersion == 7 && manifest.Phase == Phase11EvidenceValidator.Phase);
        if (!supportedHeader || string.IsNullOrWhiteSpace(manifest.Project))
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
        bool phase11 = manifest.SchemaVersion == 7 && manifest.Phase == Phase11EvidenceValidator.Phase;
        bool phase05 = manifest.SchemaVersion == 6 && manifest.Phase == Phase05EvidenceValidator.Phase;
        if (!phase11 && !phase05) errors.Add("The review pack is not a supported Phase 0.5R schema-6 or Phase 1.1 schema-7 pack.");
        string expectedVersion = phase11 ? Phase11EvidenceValidator.Version : "0.5.0-dev";
        ValidateRequiredDocuments(reviewRoot, errors, phase11);
        ValidateDesignDigest(reviewRoot, manifest, errors);
        ValidateSourceListing(reviewRoot, errors);
        ValidatePreflight(reviewRoot, manifest, errors);
        ValidateFeatureMetadata(reviewRoot, manifest, errors);
        ValidateTests(reviewRoot, manifest, errors);

        BuildEvidence build = BuildEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, expectedVersion, ".NETCoreApp,Version=v10.0");
        if (!BuildEvidenceValidator.IsPassed(build) || !BuildEvidenceValidator.IsPassed(manifest.Build))
        {
            errors.Add("Build evidence is not passed with zero warnings and errors.");
        }
        if (!Equivalent(build, manifest.Build)) errors.Add("Manifest build outcomes disagree with current build evidence.");

        CliEvidence cli = CliEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, expectedVersion, ".NETCoreApp,Version=v10.0");
        if (!CliEvidenceValidator.IsPassed(cli) || !CliEvidenceValidator.IsPassed(manifest.Cli))
        {
            errors.Add("CLI version, doctor, Phase 0.1/0.2 self-tests, RNG self-test, ruleset validation, or session self-test evidence is not passed.");
        }
        if (!Equivalent(cli, manifest.Cli)) errors.Add("Manifest CLI outcomes disagree with current CLI evidence.");

        (RngEvidence rng, RulesetEvidence rulesets) = Phase03EvidenceValidator.Evaluate(reviewRoot);
        if (rng.Status != EvidenceStatus.Passed || manifest.Rng?.Status != EvidenceStatus.Passed || !Equivalent(rng, manifest.Rng)) errors.Add($"RNG evidence is not passed or disagrees with the manifest: {rng.Detail}");
        if (rulesets.Status != EvidenceStatus.Passed || manifest.Rulesets?.Status != EvidenceStatus.Passed || !Equivalent(rulesets, manifest.Rulesets)) errors.Add($"Ruleset evidence is not passed or disagrees with the manifest: {rulesets.Detail}");
        SessionEvidence session = Phase04EvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, expectedVersion, requirePresentation: false);
        if (session.Status != EvidenceStatus.Passed || manifest.Session?.Status != EvidenceStatus.Passed || !Equivalent(session, manifest.Session)) errors.Add($"Phase 0.4R regression evidence is not passed or disagrees with the manifest: {session.Detail}");
        PersistenceEvidence persistence = Phase05EvidenceValidator.Evaluate(reviewRoot);
        if (persistence.Status != EvidenceStatus.Passed || manifest.Persistence?.Status != EvidenceStatus.Passed || !Equivalent(persistence, manifest.Persistence)) errors.Add($"Phase 0.5R persistence evidence is not passed or disagrees with the manifest: {persistence.Detail}");
        if (phase11)
        {
            EnvironmentEvidence environment = Phase11EvidenceValidator.Evaluate(reviewRoot);
            if (environment.Status != EvidenceStatus.Passed || manifest.Environment?.Status != EvidenceStatus.Passed || !Equivalent(environment, manifest.Environment))
                errors.Add($"Phase 1.1 environment evidence is not passed or disagrees with the manifest: {environment.Detail}");
        }

        AppEvidence app = AppEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, ".NETCoreApp,Version=v10.0", manifest.GodotVersion,
            expectedVersion, phase11 ? Phase11EvidenceValidator.Phase : Phase05EvidenceValidator.Phase,
            phase11 ? Phase11EvidenceValidator.ShellMarker : "FOUNDATION / M0.5R");
        if (app.Status != EvidenceStatus.Passed || manifest.App.Status != EvidenceStatus.Passed)
        {
            errors.Add($"App evidence is not passed: {app.Detail}");
        }
        if (!Equivalent(app, manifest.App))
        {
            errors.Add("Manifest App outcome disagrees with current App evidence.");
        }

        PackageEvidence package = PackageEvidenceValidator.Evaluate(reviewRoot, manifest.GitCommit, ".NETCoreApp,Version=v10.0", expectedVersion,
            phase11 ? Phase11EvidenceValidator.Phase : Phase05EvidenceValidator.Phase);
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

    private static void ValidateFeatureMetadata(string reviewRoot, ReviewManifest manifest, List<string> errors)
    {
        if (manifest.SchemaVersion == 7 && manifest.Phase == Phase11EvidenceValidator.Phase)
        {
            ValidatePhase11FeatureMetadata(reviewRoot, manifest, errors);
            return;
        }
        const string acceptedMain = "edb6f24898453841a4ecf3283bdd114ccebc2167";
        const string originalPhase05 = "244bb8b5f6e0e2714ce1f7dec57c5d3bcb323f58";
        string path = Path.Combine(reviewRoot, "git", "feature-metadata.json");
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Dictionary<string, string> values = document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
            string[] required =
            [
                "branch", "correctionCommit", "correctionSubject", "correctionParent", "acceptedMainCommit",
                "originalPhase05Commit", "originalPhase05Subject", "originalPhase05Parent",
            ];
            if (!values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(required)) errors.Add("Correction commit metadata has missing or unexpected fields.");
            if (!values.TryGetValue("branch", out string? branch) || branch != "milestone-0-phase-0.5" || branch != manifest.GitBranch) errors.Add("Correction commit metadata has the wrong branch.");
            if (!values.TryGetValue("correctionCommit", out string? correction) || !correction.Equals(manifest.GitCommit, StringComparison.OrdinalIgnoreCase)) errors.Add("Correction metadata does not identify the reviewed commit.");
            if (!values.TryGetValue("correctionSubject", out string? correctionSubject) || correctionSubject != "M0 P0.5R make package locks crash recoverable") errors.Add("Correction commit subject is wrong.");
            if (!values.TryGetValue("correctionParent", out string? correctionParent) || !correctionParent.Equals(originalPhase05, StringComparison.OrdinalIgnoreCase)) errors.Add("Correction commit is not directly after the original Phase 0.5 commit.");
            if (!values.TryGetValue("acceptedMainCommit", out string? main) || !main.Equals(acceptedMain, StringComparison.OrdinalIgnoreCase)) errors.Add("Accepted main commit metadata is wrong.");
            if (!values.TryGetValue("originalPhase05Commit", out string? original) || !original.Equals(originalPhase05, StringComparison.OrdinalIgnoreCase)) errors.Add("Original Phase 0.5 commit metadata is wrong.");
            if (!values.TryGetValue("originalPhase05Subject", out string? originalSubject) || originalSubject != "M0 P0.5 coherent snapshots and atomic save load") errors.Add("Original Phase 0.5 commit subject is wrong.");
            if (!values.TryGetValue("originalPhase05Parent", out string? originalParent) || !originalParent.Equals(acceptedMain, StringComparison.OrdinalIgnoreCase)) errors.Add("Original Phase 0.5 commit is not directly based on accepted main.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or ArgumentException)
        {
            errors.Add($"Feature commit metadata is invalid: {exception.Message}");
        }
    }

    private static void ValidatePhase11FeatureMetadata(string reviewRoot, ReviewManifest manifest, List<string> errors)
    {
        const string baseline = "6d51b8f36930600e6b5662e039f2e6a6a3d8627d";
        string path = Path.Combine(reviewRoot, "git", "feature-metadata.json");
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Dictionary<string, string> values = document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
            string[] required = ["branch", "featureCommit", "featureSubject", "featureParent", "baselineCommit", "baselineSubject", "acceptedCorrectionCommit", "originalPhase05Commit"];
            if (!values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(required)) errors.Add("Phase 1.1 feature metadata has missing or unexpected fields.");
            if (values.GetValueOrDefault("branch") != "milestone-1-phase-1.1" || values.GetValueOrDefault("branch") != manifest.GitBranch) errors.Add("Phase 1.1 metadata has the wrong branch.");
            if (!values.GetValueOrDefault("featureCommit", "").Equals(manifest.GitCommit, StringComparison.OrdinalIgnoreCase)) errors.Add("Phase 1.1 metadata does not identify the reviewed commit.");
            if (values.GetValueOrDefault("featureSubject") != "M1 P1.1 region and environmental field lattice") errors.Add("Phase 1.1 feature commit subject is wrong.");
            if (!values.GetValueOrDefault("featureParent", "").Equals(baseline, StringComparison.OrdinalIgnoreCase)) errors.Add("Phase 1.1 feature commit is not directly based on the accepted main baseline.");
            if (!values.GetValueOrDefault("baselineCommit", "").Equals(baseline, StringComparison.OrdinalIgnoreCase)) errors.Add("Phase 1.1 baseline metadata is wrong.");
            if (!values.GetValueOrDefault("acceptedCorrectionCommit", "").Equals("ba98387b4f4f1b6cb85a773e2c2beb974a464653", StringComparison.OrdinalIgnoreCase)) errors.Add("Accepted correction ancestor metadata is wrong.");
            if (!values.GetValueOrDefault("originalPhase05Commit", "").Equals("244bb8b5f6e0e2714ce1f7dec57c5d3bcb323f58", StringComparison.OrdinalIgnoreCase)) errors.Add("Original Phase 0.5 ancestor metadata is wrong.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or ArgumentException)
        {
            errors.Add($"Phase 1.1 feature commit metadata is invalid: {exception.Message}");
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
        ValidateNamedLockTests(reviewRoot, errors);
    }

    private static void ValidateNamedLockTests(string reviewRoot, List<string> errors)
    {
        string trx = Path.Combine(
            reviewRoot,
            "tests",
            "Emergence.Persistence.Tests",
            "Emergence.Persistence.Tests.trx");
        try
        {
            XDocument document = XDocument.Load(trx, LoadOptions.None);
            string[] passed = document.Descendants()
                .Where(static element => element.Name.LocalName == "UnitTestResult"
                    && string.Equals((string?)element.Attribute("outcome"), "Passed", StringComparison.Ordinal))
                .Select(static element => (string?)element.Attribute("testName") ?? string.Empty)
                .ToArray();
            string[] required =
            [
                "StaleEmptyLockDoesNotBlockSave",
                "StaleArbitraryMetadataDoesNotBlockSave",
                "StaleEmptyLockDoesNotBlockRecoverWithValidTarget",
                "StaleTruncatedMetadataDoesNotBlockRecover",
                "StaleLockWithMissingTargetAppliesRecoveryCandidateTable",
                "StaleLockWithInvalidTargetQuarantinesAndUsesValidCandidate",
                "CrashLikeStaleLockDoesNotHideOrDeleteRecoveryEvidence",
                "ActiveExclusiveOwnerBlocksSaveWithoutMutatingPackageOrSidecars",
                "ActiveExclusiveOwnerBlocksRecoverWithoutMutatingPackageOrSidecars",
                "ReleasedOwnerWithRemainingLockPathAllowsSaveReacquisition",
                "ReleasedOwnerWithRemainingLockPathAllowsRecoverReacquisition",
                "CoordinatedAcquisitionsProduceExactlyOneOwnerWithoutTimingSleeps",
                "ReleasedLeaseCannotDeleteSuccessorLease",
                "CommittedSaveCleanupWarningPreservesSuccessIdentityManifestAndBytes",
                "SuccessfulRecoveryCleanupWarningPreservesRecoveredStatus",
            ];
            foreach (string name in required)
            {
                if (!passed.Any(test => test.Contains(name, StringComparison.Ordinal)))
                    errors.Add($"Persistence TRX is missing the required passed lock regression '{name}'.");
            }

            int stale = passed.Count(static test => test.Contains("Stale", StringComparison.Ordinal));
            int active = passed.Count(static test => test.Contains("ActiveExclusiveOwner", StringComparison.Ordinal));
            int cleanup = passed.Count(static test => test.Contains("CleanupWarning", StringComparison.Ordinal));
            if (stale < 9 || active < 2 || cleanup < 2)
                errors.Add($"Named lock regression semantic totals are incomplete: stale={stale}, active={active}, cleanup={cleanup}; required at least 9/2/2.");
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException or InvalidOperationException)
        {
            errors.Add($"Named lock regression evidence is invalid: {exception.Message}");
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

    private static void ValidateRequiredDocuments(string reviewRoot, List<string> errors, bool phase11)
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
            "docs/phase-0.5-traceability.md",
            "docs/design/README.md",
        ];
        if (phase11) required = [.. required, "docs/phase-1.1-traceability.md"];
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

    private static bool Equivalent(PersistenceEvidence left, PersistenceEvidence? right) => right is not null
        && left.Status == right.Status && left.AlgorithmCatalogDigest == right.AlgorithmCatalogDigest
        && left.CommandProcessorCatalogDigest == right.CommandProcessorCatalogDigest && left.DefinitionDigest == right.DefinitionDigest
        && left.PreSaveStateDigest == right.PreSaveStateDigest && left.SnapshotDigest == right.SnapshotDigest
        && left.PackageIdentityDigest == right.PackageIdentityDigest && left.ManifestDigest == right.ManifestDigest
        && left.PackageBytes == right.PackageBytes && left.PackageEntryCount == right.PackageEntryCount
        && left.LoadedStateDigest == right.LoadedStateDigest && left.RngContinuationMatched == right.RngContinuationMatched
        && left.NextCommandSequence == right.NextCommandSequence
        && left.ContinuationEventIds.SequenceEqual(right.ContinuationEventIds, StringComparer.Ordinal)
        && left.FinalStateDigest == right.FinalStateDigest && left.PersistenceTraceDigest == right.PersistenceTraceDigest
        && left.RecoveryScenarioCount == right.RecoveryScenarioCount && left.RecoveryScenarioPassed == right.RecoveryScenarioPassed
        && left.LockCheckCount == right.LockCheckCount && left.LockCheckPassed == right.LockCheckPassed
        && left.AppRoundTripValid == right.AppRoundTripValid && left.PackagedRoundTripValid == right.PackagedRoundTripValid
        && left.AppStaleLockValid == right.AppStaleLockValid && left.PackagedStaleLockValid == right.PackagedStaleLockValid
        && left.EvidencePaths.SequenceEqual(right.EvidencePaths, StringComparer.Ordinal);

    private static bool Equivalent(EnvironmentEvidence left, EnvironmentEvidence? right) => right is not null
        && left.Status == right.Status && left.FieldChannelCatalogDigest == right.FieldChannelCatalogDigest
        && left.RegionDefinitionDigest == right.RegionDefinitionDigest && left.RegionStateDigest == right.RegionStateDigest
        && left.EnvironmentDefinitionDigest == right.EnvironmentDefinitionDigest && left.EnvironmentStateDigest == right.EnvironmentStateDigest
        && left.AlgorithmCatalogDigest == right.AlgorithmCatalogDigest && left.SessionDefinitionDigest == right.SessionDefinitionDigest
        && left.SessionStateDigest == right.SessionStateDigest && left.SnapshotDigest == right.SnapshotDigest
        && left.PackageIdentityDigest == right.PackageIdentityDigest && left.ManifestDigest == right.ManifestDigest
        && left.SolidCellCount == right.SolidCellCount && left.FluidCellCount == right.FluidCellCount
        && left.ChannelTotals.SequenceEqual(right.ChannelTotals, StringComparer.Ordinal)
        && left.ChunkPaths.SequenceEqual(right.ChunkPaths, StringComparer.Ordinal) && left.ChunkCount == right.ChunkCount
        && left.SaveLoadMatched == right.SaveLoadMatched && left.StaticTickMatched == right.StaticTickMatched
        && left.IndependentReferenceMatched == right.IndependentReferenceMatched
        && left.NormalScreenshotPresent == right.NormalScreenshotPresent && left.RawGridScreenshotPresent == right.RawGridScreenshotPresent
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
