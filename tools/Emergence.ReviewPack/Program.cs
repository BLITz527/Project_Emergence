using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emergence.Foundation;

return await ReviewPackApplication.RunAsync(args);

internal static class ReviewPackApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 2 && args[0] == "verify")
        {
            return await VerifyAsync(args[1]);
        }

        if (args.Length != 3 || args[0] != "create")
        {
            Console.Error.WriteLine("Usage: Emergence.ReviewPack create <repository-root> <output-root> | verify <manifest-path>");
            return 2;
        }

        return await CreateAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
    }

    private static async Task<int> CreateAsync(string repositoryRoot, string outputRoot)
    {
        if (!File.Exists(Path.Combine(repositoryRoot, "ProjectEmergence.slnx")))
        {
            Console.Error.WriteLine($"Not a Project Emergence repository: {repositoryRoot}");
            return 2;
        }

        string relativeOutput = Path.GetRelativePath(repositoryRoot, outputRoot);
        if (!relativeOutput.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relativeOutput))
        {
            Console.Error.WriteLine("Review-pack output must be outside the repository.");
            return 2;
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        string reviewDirectory = Path.Combine(outputRoot, $"M0_P0.1_{timestamp}");
        Directory.CreateDirectory(reviewDirectory);
        string[] requiredDirectories =
        [
            "git", "environment", "source", "tests", "build", "cli", "app", "package", "docs",
        ];
        foreach (string directory in requiredDirectories)
        {
            Directory.CreateDirectory(Path.Combine(reviewDirectory, directory));
        }

        GitResult branch = RunGit(repositoryRoot, "branch", "--show-current");
        GitResult head = RunGit(repositoryRoot, "rev-parse", "HEAD");
        GitResult status = RunGit(repositoryRoot, "status", "--short");
        Write(Path.Combine(reviewDirectory, "git", "branch.txt"), branch.Output);
        Write(Path.Combine(reviewDirectory, "git", "head.txt"), head.Output);
        Write(Path.Combine(reviewDirectory, "git", "status.txt"), status.Output);
        Write(Path.Combine(reviewDirectory, "git", "log.txt"), RunGit(repositoryRoot, "log", "--decorate", "--oneline", "-20").Output);
        Write(Path.Combine(reviewDirectory, "git", "diff-stat.txt"), RunGit(repositoryRoot, "diff", "--stat", "HEAD").Output);

        GitResult filesResult = RunGit(repositoryRoot, "ls-files", "--cached", "--others", "--exclude-standard");
        string[] sourceFiles = filesResult.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Write(Path.Combine(reviewDirectory, "git", "tracked-files.txt"), string.Join(Environment.NewLine, sourceFiles));
        foreach (string relative in sourceFiles)
        {
            string source = Path.Combine(repositoryRoot, relative);
            if (!File.Exists(source))
            {
                continue;
            }

            string destination = Path.Combine(reviewDirectory, "source", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }

        CopyIfPresent(Path.Combine(repositoryRoot, "artifacts", "preflight"), Path.Combine(reviewDirectory, "environment"));
        CopyIfPresent(Path.Combine(repositoryRoot, "artifacts", "tests"), Path.Combine(reviewDirectory, "tests"));
        CopyIfPresent(Path.Combine(repositoryRoot, "artifacts", "build"), Path.Combine(reviewDirectory, "build"));
        CopyIfPresent(Path.Combine(repositoryRoot, "artifacts", "cli"), Path.Combine(reviewDirectory, "cli"));
        CopyIfPresent(Path.Combine(repositoryRoot, "artifacts", "app"), Path.Combine(reviewDirectory, "app"));
        CopyIfPresent(Path.Combine(repositoryRoot, "artifacts", "package"), Path.Combine(reviewDirectory, "package"));
        CopyIfPresent(Path.Combine(repositoryRoot, "docs", "architecture"), Path.Combine(reviewDirectory, "docs", "architecture"));
        CopyIfPresent(Path.Combine(repositoryRoot, "docs", "roadmap"), Path.Combine(reviewDirectory, "docs", "roadmap"));

        Write(Path.Combine(reviewDirectory, "README_REVIEW.md"),
            $"# Project Emergence M0 Phase 0.1 Review Pack\n\nCreated UTC: {DateTime.UtcNow:O}\n\nThis directory contains an exact identified source snapshot and available build, test, diagnostics, Godot, and package evidence. Missing evidence remains visible as a limitation in the manifest.\n");
        Write(Path.Combine(reviewDirectory, "IMPLEMENTATION_REPORT.md"),
            $"# Implementation report\n\nPhase: M0 P0.1\n\nGit branch: {OneLine(branch.Output)}\n\nGit commit: {OneLine(head.Output)}\n\nWorking tree clean: {string.IsNullOrWhiteSpace(status.Output)}\n\nGodot and packaging status are determined by the evidence captured under `app/` and `package/`.\n");

        string sourceDigest = DigestTree(Path.Combine(reviewDirectory, "source"));
        List<string> warnings = [];
        if (head.ExitCode != 0)
        {
            warnings.Add("No reviewed Git commit was available; source represents a dirty/unborn working tree.");
        }
        if (!File.Exists(Path.Combine(repositoryRoot, "design-input", "Project_Emergence_Design_v1.0.zip")))
        {
            warnings.Add("The Version 1.0 design archive was not supplied.");
        }
        string appStatus = Path.Combine(reviewDirectory, "app", "app-status.txt");
        if (!File.Exists(appStatus) || File.ReadAllText(appStatus).Contains("BLOCKED", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("No Godot runtime evidence was available.");
        }

        bool packagePresent = Directory.EnumerateFiles(Path.Combine(reviewDirectory, "package"), "ProjectEmergence.exe", SearchOption.AllDirectories).Any();
        (string godotVersion, string godotPath, bool templatesAvailable) = ReadPreflight(repositoryRoot);
        string selectedSdk = OneLine(Run(repositoryRoot, "dotnet", "--version").Output);
        ReviewManifest seed = new(
            "Project Emergence",
            "M0 Phase 0.1",
            DateTime.UtcNow,
            repositoryRoot,
            reviewDirectory,
            OneLine(branch.Output),
            head.ExitCode == 0 ? OneLine(head.Output) : "unborn",
            string.IsNullOrWhiteSpace(status.Output),
            selectedSdk,
            ["net10.0"],
            godotVersion,
            godotPath,
            templatesAvailable,
            sourceDigest,
            DesignDigest(repositoryRoot),
            TestOutcomes(reviewDirectory),
            packagePresent ? "evidence-present" : "blocked-or-not-produced",
            [],
            warnings);

        string manifestPath = Path.Combine(reviewDirectory, "MANIFEST.json");
        ReviewManifest manifest = seed with { Files = Inventory(reviewDirectory, manifestPath) };
        await File.WriteAllTextAsync(manifestPath, JsonDefaults.Serialize(manifest) + Environment.NewLine, new UTF8Encoding(false));
        Console.WriteLine(reviewDirectory);
        return 0;
    }

    private static async Task<int> VerifyAsync(string manifestPath)
    {
        string fullPath = Path.GetFullPath(manifestPath);
        ReviewManifest? manifest = JsonSerializer.Deserialize<ReviewManifest>(
            await File.ReadAllTextAsync(fullPath),
            JsonDefaults.Compact);
        if (manifest is null)
        {
            Console.Error.WriteLine("Manifest could not be parsed.");
            return 1;
        }

        string root = Path.GetDirectoryName(fullPath)!;
        foreach (FileEntry entry in manifest.Files)
        {
            string file = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                Console.Error.WriteLine($"Missing: {entry.Path}");
                return 1;
            }

            FileInfo info = new(file);
            if (info.Length != entry.Bytes || !string.Equals(Hash(file), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Mismatch: {entry.Path}");
                return 1;
            }
        }

        Console.WriteLine($"Verified {manifest.Files.Count} manifest entries.");
        return 0;
    }

    private static IReadOnlyList<FileEntry> Inventory(string root, string manifestPath) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, manifestPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new FileEntry(Path.GetRelativePath(root, path).Replace('\\', '/'), new FileInfo(path).Length, Hash(path)))
            .ToArray();

    private static IReadOnlyList<CommandOutcome> TestOutcomes(string reviewDirectory)
    {
        string tests = Path.Combine(reviewDirectory, "tests");
        string[] projects = ["Emergence.Foundation.Tests", "Emergence.Architecture.Tests", "Emergence.Cli.IntegrationTests"];
        return projects.Select(project => new CommandOutcome(
            $"dotnet test tests/{project}/{project}.csproj --configuration Release --collect 'XPlat Code Coverage'",
            File.Exists(Path.Combine(tests, project, $"{project}.trx")) ? "passed-evidence-present" : "evidence-missing"))
            .ToArray();
    }

    private static (string Version, string Path, bool Templates) ReadPreflight(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, "artifacts", "preflight", "preflight.json");
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

    private static string DesignDigest(string root)
    {
        string digestFile = Path.Combine(root, "docs", "design", "v1.0", "IMPORTED_ARCHIVE_SHA256.txt");
        return File.Exists(digestFile) ? File.ReadAllText(digestFile).Trim() : string.Empty;
    }

    private static string DigestTree(string root)
    {
        using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            digest.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file).Replace('\\', '/') + "\n"));
            digest.AppendData(File.ReadAllBytes(file));
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    private static void CopyIfPresent(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
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
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitResult(process.ExitCode, string.IsNullOrWhiteSpace(error) ? output : output + error);
    }

    private static GitResult RunGit(string repositoryRoot, params string[] arguments)
    {
        string[] safeArguments = ["-c", $"safe.directory={repositoryRoot.Replace('\\', '/')}", .. arguments];
        return Run(repositoryRoot, "git", safeArguments);
    }

    private static string Hash(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
    private static string OneLine(string value) => value.Trim().Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    private static void Write(string path, string contents) => File.WriteAllText(path, contents.TrimEnd() + Environment.NewLine, new UTF8Encoding(false));

    private sealed record GitResult(int ExitCode, string Output);
}

internal sealed record ReviewManifest(
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
    IReadOnlyList<CommandOutcome> TestCommands,
    string PackageStatus,
    IReadOnlyList<FileEntry> Files,
    IReadOnlyList<string> Warnings);

internal sealed record FileEntry(string Path, long Bytes, string Sha256);
internal sealed record CommandOutcome(string Command, string Outcome);
