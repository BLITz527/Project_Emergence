using System.Text;
using System.Text.Json;
using Emergence.ReviewPack;

namespace Emergence.ReviewPack.Tests;

public sealed class ReviewPackEvidenceTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string Framework = ".NETCoreApp,Version=v10.0";

    [Fact]
    public void PassingTrxReportsExactCounters()
    {
        using TemporaryDirectory fixture = new();
        string trx = fixture.PathOf("passing.trx");
        string coverage = fixture.Write("coverage.cobertura.xml", "<coverage />");
        WriteTrx(trx, "Completed", total: 7, executed: 7, passed: 7, failed: 0, notExecuted: 0);

        TestEvidence result = Parse(trx, coverage);

        Assert.True(result.Status == EvidenceStatus.Passed, result.Detail);
        Assert.Equal((7, 7, 7, 0, 0), (result.Total, result.Executed, result.Passed, result.Failed, result.SkippedNotExecuted));
    }

    [Fact]
    public void FailedTrxIsNeverPassed()
    {
        using TemporaryDirectory fixture = new();
        string trx = fixture.PathOf("failed.trx");
        string coverage = fixture.Write("coverage.cobertura.xml", "<coverage />");
        WriteTrx(trx, "Failed", total: 5, executed: 5, passed: 4, failed: 1, notExecuted: 0);

        TestEvidence result = Parse(trx, coverage);

        Assert.Equal(EvidenceStatus.Failed, result.Status);
        Assert.Equal(1, result.Failed);
    }

    [Theory]
    [InlineData("Aborted", 3, 2, 2, 0, 1, EvidenceStatus.Failed)]
    [InlineData("Completed", 3, 2, 2, 0, 1, EvidenceStatus.Incomplete)]
    public void AbortedOrNotExecutedTrxIsRepresentedHonestly(
        string outcome,
        int total,
        int executed,
        int passed,
        int failed,
        int notExecuted,
        EvidenceStatus expected)
    {
        using TemporaryDirectory fixture = new();
        string trx = fixture.PathOf("result.trx");
        string coverage = fixture.Write("coverage.cobertura.xml", "<coverage />");
        WriteTrx(trx, outcome, total, executed, passed, failed, notExecuted, outcome == "Aborted" ? 1 : 0);

        TestEvidence result = Parse(trx, coverage);

        Assert.Equal(expected, result.Status);
        Assert.Equal(notExecuted, result.SkippedNotExecuted);
    }

    [Fact]
    public void MissingTrxIsMissingNeverPassed()
    {
        using TemporaryDirectory fixture = new();
        TestEvidence result = Parse(fixture.PathOf("missing.trx"), fixture.PathOf("missing-coverage.xml"));
        Assert.Equal(EvidenceStatus.Missing, result.Status);
    }

    [Fact]
    public void ExeWithoutSmokeAndDoctorIsNotPassedPackage()
    {
        using TemporaryDirectory fixture = new();
        Directory.CreateDirectory(fixture.PathOf("package/windows-x86_64"));
        fixture.Write("package/windows-x86_64/ProjectEmergence.exe", "exe");

        PackageEvidence result = PackageEvidenceValidator.Evaluate(fixture.Root, Commit, Framework);

        Assert.NotEqual(EvidenceStatus.Passed, result.Status);
    }

    [Fact]
    public void DoctorSuccessFalseIsNotPassedPackage()
    {
        using TemporaryDirectory fixture = new();
        CreatePackageEvidence(fixture, doctorSuccess: false);

        PackageEvidence result = PackageEvidenceValidator.Evaluate(fixture.Root, Commit, Framework);

        Assert.Equal(EvidenceStatus.Failed, result.Status);
        Assert.Contains("success=true", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void CompletePackageEvidenceIsPassedWithExactManifest()
    {
        using TemporaryDirectory fixture = new();
        CreatePackageEvidence(fixture);

        PackageEvidence result = PackageEvidenceValidator.Evaluate(fixture.Root, Commit, Framework);

        Assert.True(result.Status == EvidenceStatus.Passed, result.Detail);
        Assert.Equal(5, result.PackageFileCount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void MissingOrMismatchedPackageManifestEntryFails(bool omitEntry, bool mismatchHash)
    {
        using TemporaryDirectory fixture = new();
        CreatePackageEvidence(fixture, omitManifestEntry: omitEntry, mismatchHash: mismatchHash);

        PackageEvidence result = PackageEvidenceValidator.Evaluate(fixture.Root, Commit, Framework);

        Assert.Equal(EvidenceStatus.Failed, result.Status);
        Assert.Contains("manifest", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestVerificationDetectsHashMismatch()
    {
        using TemporaryDirectory fixture = new();
        ReviewManifest manifest = CreateIntegrityManifest(fixture);
        ReviewFileEntry entry = manifest.Files[0] with { Sha256 = new string('0', 64) };

        VerificationResult result = ManifestIntegrityValidator.Validate(fixture.Root, manifest with { Files = [entry] });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("SHA-256 mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestVerificationDetectsUnlistedExtraFile()
    {
        using TemporaryDirectory fixture = new();
        ReviewManifest manifest = CreateIntegrityManifest(fixture);
        fixture.Write("extra.txt", "unexpected");

        VerificationResult result = ManifestIntegrityValidator.Validate(fixture.Root, manifest);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Unlisted extra", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestVerificationRejectsDuplicatePaths()
    {
        using TemporaryDirectory fixture = new();
        ReviewManifest manifest = CreateIntegrityManifest(fixture);
        ReviewFileEntry entry = manifest.Files[0];

        VerificationResult result = ManifestIntegrityValidator.Validate(fixture.Root, manifest with { Files = [entry, entry] });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Duplicate manifest path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("source\\backslash.txt")]
    public void ManifestVerificationRejectsUnsafePaths(string unsafePath)
    {
        using TemporaryDirectory fixture = new();
        ReviewManifest manifest = CreateIntegrityManifest(fixture);
        ReviewFileEntry unsafeEntry = new(unsafePath, 0, new string('0', 64));

        VerificationResult result = ManifestIntegrityValidator.Validate(fixture.Root, manifest with { Files = [unsafeEntry] });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Unsafe", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestVerificationDetectsSourceDigestMismatch()
    {
        using TemporaryDirectory fixture = new();
        ReviewManifest manifest = CreateIntegrityManifest(fixture) with { SourceTreeDigest = new string('f', 64) };

        VerificationResult result = ManifestIntegrityValidator.Validate(fixture.Root, manifest);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Source-tree digest mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void FilteredCopyExcludesGeneratedClutterAndArchives()
    {
        using TemporaryDirectory fixture = new();
        string source = fixture.PathOf("input");
        string destination = fixture.PathOf("output");
        Directory.CreateDirectory(source);
        Write(Path.Combine(source, "keep", "test.log"), "keep");
        Write(Path.Combine(source, "bin", "output.dll"), "drop");
        Write(Path.Combine(source, "obj", "project.assets.json"), "drop");
        Write(Path.Combine(source, "8f5fa9f6-7cb6-4c48-b387-00a0e1f44b08", "coverage.cobertura.xml"), "drop");
        Write(Path.Combine(source, "nested-review.zip"), "drop");

        ReviewPackFilters.CopyFilteredTree(source, destination);

        Assert.True(File.Exists(Path.Combine(destination, "keep", "test.log")));
        Assert.Single(Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void ImplementationReportContainsAllRequiredNumberedHeadings()
    {
        using TemporaryDirectory fixture = new();
        Directory.CreateDirectory(fixture.PathOf("source"));
        ReviewManifest manifest = EmptyManifest(fixture.Root) with
        {
            App = EmptyManifest(fixture.Root).App with { Status = EvidenceStatus.Passed },
            Package = EmptyManifest(fixture.Root).Package with { Status = EvidenceStatus.Passed },
        };

        string report = ImplementationReportBuilder.Build(manifest, fixture.Root);

        Assert.All(ImplementationReportBuilder.RequiredHeadings, heading => Assert.Contains($"## {heading}", report, StringComparison.Ordinal));
    }

    [Fact]
    public void ImportedDesignDigestIsCarriedIntoManifest()
    {
        using TemporaryDirectory fixture = new();
        const string digest = "915f013f26955e1c614bb851a39b83c6966951ee94b73ac13a06167b2ff5fb6c";
        fixture.Write("docs/design/v1.0/IMPORTED_ARCHIVE_SHA256.txt", digest + Environment.NewLine);

        string imported = ReviewPackApplication.ReadDesignDigest(fixture.Root);
        ReviewManifest manifest = EmptyManifest(fixture.Root) with { DesignArchiveDigest = imported };

        Assert.Equal(digest, manifest.DesignArchiveDigest);
        Assert.Contains(digest, JsonSerializer.Serialize(manifest, ReviewPackJson.Options), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLogWithFailureIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); fixture.Write("build/build-debug.log", "Build FAILED.\n0 Warning(s)\n1 Error(s)");
        Assert.Equal(EvidenceStatus.Failed, BuildEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Debug.Status);
    }

    [Fact]
    public void NonzeroBuildWarningIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); fixture.Write("build/build-debug.log", "Build succeeded.\n1 Warning(s)\n0 Error(s)");
        Assert.Equal(EvidenceStatus.Failed, BuildEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Debug.Status);
    }

    [Fact]
    public void MissingReleaseLogIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); File.Delete(fixture.PathOf("build/build-release.log"));
        Assert.Equal(EvidenceStatus.Failed, BuildEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Release.Status);
    }

    [Fact]
    public void AssemblyMetadataMismatchIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); string path = fixture.PathOf("build/assemblies.json"); fixture.Write("build/assemblies.json", File.ReadAllText(path).Replace(Commit, new string('f', 40), StringComparison.Ordinal));
        Assert.Equal(EvidenceStatus.Failed, BuildEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).AssemblyInventory.Status);
    }

    [Fact]
    public void CliDoctorSuccessFalseIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); fixture.Write("cli/doctor.json", DoctorJson(false));
        Assert.Equal(EvidenceStatus.Failed, CliEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Doctor.Status);
    }

    [Fact]
    public void Phase01VectorMismatchIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); fixture.Write("cli/self-test.json", "{\"success\":true,\"sha256\":\"bad\"}");
        Assert.Equal(EvidenceStatus.Failed, CliEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Phase01SelfTest.Status);
    }

    [Fact]
    public void Phase02CanonicalDigestMismatchIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); string path = fixture.PathOf("cli/domain-self-test.json"); fixture.Write("cli/domain-self-test.json", File.ReadAllText(path).Replace(CliEvidenceValidator.Phase02CanonicalDigest, new string('0', 64), StringComparison.Ordinal));
        Assert.Equal(EvidenceStatus.Failed, CliEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Phase02DomainSelfTest.Status);
    }

    [Fact]
    public void CliVersionMismatchIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); string path = fixture.PathOf("cli/version.txt"); fixture.Write("cli/version.txt", File.ReadAllText(path).Replace("0.4.0-dev", "0.1.0-dev", StringComparison.Ordinal));
        Assert.Equal(EvidenceStatus.Failed, CliEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Version.Status);
    }

    [Fact]
    public void ReviewedGitCommitMismatchIsRejected()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture); string path = fixture.PathOf("cli/version.txt"); fixture.Write("cli/version.txt", File.ReadAllText(path).Replace(Commit, new string('a', 40), StringComparison.Ordinal));
        Assert.Equal(EvidenceStatus.Failed, CliEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework).Version.Status);
    }

    [Fact]
    public void ValidBuildAndCliFixturesPass()
    {
        using TemporaryDirectory fixture = new(); CreateBuildAndCliEvidence(fixture);
        Assert.True(BuildEvidenceValidator.IsPassed(BuildEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework)));
        Assert.True(CliEvidenceValidator.IsPassed(CliEvidenceValidator.Evaluate(fixture.Root, Commit, "0.4.0-dev", Framework)));
    }

    private static TestEvidence Parse(string trx, string coverage) =>
        TrxEvidenceParser.Parse("Fixture.Tests", "dotnet test fixture", "Release", trx, coverage, "tests/fixture.trx", "tests/coverage.cobertura.xml");

    private static void WriteTrx(string path, string outcome, int total, int executed, int passed, int failed, int notExecuted, int aborted = 0)
    {
        Write(path, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <ResultSummary outcome="{outcome}">
                <Counters total="{total}" executed="{executed}" passed="{passed}" failed="{failed}" error="0" timeout="0" aborted="{aborted}" inconclusive="0" notExecuted="{notExecuted}" pending="0" disconnected="0" />
              </ResultSummary>
            </TestRun>
            """);
    }

    private static void CreateBuildAndCliEvidence(TemporaryDirectory fixture)
    {
        fixture.Write("build/restore.log", "Restore completed.");
        fixture.Write("build/build-debug.log", "Build succeeded.\n0 Warning(s)\n0 Error(s)");
        fixture.Write("build/build-release.log", "Build succeeded.\n0 Warning(s)\n0 Error(s)");
        string[] assemblies = ["Emergence.Foundation", "Emergence.Model", "Emergence.Simulation", "Emergence.Analytics", "Emergence.History", "Emergence.Persistence", "Emergence.Presentation.Contracts", "Emergence.Cli", "Emergence.App", "Emergence.ReviewPack"];
        AssemblyInventoryEntry[] inventory = assemblies.Select(name => new AssemblyInventoryEntry(name, "Release", $"src/{name}/bin/Release/net10.0/{name}.dll", "0.4.0.0", $"0.4.0-dev+{Commit}", Commit, Framework)).ToArray();
        fixture.Write("build/assemblies.json", JsonSerializer.Serialize(inventory, ReviewPackJson.Options));

        fixture.Write("cli/version.txt", $"Product: Project Emergence\nVersion: 0.4.0-dev\nGit commit: {Commit}\nTarget framework: {Framework}\n");
        fixture.Write("cli/doctor.log", "Wrote doctor.json");
        fixture.Write("cli/doctor.json", DoctorJson(true));
        fixture.Write("cli/self-test.log", "Wrote self-test.json");
        fixture.Write("cli/self-test.json", $$"""{"success":true,"sha256":"{{CliEvidenceValidator.Phase01Vector}}"}""");
        fixture.Write("cli/domain-self-test.log", "Wrote domain-self-test.json");
        fixture.Write("cli/domain-self-test.json", $$"""{"success":true,"canonicalDigest":"{{CliEvidenceValidator.Phase02CanonicalDigest}}","stableIdFixture":"{{CliEvidenceValidator.Phase02StableId}}","algorithmCatalogDigest":"{{CliEvidenceValidator.Phase02CatalogDigest}}","configurationDigest":"{{CliEvidenceValidator.Phase02ConfigurationDigest}}"}""");
        fixture.Write("cli/rng-self-test.log", "Wrote rng-self-test.json");
        fixture.Write("cli/rng-self-test.json", "{\"success\":true}");
        fixture.Write("cli/ruleset-validation.log", "Wrote ruleset-validation.json");
        fixture.Write("cli/ruleset-validation.json", "{\"success\":true}");
        fixture.Write("cli/session-self-test.log", "Wrote session-self-test.json");
        fixture.Write("cli/session-self-test.json", "{\"success\":true}");
    }

    private static void CreatePackageEvidence(TemporaryDirectory fixture, bool doctorSuccess = true, bool omitManifestEntry = false, bool mismatchHash = false)
    {
        string packageRoot = fixture.PathOf("package/windows-x86_64");
        Directory.CreateDirectory(packageRoot);
        string executable = fixture.Write("package/windows-x86_64/ProjectEmergence.exe", "exe");
        string ruleset = fixture.Write("package/windows-x86_64/rulesets/foundation-reference.ruleset.json", "ruleset");
        fixture.Write("source/rulesets/foundation-reference.ruleset.json", "ruleset");
        string model = fixture.Write("package/windows-x86_64/data_Emergence.App_windows_x86_64/Emergence.Model.dll", "model");
        string simulation = fixture.Write("package/windows-x86_64/data_Emergence.App_windows_x86_64/Emergence.Simulation.dll", "simulation");
        string presentation = fixture.Write("package/windows-x86_64/data_Emergence.App_windows_x86_64/Emergence.Presentation.Contracts.dll", "presentation");
        fixture.Write("package/package-status.txt", "PASSED: fixture\n");
        fixture.Write("package/packaged-smoke.log", "PROJECT_EMERGENCE_SMOKE_OK\n");
        fixture.Write("package/packaged-doctor.json", DoctorJson(doctorSuccess));
        List<PackageFileEntry> entries = [];
        if (!omitManifestEntry)
        {
            entries.Add(new PackageFileEntry(
                "ProjectEmergence.exe",
                new FileInfo(executable).Length,
                mismatchHash ? new string('0', 64) : EvidencePaths.HashFile(executable)));
            entries.Add(new PackageFileEntry("rulesets/foundation-reference.ruleset.json", new FileInfo(ruleset).Length, EvidencePaths.HashFile(ruleset)));
            entries.Add(new PackageFileEntry("data_Emergence.App_windows_x86_64/Emergence.Model.dll", new FileInfo(model).Length, EvidencePaths.HashFile(model)));
            entries.Add(new PackageFileEntry("data_Emergence.App_windows_x86_64/Emergence.Simulation.dll", new FileInfo(simulation).Length, EvidencePaths.HashFile(simulation)));
            entries.Add(new PackageFileEntry("data_Emergence.App_windows_x86_64/Emergence.Presentation.Contracts.dll", new FileInfo(presentation).Length, EvidencePaths.HashFile(presentation)));
        }
        fixture.Write("package/package-manifest.json", JsonSerializer.Serialize(entries, ReviewPackJson.Options));
    }

    private static string DoctorJson(bool success) => $$"""
        {
          "success": {{success.ToString().ToLowerInvariant()}},
          "build": { "semanticVersion": "0.4.0-dev", "gitCommit": "{{Commit}}", "targetFramework": "{{Framework}}" },
          "checks": [
            { "id": "process.architecture", "severity": "Success", "detail": "x64" },
            { "id": "runtime.dotnet", "severity": "Success", "detail": ".NET 10" },
            { "id": "runtime.mode", "severity": "Success", "detail": "fixture" },
            { "id": "path.temp", "severity": "Success", "detail": "temp" },
            { "id": "path.localAppData", "severity": "Success", "detail": "data" },
            { "id": "runtime.layout", "severity": "Success", "detail": "packaged" },
            { "id": "runtime.godot", "severity": "Success", "detail": "4.7" },
            { "id": "ruleset.registry", "severity": "Success", "detail": "count=1;digest={{Phase03EvidenceValidator.RegistryDigest}}" },
            { "id": "rng.algorithm", "severity": "Success", "detail": "{{Phase03EvidenceValidator.AlgorithmDigest}}" },
            { "id": "rng.domains", "severity": "Success", "detail": "{{Phase03EvidenceValidator.DomainDigest}}" },
            { "id": "session.definition", "severity": "Success", "detail": "{{Phase04EvidenceValidator.SessionDefinitionDigest}}" },
            { "id": "session.scheduler", "severity": "Success", "detail": "{{Phase04EvidenceValidator.SchedulerGraphDigest}}" },
            { "id": "presentation.snapshot", "severity": "Success", "detail": "world=0000000000000000000000000000002a;branch=00000000000000000000000000000007;tick=0;status=Paused;definition={{Phase04EvidenceValidator.SessionDefinitionDigest}};state={{new string('a', 64)}}" },
            { "id": "presentation.nonbiological", "severity": "Success", "detail": "hasBiologicalState=false" },
            { "id": "presentation.no-mutation", "severity": "Success", "detail": "before={{new string('b', 64)}};after={{new string('b', 64)}}" },
            { "id": "session.core-headless", "severity": "Success", "detail": "Emergence.Simulation" }
          ]
        }
        """;

    private static ReviewManifest CreateIntegrityManifest(TemporaryDirectory fixture)
    {
        string source = fixture.Write("source/a.txt", "source");
        ReviewFileEntry entry = new("source/a.txt", new FileInfo(source).Length, EvidencePaths.HashFile(source));
        return EmptyManifest(fixture.Root) with
        {
            SourceTreeDigest = EvidencePaths.DigestTree(fixture.PathOf("source")),
            Files = [entry],
        };
    }

    private static ReviewManifest EmptyManifest(string root) => new(
        5,
        "Project Emergence",
        "M0 Phase 0.4",
        DateTime.UnixEpoch,
        root,
        root,
        "main",
        Commit,
        true,
        "10.0.201",
        ["net10.0"],
        "4.7.stable.mono.official.fixture",
        "godot.exe",
        true,
        string.Empty,
        string.Empty,
        EmptyBuildEvidence(),
        EmptyCliEvidence(),
        [],
        new AppEvidence(EvidenceStatus.Missing, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
        new PackageEvidence(EvidenceStatus.Missing, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty),
        [],
        []);

    private static BuildEvidence EmptyBuildEvidence()
    {
        BuildOutcome outcome = new("fixture", "fixture", EvidenceStatus.Missing, string.Empty, string.Empty, 0, 0, Commit, string.Empty);
        return new(outcome, outcome, outcome, outcome);
    }

    private static CliEvidence EmptyCliEvidence()
    {
        CliCommandEvidence outcome = new("fixture", "fixture", EvidenceStatus.Missing, string.Empty, string.Empty, false, string.Empty, string.Empty, string.Empty, string.Empty);
        return new(outcome, outcome, outcome, outcome, outcome, outcome);
    }

    private static void Write(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), $"emergence-review-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        public string Write(string relative, string contents)
        {
            string path = PathOf(relative);
            ReviewPackEvidenceTests.Write(path, contents);
            return path;
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
