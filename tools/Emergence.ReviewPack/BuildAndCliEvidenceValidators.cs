using System.Text.Json;
using System.Text.RegularExpressions;

namespace Emergence.ReviewPack;

public static class BuildEvidenceValidator
{
    private static readonly string[] RequiredAssemblies =
    [
        "Emergence.Foundation", "Emergence.Model", "Emergence.Simulation", "Emergence.Analytics", "Emergence.History",
        "Emergence.Persistence", "Emergence.Presentation.Contracts", "Emergence.Cli", "Emergence.App", "Emergence.ReviewPack",
    ];

    public static BuildEvidence Evaluate(string reviewRoot, string expectedCommit, string expectedVersion, string expectedFramework)
    {
        BuildOutcome restore = ReadBuildLog(reviewRoot, "restore", "dotnet restore ProjectEmergence.slnx", string.Empty, "build/restore.log", expectedCommit, requireSummary: false);
        BuildOutcome debug = ReadBuildLog(reviewRoot, "debug", "dotnet build ProjectEmergence.slnx --configuration Debug --no-restore", "Debug", "build/build-debug.log", expectedCommit, requireSummary: true);
        BuildOutcome release = ReadBuildLog(reviewRoot, "release", "dotnet build ProjectEmergence.slnx --configuration Release --no-restore", "Release", "build/build-release.log", expectedCommit, requireSummary: true);
        BuildOutcome inventory = ReadInventory(reviewRoot, expectedCommit, expectedVersion, expectedFramework);
        return new(restore, debug, release, inventory);
    }

    public static bool IsPassed(BuildEvidence evidence) =>
        new[] { evidence.Restore, evidence.Debug, evidence.Release, evidence.AssemblyInventory }.All(static outcome => outcome.Status == EvidenceStatus.Passed && outcome.WarningCount == 0 && outcome.ErrorCount == 0);

    private static BuildOutcome ReadBuildLog(string root, string name, string command, string configuration, string relative, string expectedCommit, bool requireSummary)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        List<string> errors = [];
        int warnings = 0;
        int errorCount = 0;
        if (!File.Exists(path)) errors.Add($"Missing {name} build log.");
        else
        {
            string text = File.ReadAllText(path);
            if (text.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase) || text.Contains(": error ", StringComparison.OrdinalIgnoreCase)) errors.Add($"{name} log reports failure.");
            Match warning = Regex.Match(text, @"(?m)^\s*(\d+) Warning\(s\)\s*$", RegexOptions.CultureInvariant);
            Match failure = Regex.Match(text, @"(?m)^\s*(\d+) Error\(s\)\s*$", RegexOptions.CultureInvariant);
            if (requireSummary && (!warning.Success || !failure.Success)) errors.Add($"{name} log lacks an exact warning/error summary.");
            warnings = warning.Success ? int.Parse(warning.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            errorCount = failure.Success ? int.Parse(failure.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            if (warnings != 0) errors.Add($"{name} build has {warnings} warning(s).");
            if (errorCount != 0) errors.Add($"{name} build has {errorCount} error(s).");
        }
        return new(name, command, errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed, configuration, relative, warnings, errorCount, expectedCommit, errors.Count == 0 ? $"{name} outcome passed with zero warnings and errors." : string.Join(" ", errors));
    }

    private static BuildOutcome ReadInventory(string root, string expectedCommit, string expectedVersion, string expectedFramework)
    {
        const string relative = "build/assemblies.json";
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        List<string> errors = [];
        AssemblyInventoryEntry[] entries = [];
        try { if (File.Exists(path)) entries = JsonSerializer.Deserialize<AssemblyInventoryEntry[]>(File.ReadAllText(path), ReviewPackJson.Options) ?? []; else errors.Add("Assembly inventory is missing."); }
        catch (JsonException exception) { errors.Add($"Assembly inventory is invalid: {exception.Message}"); }
        foreach (string assembly in RequiredAssemblies)
        {
            AssemblyInventoryEntry? entry = entries.FirstOrDefault(item => item.Assembly == assembly && item.Configuration == "Release");
            if (entry is null) { errors.Add($"Release assembly is missing from inventory: {assembly}."); continue; }
            if (entry.AssemblyVersion != "0.4.0.0" || !entry.InformationalVersion.StartsWith(expectedVersion + "+", StringComparison.Ordinal)
                || !entry.GitCommit.Equals(expectedCommit, StringComparison.OrdinalIgnoreCase) || entry.TargetFramework != expectedFramework)
            {
                errors.Add($"Assembly metadata mismatch: {assembly}.");
            }
        }
        return new("assembly-inventory", "Emergence.ReviewPack inventory", errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed, "Debug+Release", relative, 0, errors.Count, expectedCommit, errors.Count == 0 ? "Required Release assemblies report the reviewed commit, version, and framework." : string.Join(" ", errors));
    }
}

public sealed record AssemblyInventoryEntry(string Assembly, string Configuration, string Path, string AssemblyVersion, string InformationalVersion, string GitCommit, string TargetFramework);

public static class CliEvidenceValidator
{
    public const string Phase01Vector = "f4fd4d01fc3f3e82b74c69622c8fed9a8a87bc02ec6ce2f9f18127aec7544ce1";
    public const string Phase02CanonicalDigest = "82c8ccdd15e3c521c298553e1fc02360048f5a115ad96dc6d82802e244a7c370";
    public const string Phase02StableId = "0123456789abcdeffedcba9876543210";
    public const string Phase02CatalogDigest = "a8d497cee1881fe786f414ebd2a944c2da4ccb9433430feef675b1aeb17fd6dc";
    public const string Phase02ConfigurationDigest = "75b8257ce1bbcf5599165648ea4601e64029afb562667639e271dfde14bc2cb5";

    public static CliEvidence Evaluate(string reviewRoot, string expectedCommit, string expectedVersion, string expectedFramework)
    {
        CliCommandEvidence version = ReadVersion(reviewRoot, expectedCommit, expectedVersion, expectedFramework);
        CliCommandEvidence doctor = ReadDoctor(reviewRoot, expectedCommit, expectedVersion, expectedFramework);
        CliCommandEvidence phase01 = ReadSelfTest(reviewRoot, "self-test", "cli/self-test.json", "cli/self-test.log", "sha256", Phase01Vector);
        CliCommandEvidence phase02 = ReadDomainSelfTest(reviewRoot);
        CliCommandEvidence rng = ReadSuccessReport(reviewRoot, "rng-self-test", "cli/rng-self-test.json", "cli/rng-self-test.log");
        CliCommandEvidence rulesets = ReadSuccessReport(reviewRoot, "ruleset-validation", "cli/ruleset-validation.json", "cli/ruleset-validation.log");
        CliCommandEvidence session = ReadSuccessReport(reviewRoot, "session-self-test", "cli/session-self-test.json", "cli/session-self-test.log");
        return new(version, doctor, phase01, phase02, rng, rulesets, session);
    }

    public static bool IsPassed(CliEvidence evidence) =>
        new[] { evidence.Version, evidence.Doctor, evidence.Phase01SelfTest, evidence.Phase02DomainSelfTest, evidence.RngSelfTest, evidence.RulesetValidation, evidence.SessionSelfTest }.All(static outcome => outcome is not null && outcome.Status == EvidenceStatus.Passed && outcome.Success);

    private static CliCommandEvidence ReadSuccessReport(string root, string name, string data, string log)
    {
        List<string> errors = []; string path = Resolve(root, data);
        try { using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)); if (!document.RootElement.TryGetProperty("success", out JsonElement success) || success.ValueKind != JsonValueKind.True) errors.Add($"CLI {name} does not report success=true."); }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException) { errors.Add($"CLI {name} is invalid: {exception.Message}"); }
        if (!File.Exists(Resolve(root, log))) errors.Add($"CLI {name} log is missing.");
        return Command(name, $"emergence {name} --json", data, log, errors, string.Empty, string.Empty, string.Empty);
    }

    private static CliCommandEvidence ReadVersion(string root, string commit, string version, string framework)
    {
        const string relative = "cli/version.txt"; string path = Resolve(root, relative); List<string> errors = []; Dictionary<string, string> fields = new(StringComparer.Ordinal);
        if (!File.Exists(path)) errors.Add("CLI version evidence is missing.");
        else foreach (string line in File.ReadAllLines(path)) { int separator = line.IndexOf(": ", StringComparison.Ordinal); if (separator > 0) fields[line[..separator]] = line[(separator + 2)..]; }
        Require(fields, "Version", version, errors); Require(fields, "Git commit", commit, errors); Require(fields, "Target framework", framework, errors);
        return Command("version", "emergence version", relative, relative, errors, fields.GetValueOrDefault("Version", ""), fields.GetValueOrDefault("Git commit", ""), fields.GetValueOrDefault("Target framework", ""));
    }

    private static CliCommandEvidence ReadDoctor(string root, string commit, string version, string framework)
    {
        const string data = "cli/doctor.json"; const string log = "cli/doctor.log"; List<string> errors = [];
        DoctorSummary summary = DoctorEvidence.Read(Resolve(root, data), commit, framework, version, false); errors.AddRange(summary.Errors);
        string actualVersion = string.Empty;
        try { using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Resolve(root, data))); actualVersion = doc.RootElement.GetProperty("build").GetProperty("semanticVersion").GetString() ?? string.Empty; if (actualVersion != version) errors.Add($"Doctor version '{actualVersion}' does not match '{version}'."); }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or KeyNotFoundException) { errors.Add($"Doctor version is unreadable: {exception.Message}"); }
        if (!File.Exists(Resolve(root, log))) errors.Add("CLI doctor log is missing.");
        return Command("doctor", "emergence doctor --json", data, log, errors, actualVersion, summary.GitCommit, summary.TargetFramework);
    }

    private static CliCommandEvidence ReadSelfTest(string root, string name, string data, string log, string property, string expected)
    {
        List<string> errors = []; ValidateJsonVector(Resolve(root, data), property, expected, errors); if (!File.Exists(Resolve(root, log))) errors.Add($"CLI {name} log is missing.");
        return Command(name, $"emergence {name} --json", data, log, errors, string.Empty, string.Empty, string.Empty);
    }

    private static CliCommandEvidence ReadDomainSelfTest(string root)
    {
        const string data = "cli/domain-self-test.json"; const string log = "cli/domain-self-test.log"; List<string> errors = [];
        string path = Resolve(root, data);
        ValidateJsonVector(path, "canonicalDigest", Phase02CanonicalDigest, errors);
        ValidateJsonVector(path, "stableIdFixture", Phase02StableId, errors);
        ValidateJsonVector(path, "algorithmCatalogDigest", Phase02CatalogDigest, errors);
        ValidateJsonVector(path, "configurationDigest", Phase02ConfigurationDigest, errors);
        if (!File.Exists(Resolve(root, log))) errors.Add("CLI domain-self-test log is missing.");
        return Command("domain-self-test", "emergence domain-self-test --json", data, log, errors, string.Empty, string.Empty, string.Empty);
    }

    private static void ValidateJsonVector(string path, string property, string expected, List<string> errors)
    {
        try { using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)); JsonElement root = document.RootElement; if (!root.TryGetProperty("success", out JsonElement success) || success.ValueKind != JsonValueKind.True) errors.Add($"{Path.GetFileName(path)} does not report success=true."); if (!root.TryGetProperty(property, out JsonElement vector) || vector.GetString() != expected) errors.Add($"{Path.GetFileName(path)} {property} mismatch."); }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException) { errors.Add($"{Path.GetFileName(path)} is invalid: {exception.Message}"); }
    }

    private static CliCommandEvidence Command(string name, string command, string data, string log, List<string> errors, string version, string commit, string framework) => new(name, command, errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed, data, log, errors.Count == 0, version, commit, framework, errors.Count == 0 ? $"CLI {name} evidence passed semantic validation." : string.Join(" ", errors));
    private static string Resolve(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    private static void Require(Dictionary<string, string> fields, string name, string expected, List<string> errors) { if (!fields.TryGetValue(name, out string? value) || !value.Equals(expected, StringComparison.OrdinalIgnoreCase)) errors.Add($"CLI version field {name} mismatch: '{value}'."); }
}
