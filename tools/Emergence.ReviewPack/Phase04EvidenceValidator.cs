using System.Text.Json;
using System.Text.RegularExpressions;

namespace Emergence.ReviewPack;

public static class Phase04EvidenceValidator
{
    public const string CorrectionPhase = "M0 Phase 0.4R";
    public const string AlgorithmCatalogDigest = "bbaebfc88087fc04ab024d2505b9a50ed7e7a2f21cd34a18eb4e83d56cb1a418";
    public const string SchedulerGraphDigest = "3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a";
    public const string SessionDefinitionDigest = "fcc91152d376a93f558f44c2e76eb8493ab61fb519d598faa8782992d8cd3456";
    public const string SessionTraceDigest = "58f7313342790881b43875ba1bf3461e2aa8b1dd4b23d19278dd32cd973a7491";
    public const string FinalStateDigest = "6de0d3bee6901dfdd83b080545ce58efcd86a2b52bf67f21692a947d19fb9ff0";
    public static IReadOnlyList<string> EventIds { get; } = Array.AsReadOnly(new[]
    {
        "4598305249711c692a4e067efca02ee9",
        "42abdecf1c4b67c932774281ef342a58",
        "dbd65e20c6c2ea23b9d8a57d51820e3a",
        "21bc5536586ec3ca10486cf27925c6e6",
        "6f91cd4dd0c768ff9d210fef527e978f",
        "d94aa1148c34320c397278b2af0d3e9e",
        "79fa3e441504547adc4f252fed87ade9",
        "4d5a5e7f1c2c38c29e375984d1c9cc93",
        "97bf2d51795ee43365d08aa9e1c085f4",
        "54ace7a336281d6a987af49193031dd6",
    });

    private static readonly IReadOnlyDictionary<string, string[]> RequiredCorrectionTests = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["tests/Emergence.Simulation.Tests/Emergence.Simulation.Tests.trx"] =
        [
            "ActiveStepMutationFromCommandProcessorFaultsAtomically",
            "ActiveStepMutationFromSimulationSystemFaultsAtomically",
            "ReentrantCommandProcessorFaultsWithEmptyOrNonemptyGraph",
            "SuccessfulCallbackIssuesArePreservedInDeterministicOrder",
            "ReceiptIssueLimitIsExactAndOneOverFaultsAtomically",
            "WrongThreadMutationDuringStepIsRejectedWithoutViolatingOwnerTransaction",
        ],
        ["tests/Emergence.Foundation.Tests/Emergence.Foundation.Tests.trx"] = ["IssueSeverityUsesExactClosedJson"],
        ["tests/Emergence.Model.Tests/Emergence.Model.Tests.trx"] = ["TickReceiptDefinitionIdentityIsImmutableAndJsonStable", "TickReceiptIssuesAreDefensivelyCopied"],
        ["tests/Emergence.Presentation.Contracts.Tests/Emergence.Presentation.Contracts.Tests.trx"] = ["CrossSessionReceiptBindingRejectsOtherBranchOrWorld"],
        ["tests/Emergence.Architecture.Tests/Emergence.Architecture.Tests.trx"] = ["ProductionCallbacksAreDocumentedAndStateless"],
    };

    public static SessionEvidence Evaluate(
        string reviewRoot,
        string expectedCommit,
        string expectedVersion = "0.4.0-dev",
        bool requirePresentation = true)
    {
        const string dataRelative = "cli/session-self-test.json";
        const string logRelative = "cli/session-self-test.log";
        const string appDoctorRelative = "app/doctor.json";
        const string packageDoctorRelative = "package/packaged-doctor.json";
        List<string> errors = [];
        string phase = string.Empty, version = string.Empty, commit = string.Empty, algorithm = string.Empty, graph = string.Empty;
        string definition = string.Empty, trace = string.Empty, state = string.Empty, tick = string.Empty;
        int commands = 0, events = 0;
        string[] eventIds = [];

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Resolve(reviewRoot, dataRelative)));
            JsonElement root = document.RootElement;
            Exact(root, "success", "phase", "version", "gitCommit", "algorithmCatalogDigest", "schedulerGraphDigest", "sessionDefinitionDigest", "sessionTraceDigest", "finalStateDigest", "finalTick", "acceptedCommands", "committedEvents", "eventIds", "checks");
            if (root.GetProperty("success").ValueKind != JsonValueKind.True) errors.Add("Session self-test does not report success=true.");
            phase = root.GetProperty("phase").GetString() ?? string.Empty;
            version = root.GetProperty("version").GetString() ?? string.Empty;
            commit = root.GetProperty("gitCommit").GetString() ?? string.Empty;
            algorithm = root.GetProperty("algorithmCatalogDigest").GetString() ?? string.Empty;
            graph = root.GetProperty("schedulerGraphDigest").GetString() ?? string.Empty;
            definition = root.GetProperty("sessionDefinitionDigest").GetString() ?? string.Empty;
            trace = root.GetProperty("sessionTraceDigest").GetString() ?? string.Empty;
            state = root.GetProperty("finalStateDigest").GetString() ?? string.Empty;
            tick = root.GetProperty("finalTick").GetString() ?? string.Empty;
            commands = root.GetProperty("acceptedCommands").GetInt32();
            events = root.GetProperty("committedEvents").GetInt32();
            eventIds = root.GetProperty("eventIds").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
            if (phase != CorrectionPhase) errors.Add($"Session phase mismatch: '{phase}'.");
            if (version != expectedVersion) errors.Add($"Session version mismatch: '{version}'.");
            if (!commit.Equals(expectedCommit, StringComparison.OrdinalIgnoreCase)) errors.Add($"Session reviewed commit mismatch: '{commit}'.");
            Require(algorithm, AlgorithmCatalogDigest, "algorithm catalog", errors);
            Require(graph, SchedulerGraphDigest, "scheduler graph", errors);
            Require(definition, SessionDefinitionDigest, "session definition", errors);
            Require(trace, SessionTraceDigest, "session trace", errors);
            Require(state, FinalStateDigest, "final state", errors);
            if (tick != "2") errors.Add($"Session final tick mismatch: '{tick}'.");
            if (commands != 4) errors.Add($"Accepted command count mismatch: {commands}.");
            if (events != 10) errors.Add($"Committed event count mismatch: {events}.");
            if (!eventIds.SequenceEqual(EventIds, StringComparer.Ordinal)) errors.Add("Session EventIds are missing, reordered, or incorrect.");
            JsonElement[] checks = root.GetProperty("checks").EnumerateArray().ToArray();
            if (checks.Length == 0 || checks.Any(item => item.GetProperty("severity").GetString() != "Success")) errors.Add("Session self-test checks are absent or not all successful.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            errors.Add($"Session self-test evidence is invalid: {exception.Message}");
        }
        if (!File.Exists(Resolve(reviewRoot, logRelative))) errors.Add("Session self-test log is missing.");
        ValidateCorrectionTests(reviewRoot, errors);

        bool presentation = !requirePresentation
            || (ValidateDoctor(Resolve(reviewRoot, appDoctorRelative), expectedCommit, errors, "source")
                && ValidateDoctor(Resolve(reviewRoot, packageDoctorRelative), expectedCommit, errors, "packaged"));
        string[] evidencePaths = requirePresentation
            ? [dataRelative, logRelative, appDoctorRelative, packageDoctorRelative, .. RequiredCorrectionTests.Keys]
            : [dataRelative, logRelative, .. RequiredCorrectionTests.Keys];
        return new SessionEvidence(
            "emergence session-self-test --json",
            errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed,
            phase,
            version,
            commit,
            algorithm,
            graph,
            definition,
            trace,
            state,
            tick,
            commands,
            events,
            Array.AsReadOnly(eventIds),
            presentation,
            requirePresentation ? (presentation ? "Paused@0" : string.Empty) : "Superseded by Phase 0.5 shell; prior session vectors retained",
            Array.AsReadOnly(evidencePaths),
            errors.Count == 0 ? "Phase 0.4R transaction, issue, receipt, session, event, state, and presentation evidence passed independent semantic validation." : string.Join(" ", errors));
    }

    private static bool ValidateDoctor(string path, string expectedCommit, List<string> errors, string role)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (root.GetProperty("success").ValueKind != JsonValueKind.True) errors.Add($"{role} App doctor does not report success=true.");
            JsonElement build = root.GetProperty("build");
            if (build.GetProperty("semanticVersion").GetString() != "0.4.0-dev") errors.Add($"{role} App doctor has wrong Phase 0.4R version.");
            if (!string.Equals(build.GetProperty("gitCommit").GetString(), expectedCommit, StringComparison.OrdinalIgnoreCase)) errors.Add($"{role} App doctor has wrong reviewed commit.");
            Dictionary<string, JsonElement> checks = root.GetProperty("checks").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetString() ?? string.Empty, StringComparer.Ordinal);
            string[] required = ["phase.identity", "session.definition", "session.scheduler", "presentation.snapshot", "presentation.nonbiological", "presentation.no-mutation", "session.core-headless"];
            foreach (string id in required)
            {
                if (!checks.TryGetValue(id, out JsonElement check) || check.GetProperty("severity").GetString() != "Success") errors.Add($"{role} App doctor is missing successful check '{id}'.");
            }
            if (checks.TryGetValue("phase.identity", out JsonElement phaseIdentity) && phaseIdentity.GetProperty("detail").GetString() != CorrectionPhase) errors.Add($"{role} App doctor has stale correction phase identity.");
            if (checks.TryGetValue("session.definition", out JsonElement definition) && definition.GetProperty("detail").GetString() != SessionDefinitionDigest) errors.Add($"{role} App session definition mismatch.");
            if (checks.TryGetValue("session.scheduler", out JsonElement graph) && graph.GetProperty("detail").GetString() != SchedulerGraphDigest) errors.Add($"{role} App scheduler graph mismatch.");
            if (checks.TryGetValue("presentation.snapshot", out JsonElement snapshot))
            {
                string detail = snapshot.GetProperty("detail").GetString() ?? string.Empty;
                string expectedPrefix = "world=0000000000000000000000000000002a;branch=00000000000000000000000000000007;tick=0;status=Paused;definition=" + SessionDefinitionDigest + ";state=";
                if (!detail.StartsWith(expectedPrefix, StringComparison.Ordinal) || !Regex.IsMatch(detail[expectedPrefix.Length..], "^[0-9a-f]{64}$", RegexOptions.CultureInvariant)) errors.Add($"{role} App snapshot/session mismatch.");
            }
            if (checks.TryGetValue("presentation.nonbiological", out JsonElement biology) && biology.GetProperty("detail").GetString() != "hasBiologicalState=false") errors.Add($"{role} App doctor claims biological state.");
            if (checks.TryGetValue("presentation.no-mutation", out JsonElement mutation))
            {
                string detail = mutation.GetProperty("detail").GetString() ?? string.Empty;
                Match match = Regex.Match(detail, "^before=([0-9a-f]{64});after=([0-9a-f]{64})$", RegexOptions.CultureInvariant);
                if (!match.Success || match.Groups[1].Value != match.Groups[2].Value) errors.Add($"{role} snapshot creation changed session state.");
            }
            return required.All(id => checks.TryGetValue(id, out JsonElement value) && value.GetProperty("severity").GetString() == "Success");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or ArgumentException)
        {
            errors.Add($"{role} App doctor session evidence is invalid: {exception.Message}");
            return false;
        }
    }

    private static void ValidateCorrectionTests(string reviewRoot, List<string> errors)
    {
        foreach ((string relative, string[] requiredNames) in RequiredCorrectionTests)
        {
            string path = Resolve(reviewRoot, relative);
            if (!File.Exists(path))
            {
                errors.Add($"Correction test evidence is missing: {relative}.");
                continue;
            }
            string trx = File.ReadAllText(path);
            foreach (string requiredName in requiredNames)
                if (!trx.Contains(requiredName, StringComparison.Ordinal)) errors.Add($"Correction test evidence '{relative}' does not include '{requiredName}'.");
        }
    }

    private static void Exact(JsonElement root, params string[] expected)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected session self-test object.");
        HashSet<string> required = new(expected, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
            if (!required.Contains(property.Name) || !seen.Add(property.Name)) throw new JsonException($"Unexpected or duplicate session property '{property.Name}'.");
        if (!required.SetEquals(seen)) throw new JsonException("Session self-test is missing required properties.");
    }

    private static void Require(string actual, string expected, string name, List<string> errors)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal)) errors.Add($"Phase 0.4R {name} digest mismatch.");
    }

    private static string Resolve(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
}
