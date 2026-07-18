using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Emergence.ReviewPack;

public static class ReviewPackApplication
{
    private static readonly string[] RequiredTestProjects =
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

    public static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 3 && args[0] == "inventory")
        {
            return Task.FromResult(WriteAssemblyInventory(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), output, error));
        }
        if (args.Length == 2 && args[0] == "verify")
        {
            return Task.FromResult(Verify(Path.GetFullPath(args[1]), output, error));
        }
        if (args.Length == 3 && args[0] == "create")
        {
            return Task.FromResult(Create(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), output, error));
        }
        error.WriteLine("Usage: Emergence.ReviewPack create <repository-root> <output-root> | verify <manifest-path> | inventory <repository-root> <output-json>");
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
        string reviewRoot = Path.Combine(outputRoot, $"M0_P0.5_{timestamp}");
        Directory.CreateDirectory(reviewRoot);
        foreach (string directory in new[] { "git", "environment", "source", "tests", "build", "cli", "persistence", "app", "package", "docs" })
        {
            Directory.CreateDirectory(Path.Combine(reviewRoot, directory));
        }

        Write(Path.Combine(reviewRoot, "git", "branch.txt"), branch.Output);
        Write(Path.Combine(reviewRoot, "git", "head.txt"), head.Output);
        Write(Path.Combine(reviewRoot, "git", "status.txt"), status.Output);
        Write(Path.Combine(reviewRoot, "git", "log.txt"), RunGit(repositoryRoot, "log", "--decorate", "--oneline", "-20").Output);
        Write(Path.Combine(reviewRoot, "git", "diff-stat.txt"), RunGit(repositoryRoot, "diff", "--stat", "HEAD").Output);
        Dictionary<string, string> featureMetadata = new(StringComparer.Ordinal)
        {
            ["branch"] = OneLine(branch.Output),
            ["featureCommit"] = OneLine(head.Output),
            ["featureSubject"] = OneLine(RunGit(repositoryRoot, "show", "-s", "--format=%s", "HEAD").Output),
            ["featureParent"] = OneLine(RunGit(repositoryRoot, "rev-parse", "HEAD^").Output),
            ["acceptedMainCommit"] = "edb6f24898453841a4ecf3283bdd114ccebc2167",
            ["acceptedCorrectionCommit"] = "b2c6e61b2daac16e2ba0555d5f59c7d440c09cad",
            ["originalPhase04Commit"] = "903e15ca60b9d7ba2513ace3468cd7691ec2d660",
        };
        Write(Path.Combine(reviewRoot, "git", "feature-metadata.json"), JsonSerializer.Serialize(featureMetadata, new JsonSerializerOptions { WriteIndented = true }));

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
        ExtractPersistenceEvidence(reviewRoot);
        CopyReviewDocuments(repositoryRoot, reviewRoot);
        (string godotVersion, string godotPath, bool templates) = ReadPreflight(reviewRoot);
        string gitCommit = OneLine(head.Output);
        IReadOnlyList<TestEvidence> tests = ReadTests(reviewRoot);
        AppEvidence app = AppEvidenceValidator.Evaluate(reviewRoot, gitCommit, ".NETCoreApp,Version=v10.0", godotVersion, "0.5.0-dev", Phase05EvidenceValidator.Phase, "FOUNDATION / M0.5");
        PackageEvidence package = PackageEvidenceValidator.Evaluate(reviewRoot, gitCommit, ".NETCoreApp,Version=v10.0", "0.5.0-dev", Phase05EvidenceValidator.Phase);
        BuildEvidence build = BuildEvidenceValidator.Evaluate(reviewRoot, gitCommit, "0.5.0-dev", ".NETCoreApp,Version=v10.0");
        CliEvidence cli = CliEvidenceValidator.Evaluate(reviewRoot, gitCommit, "0.5.0-dev", ".NETCoreApp,Version=v10.0");
        (RngEvidence rng, RulesetEvidence rulesets) = Phase03EvidenceValidator.Evaluate(reviewRoot);
        SessionEvidence session = Phase04EvidenceValidator.Evaluate(reviewRoot, gitCommit, "0.5.0-dev", requirePresentation: false);
        PersistenceEvidence persistence = Phase05EvidenceValidator.Evaluate(reviewRoot);
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
        if (!BuildEvidenceValidator.IsPassed(build)) warnings.Add("Required build evidence is not passed.");
        if (!CliEvidenceValidator.IsPassed(cli)) warnings.Add("Required CLI evidence is not passed.");
        if (rng.Status != EvidenceStatus.Passed) warnings.Add($"Required RNG evidence is not passed: {rng.Detail}");
        if (rulesets.Status != EvidenceStatus.Passed) warnings.Add($"Required ruleset evidence is not passed: {rulesets.Detail}");
        if (session.Status != EvidenceStatus.Passed) warnings.Add($"Required Phase 0.4R regression evidence is not passed: {session.Detail}");
        if (persistence.Status != EvidenceStatus.Passed) warnings.Add($"Required Phase 0.5 persistence evidence is not passed: {persistence.Detail}");
        if (string.IsNullOrWhiteSpace(designDigest))
        {
            warnings.Add("Imported design digest is missing.");
        }

        ReviewManifest seed = new(
            6,
            "Project Emergence",
            Phase05EvidenceValidator.Phase,
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
            build,
            cli,
            tests,
            app,
            package,
            [],
            warnings,
            rng,
            rulesets,
            session,
            persistence);

        Write(Path.Combine(reviewRoot, "README_REVIEW.md"), BuildReadme(seed));
        (IReadOnlyList<string> created, IReadOnlyList<string> modified) = FeatureFiles(repositoryRoot);
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

    private static void ExtractPersistenceEvidence(string reviewRoot)
    {
        string packagePath = Path.Combine(reviewRoot, "cli", "foundation-session.emergence-world");
        if (!File.Exists(packagePath)) return;
        string destination = Path.Combine(reviewRoot, "persistence");
        Directory.CreateDirectory(destination);
        List<object> inventory = [];
        using FileStream stream = new(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, false, Encoding.UTF8);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.FullName is not ("definition.json" or "snapshot.json" or "package-manifest.json")) continue;
            using Stream input = entry.Open();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            byte[] content = bytes.ToArray();
            File.WriteAllBytes(Path.Combine(destination, entry.FullName), content);
            inventory.Add(new
            {
                path = entry.FullName,
                length = content.LongLength,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
            });
        }
        Write(Path.Combine(destination, "package-inventory.json"), JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyReviewDocuments(string repositoryRoot, string reviewRoot)
    {
        foreach (string directory in new[] { "architecture", "roadmap", "development", "design" })
        {
            ReviewPackFilters.CopyFilteredTree(
                Path.Combine(repositoryRoot, "docs", directory),
                Path.Combine(reviewRoot, "docs", directory));
        }
        foreach (string file in new[] { "phase-scope.md", "known-issues.md", "phase-0.2-traceability.md", "phase-0.3-traceability.md", "phase-0.4-traceability.md", "phase-0.5-traceability.md" })
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
        $"# Project Emergence M0 Phase 0.5 Review Pack\n\n" +
        $"Created UTC: {manifest.CreatedUtc:O}\n\n" +
        $"Reviewed commit: `{manifest.GitCommit}` on `{manifest.GitBranch}`; clean={manifest.GitClean}.\n\n" +
        $"Design archive SHA-256: `{manifest.DesignArchiveDigest}`.\n\n" +
        "This self-contained directory contains the exact reviewed source snapshot, normalized single-run tests and coverage, build/CLI/App/package evidence, an independently validated world-package fixture and extracted semantic documents, required documentation, a structured manifest, and a complete implementation report. Successful creation requires hardened exact-file and semantic verification.\n";

    private static int WriteAssemblyInventory(string repositoryRoot, string outputPath, TextWriter output, TextWriter error)
    {
        string[] projectRoots = [Path.Combine(repositoryRoot, "src"), Path.Combine(repositoryRoot, "tools")];
        List<AssemblyInventoryEntry> entries = [];
        foreach (string configuration in new[] { "Debug", "Release" })
        {
            foreach (string root in projectRoots.Where(Directory.Exists))
            {
                foreach (string path in Directory.EnumerateFiles(root, "Emergence.*.dll", SearchOption.AllDirectories)
                             .Where(path =>
                                 (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}net10.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                                  && string.Equals(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path))))!), Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase))
                                 || (Path.GetFileName(path).Equals("Emergence.App.dll", StringComparison.OrdinalIgnoreCase)
                                     && path.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}mono{Path.DirectorySeparatorChar}temp{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
                             .OrderBy(static path => path, StringComparer.Ordinal))
                {
                    try
                    {
                        string directory = Path.GetDirectoryName(path)!;
                        using InventoryLoadContext context = new(directory);
                        Assembly assembly = context.LoadFromAssemblyPath(path);
                        IList<CustomAttributeData> attributes = assembly.GetCustomAttributesData();
                        string informational = AttributeValue(attributes, "System.Reflection.AssemblyInformationalVersionAttribute");
                        string framework = AttributeValue(attributes, "System.Runtime.Versioning.TargetFrameworkAttribute");
                        string commit = attributes.Where(attribute => attribute.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute")
                            .FirstOrDefault(attribute => attribute.ConstructorArguments.Count == 2 && string.Equals(attribute.ConstructorArguments[0].Value as string, "GitCommit", StringComparison.Ordinal))?.ConstructorArguments[1].Value as string ?? "unknown";
                        entries.Add(new(Path.GetFileNameWithoutExtension(path), configuration, Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'), assembly.GetName().Version?.ToString() ?? "unknown", informational, commit, framework));
                    }
                    catch (Exception exception)
                    {
                        error.WriteLine($"Could not inventory {path}: {exception.Message}");
                        return 1;
                    }
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(entries, ReviewPackJson.Options) + Environment.NewLine, new UTF8Encoding(false));
        output.WriteLine($"Inventoried {entries.Count} assemblies: {outputPath}");
        return 0;
    }

    private static string AttributeValue(IList<CustomAttributeData> attributes, string fullName) =>
        attributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == fullName)?.ConstructorArguments.FirstOrDefault().Value as string ?? "unknown";

    private sealed class InventoryLoadContext(string directory) : AssemblyLoadContext(isCollectible: true), IDisposable
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string candidate = Path.Combine(directory, assemblyName.Name + ".dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }
        public void Dispose() => Unload();
    }

    private static (IReadOnlyList<string> Created, IReadOnlyList<string> Modified) FeatureFiles(string repositoryRoot)
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
