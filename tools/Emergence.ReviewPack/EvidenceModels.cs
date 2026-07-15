using System.Text.Json.Serialization;

namespace Emergence.ReviewPack;

[JsonConverter(typeof(JsonStringEnumConverter<EvidenceStatus>))]
public enum EvidenceStatus
{
    Passed,
    Failed,
    Incomplete,
    Missing,
    Blocked,
}

public sealed record TestEvidence(
    string Project,
    string Command,
    string Configuration,
    EvidenceStatus Status,
    int Total,
    int Executed,
    int Passed,
    int Failed,
    int SkippedNotExecuted,
    string TrxPath,
    string CoveragePath,
    string TrxSha256,
    string CoverageSha256,
    string Detail);

public sealed record AppEvidence(
    EvidenceStatus Status,
    string GodotVersion,
    string SourceLoadLog,
    string SmokeLog,
    string DoctorJson,
    string Screenshot,
    string ManualLaunchStatus,
    string TargetFramework,
    string GitCommit,
    string Detail);

public sealed record PackageEvidence(
    EvidenceStatus Status,
    string Executable,
    string StatusFile,
    string SmokeLog,
    string DoctorJson,
    string PackageManifest,
    string TargetFramework,
    string GitCommit,
    int PackageFileCount,
    string Detail);

public sealed record BuildOutcome(
    string Name,
    string Command,
    EvidenceStatus Status,
    string Configuration,
    string LogPath,
    int WarningCount,
    int ErrorCount,
    string ExpectedGitCommit,
    string Detail);

public sealed record BuildEvidence(
    BuildOutcome Restore,
    BuildOutcome Debug,
    BuildOutcome Release,
    BuildOutcome AssemblyInventory);

public sealed record CliCommandEvidence(
    string Name,
    string Command,
    EvidenceStatus Status,
    string DataPath,
    string LogPath,
    bool Success,
    string Version,
    string GitCommit,
    string TargetFramework,
    string Detail);

public sealed record CliEvidence(
    CliCommandEvidence Version,
    CliCommandEvidence Doctor,
    CliCommandEvidence Phase01SelfTest,
    CliCommandEvidence Phase02DomainSelfTest,
    CliCommandEvidence? RngSelfTest = null,
    CliCommandEvidence? RulesetValidation = null);

public sealed record RngEvidence(
    string Command,
    EvidenceStatus Status,
    string SeedFixture,
    string Domain,
    string Scope,
    string SampleIndex,
    string EncodedBytes,
    string PrimaryBlock,
    ulong Lane0,
    ulong BoundedResult,
    string DomainCatalogDigest,
    string AlgorithmCatalogDigest,
    IReadOnlyList<string> EvidencePaths,
    string Detail);

public sealed record RulesetEvidence(
    string Command,
    EvidenceStatus Status,
    string SourceDirectoryRole,
    int DiscoveredFileCount,
    int LoadedDescriptorCount,
    IReadOnlyList<string> Keys,
    string AlgorithmCatalogDigest,
    string DomainCatalogDigest,
    string ConfigurationDigest,
    string DescriptorDigest,
    string RegistryDigest,
    IReadOnlyList<string> EvidencePaths,
    string Detail);

public sealed record ReviewFileEntry(string Path, long Bytes, string Sha256);

public sealed record ReviewManifest(
    int SchemaVersion,
    string Project,
    string Phase,
    DateTime CreatedUtc,
    string RepositoryRoot,
    string ReviewPackRoot,
    string GitBranch,
    string GitCommit,
    bool GitClean,
    string SelectedDotnetSdk,
    IReadOnlyList<string> TargetFrameworks,
    string GodotVersion,
    string GodotExecutablePath,
    bool ExportTemplatesAvailable,
    string SourceTreeDigest,
    string DesignArchiveDigest,
    BuildEvidence Build,
    CliEvidence Cli,
    IReadOnlyList<TestEvidence> Tests,
    AppEvidence App,
    PackageEvidence Package,
    IReadOnlyList<ReviewFileEntry> Files,
    IReadOnlyList<string> Warnings,
    RngEvidence? Rng = null,
    RulesetEvidence? Rulesets = null);

public sealed record VerificationResult(
    bool Success,
    IReadOnlyList<string> Errors,
    int ManifestFileCount,
    int ActualFileCount,
    int TestTotal,
    int TestPassed,
    int PackageFileCount)
{
    public static VerificationResult Failure(params string[] errors) =>
        new(false, errors, 0, 0, 0, 0, 0);
}

public sealed record PackageFileEntry(string Path, long Length, string Sha256);
