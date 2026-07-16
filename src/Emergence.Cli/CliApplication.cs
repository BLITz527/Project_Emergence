using System.Globalization;
using System.Text.Json.Serialization;
using Emergence.Foundation;
using Emergence.Persistence.Rulesets;
using Emergence.Simulation;

namespace Emergence.Cli;

public static class CliApplication
{
    public static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteHelp(output);
            return Task.FromResult(args.Length == 0 ? 2 : 0);
        }

        return args[0] switch
        {
            "version" => Task.FromResult(Version(args, output, error)),
            "doctor" => Task.FromResult(Doctor(args, output, error)),
            "self-test" => Task.FromResult(SelfTest(args, output, error)),
            "domain-self-test" => Task.FromResult(DomainSelfTest(args, output, error)),
            "rng-self-test" => Task.FromResult(RngSelfTest(args, output, error)),
            "session-self-test" => Task.FromResult(SessionSelfTestCommand(args, output, error)),
            "ruleset" => Task.FromResult(Ruleset(args, output, error)),
            _ => Task.FromResult(Invalid(args[0], error)),
        };
    }

    private static int Version(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 1)
        {
            return UsageError("version does not accept arguments.", error);
        }

        BuildDetails build = BuildInfo.Current;
        output.WriteLine($"Product: {build.ProductName}");
        output.WriteLine($"Version: {build.SemanticVersion}");
        output.WriteLine($"Assembly version: {build.AssemblyVersion}");
        output.WriteLine($"Informational version: {build.InformationalVersion}");
        output.WriteLine($"Git commit: {build.GitCommit}");
        output.WriteLine($"Build configuration: {build.BuildConfiguration}");
        output.WriteLine($"Target framework: {build.TargetFramework}");
        output.WriteLine($"Runtime: {build.RuntimeVersion}");
        output.WriteLine($"Operating system: {build.OperatingSystem}");
        output.WriteLine($"Architecture: {build.Architecture}");
        return 0;
    }

    private static int Doctor(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryJsonPath(args, out string? path, out string? message))
        {
            return UsageError(message!, error);
        }

        DiagnosticReport report = RuntimeDiagnostics.Run("cli");
        return WriteReport(report, path, output, report.Success);
    }

    private static int SelfTest(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryJsonPath(args, out string? path, out string? message))
        {
            return UsageError(message!, error);
        }

        FoundationSelfTestReport report = FoundationSelfTest.Run();
        return WriteReport(report, path, output, report.Success);
    }

    private static int DomainSelfTest(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryJsonPath(args, out string? path, out string? message))
        {
            return UsageError(message!, error);
        }

        FoundationDomainSelfTestReport report = FoundationDomainSelfTest.Run();
        return WriteReport(report, path, output, report.Success);
    }

    private static int RngSelfTest(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryJsonPath(args, out string? path, out string? message)) return UsageError(message!, error);
        FoundationRngSelfTestReport report = FoundationRngSelfTest.Run();
        return WriteReport(report, path, output, report.Success);
    }

    private static int SessionSelfTestCommand(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryJsonPath(args, out string? path, out string? message)) return UsageError(message!, error);
        SessionSelfTestReport report = SessionSelfTest.Run();
        return WriteReport(report, path, output, report.Success);
    }

    private static int Ruleset(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is not (4 or 6) || args[1] != "validate" || args[2] != "--directory" || string.IsNullOrWhiteSpace(args[3])
            || (args.Length == 6 && (args[4] != "--json" || string.IsNullOrWhiteSpace(args[5]))))
            return UsageError("ruleset validate requires '--directory <path>' and optionally '--json <path>'.", error);
        string directory;
        try { directory = Path.GetFullPath(args[3]); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { directory = args[3]; }
        string? jsonPath = args.Length == 6 ? args[5] : null;
        RulesetDirectoryLoadResult load = new RulesetDirectoryLoader().Load(directory);
        Emergence.Foundation.Rulesets.RulesetDescriptor? descriptor = load.Registry?.Entries.Count == 1 ? load.Registry.Entries[0] : null;
        List<DiagnosticCheck> checks = load.Success
            ? [new("ruleset.registry", DiagnosticSeverity.Success, "Ruleset registry validation", $"{load.Registry!.Entries.Count} descriptor(s)")]
            : load.Issues.Select(issue => new DiagnosticCheck(issue.Code, DiagnosticSeverity.Failure, issue.FileName.Length == 0 ? "Ruleset validation" : issue.FileName, issue.Reason)).ToList();
        RulesetValidationReport report = new(
            load.Success, directory, load.DiscoveredFiles, load.Registry?.Entries.Count ?? 0,
            load.Registry?.Entries.Select(static x => x.Key.ToString()).ToArray() ?? [],
            descriptor?.Algorithms.Digest.ToString() ?? string.Empty,
            descriptor?.RngDomains.Digest.ToString() ?? string.Empty,
            descriptor?.Configuration.Digest.ToString() ?? string.Empty,
            descriptor?.Digest.ToString() ?? string.Empty,
            load.Registry?.Digest.ToString() ?? string.Empty,
            load.Issues, checks);
        return WriteReport(report, jsonPath, output, report.Success);
    }

    private static int WriteReport<T>(T report, string? path, TextWriter output, bool success)
    {
        string json = JsonDefaults.Serialize(report);
        if (path is null)
        {
            output.WriteLine(json);
        }
        else
        {
            JsonDefaults.WriteFile(path, report);
            output.WriteLine($"Wrote {Path.GetFullPath(path)}");
        }

        return success ? 0 : 1;
    }

    private static bool TryJsonPath(string[] args, out string? path, out string? message)
    {
        path = null;
        message = null;
        if (args.Length == 1)
        {
            return true;
        }

        if (args.Length == 3 && args[1] == "--json" && !string.IsNullOrWhiteSpace(args[2]))
        {
            path = args[2];
            return true;
        }

        message = $"{args[0]} accepts no arguments or '--json <path>'.";
        return false;
    }

    private static int Invalid(string command, TextWriter error) =>
        UsageError($"Unknown command '{command}'.", error);

    private static int UsageError(string message, TextWriter error)
    {
        error.WriteLine(message);
        WriteHelp(error);
        return 2;
    }

    private static bool IsHelp(string argument) => argument is "help" or "--help" or "-h";

    private static void WriteHelp(TextWriter writer)
    {
        writer.WriteLine("Project Emergence foundation CLI");
        writer.WriteLine("Usage: emergence <version|doctor|self-test|domain-self-test|rng-self-test|session-self-test> [--json <path>]");
        writer.WriteLine("       emergence ruleset validate --directory <path> [--json <path>]");
    }
}

public sealed record RulesetValidationReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] string Directory,
    [property: JsonPropertyOrder(2)] IReadOnlyList<string> DiscoveredFiles,
    [property: JsonPropertyOrder(3)] int LoadedRulesets,
    [property: JsonPropertyOrder(4)] IReadOnlyList<string> RulesetKeys,
    [property: JsonPropertyOrder(5)] string AlgorithmCatalogDigest,
    [property: JsonPropertyOrder(6)] string DomainCatalogDigest,
    [property: JsonPropertyOrder(7)] string ConfigurationDigest,
    [property: JsonPropertyOrder(8)] string DescriptorDigest,
    [property: JsonPropertyOrder(9)] string RegistryDigest,
    [property: JsonPropertyOrder(10)] IReadOnlyList<RulesetLoadIssue> Issues,
    [property: JsonPropertyOrder(11)] IReadOnlyList<DiagnosticCheck> Checks);
