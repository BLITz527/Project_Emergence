using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Emergence.ReviewPack;

public static class ReviewPackApplication
{
    private static readonly string[] RequiredTestProjects =
    [
        "Emergence.Foundation.Tests",
        "Emergence.Architecture.Tests",
        "Emergence.Cli.IntegrationTests",
        "Emergence.ReviewPack.Tests",
    ];

    public static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 2 && args[0] == "verify")
        {
            return Task.FromResult(Verify(Path.GetFullPath(args[1]), output, error));
        }
        if (args.Length == 3 && args[0] == "create")
        {
            return Task.FromResult(Create(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), output, error));
        }
        error.WriteLine("Usage: Emergence.ReviewPack create <repository-root> <output-root> | verify <manifest-path>");
        return Task.FromResult(2);
    }

    private static int Create(string repositoryRoot, string outputRoot, TextWriter output, TextWriter error)
    {
        if (!File.Exists(Path.Combine(repositoryRoot, "ProjectEmergence.slnx")))
        {
            error.WriteLine($"Not a Project Emergence repository: {repositoryRoot}");
            return 2;
        }
        string relativeOutput = Path.GetRelativePath(repositoryRoot, outputRoot);
        if (!relativeOutput.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relativeOutput))
        {
            error.WriteLine("Review-pack output must be outside the repository.");
            return 2;
        }

        GitResult branch = RunGit(repositoryRoot, "branch", "--show-current");
        GitResult head = RunGit(repositoryRoot, "rev-parse", "HEAD");
        GitResult status = RunGit(repositoryRoot, "status", "--short");
        if (branch.ExitCode != 0 || head.ExitCode != 0 || status.ExitCode != 0)
        {
            error.WriteLine("Git identity/state could not be read.");
            return 1;
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        string reviewRoot = Path.Combine(outputRoot, $"M0_P0.1R_{timestamp}");
        Directory.CreateDirectory(reviewRoot);
        foreach (string directory in new[] { "git", "environment", "source", "tests", "build", "cli", "app", "package", "docs" })
        {
            Directory.CreateDirectory(Path.Combine(reviewRoot, directory));
        }

        Write(Path.Combine(reviewRoot, "git", "branch.txt"), branch.Output);
        Write(Path.Combine(reviewRoot, "git", "head.txt"), head.Output);
        Write(Path.Combine(reviewRoot, "git", "status.txt"), status.Output);
        Write(Path.Combine(reviewRoot, "git", "log.txt"), RunGit(repositoryRoot, "log", "--decorate", "--oneline", "-20").Output);
        Write(Path.Combine(reviewRoot, "git", "diff-stat.txt"), RunGit(repositoryRoot, "diff", "--stat", "HEAD").Output);

        GitResult sourceList = RunGit(repositoryRoot, "ls-tree", "-r", "--name-only", "HEAD");
        if (sourceList.ExitCode != 0)
        {
            error.WriteLine("Could not enumerate the reviewed commit source tree.");
            return 1;
        }
        string[] sourceFiles = sourceList.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Write(Path.Combine(reviewRoot, "git", "tracked-files.txt"), string.Join(Environment.NewLine, sourceFiles));
        foreach (string relative in sourceFiles)
        {
            string source = Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                error.WriteLine($"Reviewed tracked file is absent from the clean working tree: {relative}");
                return 1;
            }
            string target = Path.Combine(reviewRoot, "source", relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
        }

        CopyEvidence(repositoryRoot, reviewRoot);
        CopyReviewDocuments(repositoryRoot, reviewRoot);
        (string godotVersion, string godotPath, bool templates) = ReadPreflight(reviewRoot);
        string gitCommit = OneLine(head.Output);
        IReadOnlyList<TestEvidence> tests = ReadTests(reviewRoot);
        AppEvidence app = AppEvidenceValidator.Evaluate(reviewRoot, gitCommit, ".NETCoreApp,Version=v10.0", godotVersion);
        PackageEvidence package = PackageEvidenceValidator.Evaluate(reviewRoot, gitCommit, ".NETCoreApp,Version=v10.0");
        string designDigest = ReadDesignDigest(repositoryRoot);
        string sourceDigest = EvidencePaths.DigestTree(Path.Combine(reviewRoot, "source"));
        List<string> warnings = [];
        if (!string.IsNullOrWhiteSpace(status.Output))
        {
            warnings.Add("The reviewed working tree was not clean.");
        }
        foreach (TestEvidence test in tests.Where(test => test.Status != EvidenceStatus.Passed))
        {
            warnings.Add($"Required test evidence is not passed: {test.Project} ({test.Status}).");
        }
        if (app.Status != EvidenceStatus.Passed)
        {
            warnings.Add($"App evidence is not passed: {app.Detail}");
        }
        if (package.Status != EvidenceStatus.Passed)
        {
            warnings.Add($"Package evidence is not passed: {package.Detail}");
        }
        if (string.IsNullOrWhiteSpace(designDigest))
        {
            warnings.Add("Imported design digest is missing.");
        }

        ReviewManifest seed = new(
            2,
            "Project Emergence",
            "M0 Phase 0.1R",
            DateTime.UtcNow,
            repositoryRoot,
            reviewRoot,
            OneLine(branch.Output),
            gitCommit,
            string.IsNullOrWhiteSpace(status.Output),
            OneLine(Run(repositoryRoot, "dotnet", "--version").Output),
            ["net10.0"],
            godotVersion,
            godotPath,
            templates,
            sourceDigest,
            designDigest,
            tests,
            app,
            package,
            [],
            warnings);

        Write(Path.Combine(reviewRoot, "README_REVIEW.md"), BuildReadme(seed));
        (IReadOnlyList<string> created, IReadOnlyList<string> modified) = CorrectionFiles(repositoryRoot);
        int anticipatedManifestFileCount = Directory.EnumerateFiles(reviewRoot, "*", SearchOption.AllDirectories).Count() + 1;
        Write(Path.Combine(reviewRoot, "IMPLEMENTATION_REPORT.md"), ImplementationReportBuilder.Build(seed, reviewRoot, created, modified, anticipatedManifestFileCount));

        string manifestPath = Path.Combine(reviewRoot, "MANIFEST.json");
        ReviewManifest manifest = seed with { Files = Inventory(reviewRoot, manifestPath) };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ReviewPackJson.Options) + Environment.NewLine, new UTF8Encoding(false));

        VerificationResult verification = ReviewPackVerifier.Verify(manifestPath);
        if (!verification.Success)
        {
            foreach (string message in verification.Errors)
            {
                error.WriteLine(message);
            }
            error.WriteLine($"Review-pack verification failed with {verification.Errors.Count} inconsistency(s): {reviewRoot}");
            return 1;
        }

        output.WriteLine($"Verified files={verification.ManifestFileCount}, tests={verification.TestPassed}/{verification.TestTotal}, packageFiles={verification.PackageFileCount}, extras=0, inconsistencies=0.");
        output.WriteLine(reviewRoot);
        return 0;
    }

    private static int Verify(string manifestPath, TextWriter output, TextWriter error)
    {
        VerificationResult result = ReviewPackVerifier.Verify(manifestPath);
        if (!result.Success)
        {
            foreach (string message in result.Errors)
            {
                error.WriteLine(message);
            }
            error.WriteLine($"Verification failed: inconsistencies={result.Errors.Count}.");
            return 1;
        }
        output.WriteLine($"Verified files={result.ManifestFileCount}, actualFiles={result.ActualFileCount}, tests={result.TestPassed}/{result.TestTotal}, packageFiles={result.PackageFileCount}, extras=0, inconsistencies=0.");
        return 0;
    }

    private static void CopyEvidence(string repositoryRoot, string reviewRoot)
    {
        (string Source, string Destination)[] mappings =
        [
            ("artifacts/preflight", "environment"),
            ("artifacts/tests", "tests"),
            ("artifacts/build", "build"),
            ("artifacts/cli", "cli"),
            ("artifacts/app", "app"),
            ("artifacts/package", "package"),
        ];
        foreach ((string source, string destination) in mappings)
        {
            ReviewPackFilters.CopyFilteredTree(
                Path.Combine(repositoryRoot, source.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(reviewRoot, destination.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    private static void CopyReviewDocuments(string repositoryRoot, string reviewRoot)
    {
        foreach (string directory in new[] { "architecture", "roadmap", "development", "design" })
        {
            ReviewPackFilters.CopyFilteredTree(
                Path.Combine(repositoryRoot, "docs", directory),
                Path.Combine(reviewRoot, "docs", directory));
        }
        foreach (string file in new[] { "phase-scope.md", "known-issues.md" })
        {
            string source = Path.Combine(repositoryRoot, "docs", file);
            string target = Path.Combine(reviewRoot, "docs", file);
            if (File.Exists(source))
            {
                File.Copy(source, target, true);
            }
        }
    }

    private static IReadOnlyList<TestEvidence> ReadTests(string reviewRoot)
    {
        List<TestEvidence> outcomes = [];
        foreach (string project in RequiredTestProjects)
        {
            string directory = Path.Combine(reviewRoot, "tests", project);
            string trxRelative = $"tests/{project}/{project}.trx";
            string coverageRelative = $"tests/{project}/coverage.cobertura.xml";
            string commandFile = Path.Combine(directory, "command.txt");
            string configurationFile = Path.Combine(directory, "configuration.txt");
            string command = File.Exists(commandFile) ? File.ReadAllText(commandFile).Trim() : $"dotnet test tests/{project}/{project}.csproj";
            string configuration = File.Exists(configurationFile) ? File.ReadAllText(configurationFile).Trim() : string.Empty;
            outcomes.Add(TrxEvidenceParser.Parse(
                project,
                command,
                configuration,
                Path.Combine(directory, $"{project}.trx"),
                Path.Combine(directory, "coverage.cobertura.xml"),
                trxRelative,
                coverageRelative));
        }
        return outcomes;
    }

    private static IReadOnlyList<ReviewFileEntry> Inventory(string reviewRoot, string manifestPath) =>
        Directory.EnumerateFiles(reviewRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, manifestPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(reviewRoot, path).Replace('\\', '/'), StringComparer.Ordinal)
            .Select(path => new ReviewFileEntry(
                Path.GetRelativePath(reviewRoot, path).Replace('\\', '/'),
                new FileInfo(path).Length,
                EvidencePaths.HashFile(path)))
            .ToArray();

    private static (string Version, string Path, bool Templates) ReadPreflight(string reviewRoot)
    {
        string path = Path.Combine(reviewRoot, "environment", "preflight.json");
        if (!File.Exists(path))
        {
            return ("unavailable", string.Empty, false);
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        string version = root.TryGetProperty("godotVersion", out JsonElement versionElement) ? versionElement.GetString() ?? "unavailable" : "unavailable";
        string executable = root.TryGetProperty("godotExecutable", out JsonElement pathElement) ? pathElement.GetString() ?? string.Empty : string.Empty;
        bool templates = root.TryGetProperty("windowsExportTemplatesAvailable", out JsonElement templateElement) && templateElement.GetBoolean();
        return (string.IsNullOrWhiteSpace(version) ? "unavailable" : version, executable, templates);
    }

    public static string ReadDesignDigest(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, "docs", "design", "v1.0", "IMPORTED_ARCHIVE_SHA256.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }

    private static string BuildReadme(ReviewManifest manifest) =>
        $"# Project Emergence M0 Phase 0.1R Review Pack\n\n" +
        $"Created UTC: {manifest.CreatedUtc:O}\n\n" +
        $"Reviewed commit: `{manifest.GitCommit}` on `{manifest.GitBranch}`; clean={manifest.GitClean}.\n\n" +
        $"Design archive SHA-256: `{manifest.DesignArchiveDigest}`.\n\n" +
        "This self-contained directory contains the exact reviewed source snapshot, normalized single-run tests and coverage, build/CLI/App/package evidence, required documentation, a structured manifest, and a complete implementation report. Successful creation requires hardened exact-file and semantic verification.\n";

    private static (IReadOnlyList<string> Created, IReadOnlyList<string> Modified) CorrectionFiles(string repositoryRoot)
    {
        GitResult result = RunGit(repositoryRoot, "diff-tree", "--no-commit-id", "--name-status", "-r", "HEAD");
        List<string> created = [];
        List<string> modified = [];
        foreach (string line in result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('\t', 2);
            if (parts.Length != 2)
            {
                continue;
            }
            if (parts[0].StartsWith('A')) created.Add(parts[1]);
            else modified.Add(parts[1]);
        }
        return (created, modified);
    }

    private static GitResult Run(string workingDirectory, string executable, params string[] arguments)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitResult(process.ExitCode, string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardOutput + standardError);
    }

    private static GitResult RunGit(string repositoryRoot, params string[] arguments)
    {
        string[] safeArguments = ["-c", $"safe.directory={repositoryRoot.Replace('\\', '/')}", .. arguments];
        return Run(repositoryRoot, "git", safeArguments);
    }

    private static string OneLine(string value) => value.Trim().Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    private static void Write(string path, string contents) => File.WriteAllText(path, contents.TrimEnd() + Environment.NewLine, new UTF8Encoding(false));
    private sealed record GitResult(int ExitCode, string Output);
}
