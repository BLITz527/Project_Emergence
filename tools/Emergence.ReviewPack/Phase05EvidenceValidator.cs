using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Emergence.Foundation.Text;
using Emergence.Persistence.WorldPackages;

namespace Emergence.ReviewPack;

public static class Phase05EvidenceValidator
{
    public const string Phase = "M0 Phase 0.5R";
    public const string AlgorithmCatalogDigest = "78818c4c6a6a4aeb498a634e4cd77e5854c3fa35be2d075aabb888cb0fe7d9a1";
    public const string CommandProcessorCatalogDigest = "e2555f63b5b4c9644229336da1856f35c8dabf3cf54765e224d3c51e19a3d8f6";
    public const string DefinitionDigest = "ca024a17b1e0ee02b57d639bea1f57d0f04154e6c3da501fd24af0ebe9798e0e";
    public const string PreSaveStateDigest = "9c309262449fa1590750b9c320e853306fa516925bc2e05da606ff8c8e86e6cc";
    public const string SnapshotDigest = "33427d66eb92322396cd632ad3971407441e1ca09a72e7136549624213655893";
    public const string PackageIdentityDigest = "fcfab8b4e95de5f578330eb0d599e8759ebb62ca6fc37210f36197a88927c3d1";
    public const string FinalStateDigest = "fb303204175f2ed6186755e9d8ff8877bcc60892554e4765f52a4224f9f706dd";
    public const string PersistenceTraceDigest = "b527e3355bc94f2eef586214f7ecf841b968c380b7427250c7fa06216aae8d0e";

    public static IReadOnlyList<string> ContinuationEventIds { get; } = Array.AsReadOnly(new[]
    {
        "8adf4015e21a6e9b4d67bf735ca95840",
        "eaf3454d0b583165c89d3d785a483e7b",
        "3ca4b0b1f20eab439cca3a7d874531ef",
        "521e2a0fa467efc0f2fac2601f1194f3",
    });

    private static readonly string[] EntryNames = ["definition.json", "snapshot.json", "package-manifest.json"];
    private static readonly string[] EvidenceFiles =
    [
        "cli/persistence-self-test.json", "cli/persistence-self-test.log",
        "cli/foundation-session.emergence-world", "cli/world-package-fixture.json", "cli/world-package-fixture.log",
        "cli/world-package-verify.json", "cli/world-package-verify.log", "cli/world-package-recover.json", "cli/world-package-recover.log",
        "persistence/definition.json", "persistence/snapshot.json", "persistence/package-manifest.json", "persistence/package-inventory.json",
        "app/doctor.json", "package/packaged-doctor.json",
    ];

    public static PersistenceEvidence Evaluate(string reviewRoot)
    {
        List<string> errors = [];
        string algorithm = string.Empty, processors = string.Empty, definition = string.Empty, preState = string.Empty;
        string snapshot = string.Empty, packageIdentity = string.Empty, manifestDigest = string.Empty, loadedState = string.Empty;
        string finalState = string.Empty, trace = string.Empty, nextSequence = string.Empty;
        long packageBytes = 0;
        bool rngMatched = false, appRoundTrip = false, packagedRoundTrip = false;
        bool appStaleLock = false, packagedStaleLock = false;
        string[] eventIds = [];
        int recoveryCount = 0, recoveryPassed = 0, lockCount = 0, lockPassed = 0, entryCount = 0;

        foreach (string relative in EvidenceFiles)
            if (!File.Exists(Resolve(reviewRoot, relative))) errors.Add($"Phase 0.5R evidence is missing: {relative}.");

        try
        {
            using JsonDocument document = ReadJson(Resolve(reviewRoot, "cli/persistence-self-test.json"));
            JsonElement root = document.RootElement;
            Exact(root, "success", "algorithmCatalogDigest", "commandProcessorCatalogDigest", "definitionDigest",
                "preSaveStateDigest", "snapshotDigest", "packageIdentityDigest", "manifestDigest", "packageBytes",
                "loadedStateDigest", "rngContinuationMatched", "nextCommandSequence", "continuationEventIds",
                "finalStateDigest", "persistenceTraceDigest", "recoveryChecks", "lockChecks", "checks");
            if (root.GetProperty("success").ValueKind != JsonValueKind.True) errors.Add("Persistence self-test does not report success=true.");
            algorithm = String(root, "algorithmCatalogDigest");
            processors = String(root, "commandProcessorCatalogDigest");
            definition = String(root, "definitionDigest");
            preState = String(root, "preSaveStateDigest");
            snapshot = String(root, "snapshotDigest");
            packageIdentity = String(root, "packageIdentityDigest");
            manifestDigest = String(root, "manifestDigest");
            packageBytes = root.GetProperty("packageBytes").GetInt64();
            loadedState = String(root, "loadedStateDigest");
            rngMatched = root.GetProperty("rngContinuationMatched").ValueKind == JsonValueKind.True;
            nextSequence = root.GetProperty("nextCommandSequence").GetString() ?? string.Empty;
            eventIds = root.GetProperty("continuationEventIds").EnumerateArray().Select(static item => item.GetString() ?? string.Empty).ToArray();
            finalState = String(root, "finalStateDigest");
            trace = String(root, "persistenceTraceDigest");
            JsonElement[] recovery = root.GetProperty("recoveryChecks").EnumerateArray().ToArray();
            recoveryCount = recovery.Length;
            recoveryPassed = recovery.Count(IsSuccessCheck);
            JsonElement[] lockChecks = root.GetProperty("lockChecks").EnumerateArray().ToArray();
            lockCount = lockChecks.Length;
            lockPassed = lockChecks.Count(IsSuccessCheck);
            RequireLockChecks(lockChecks, errors);
            if (!root.GetProperty("checks").EnumerateArray().All(IsSuccessCheck)) errors.Add("Persistence self-test contains a failed diagnostic check.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or FormatException)
        {
            errors.Add($"Persistence self-test is invalid: {exception.Message}");
        }

        Require(algorithm, AlgorithmCatalogDigest, "Phase05 algorithm catalog", errors);
        Require(processors, CommandProcessorCatalogDigest, "command processor catalog", errors);
        Require(definition, DefinitionDigest, "V2 definition", errors);
        Require(preState, PreSaveStateDigest, "pre-save state", errors);
        Require(snapshot, SnapshotDigest, "snapshot", errors);
        Require(packageIdentity, PackageIdentityDigest, "package identity", errors);
        Require(loadedState, PreSaveStateDigest, "loaded state", errors);
        Require(finalState, FinalStateDigest, "continuation final state", errors);
        Require(trace, PersistenceTraceDigest, "persistence trace", errors);
        if (!rngMatched) errors.Add("Addressed RNG continuation did not match.");
        if (nextSequence != "5") errors.Add($"Next command sequence is '{nextSequence}', expected '5'.");
        if (!eventIds.SequenceEqual(ContinuationEventIds, StringComparer.Ordinal)) errors.Add("Continuation EventIds do not match the locked vector.");
        if (recoveryCount != 5 || recoveryPassed != 5) errors.Add($"Recovery scenarios passed {recoveryPassed}/{recoveryCount}, expected 5/5.");
        if (lockCount != 6 || lockPassed != 6) errors.Add($"Lock checks passed {lockPassed}/{lockCount}, expected 6/6.");

        string packagePath = Resolve(reviewRoot, "cli/foundation-session.emergence-world");
        try
        {
            FileInfo packageInfo = new(packagePath);
            if (packageInfo.Length != packageBytes) errors.Add($"Fixture package byte size {packageInfo.Length} disagrees with self-test {packageBytes}.");
            WorldPackageLoadResult load = new WorldPackageReader().Load(packagePath);
            if (!load.Success || load.Document is null)
            {
                errors.Add("Production world-package reader rejected the review fixture: " + string.Join(" ", load.Issues.Select(static issue => issue.Code)));
            }
            else
            {
                Require(load.Document.Definition.Digest.ToString(), DefinitionDigest, "package definition", errors);
                Require(load.Document.Snapshot.Digest.ToString(), SnapshotDigest, "package snapshot", errors);
                Require(load.Document.Snapshot.StateDigest.ToString(), PreSaveStateDigest, "package state", errors);
                Require(load.Document.Manifest.PackageIdentityDigest.ToString(), PackageIdentityDigest, "package manifest identity", errors);
                Require(load.Document.Manifest.Digest.ToString(), manifestDigest, "package manifest", errors);
            }

            using FileStream stream = new(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read, false);
            ZipArchiveEntry[] entries = archive.Entries.ToArray();
            entryCount = entries.Length;
            if (entryCount != 3 || !entries.Select(static entry => entry.FullName).SequenceEqual(EntryNames, StringComparer.Ordinal))
                errors.Add("Fixture package does not contain the exact ordered three-entry inventory.");
            Dictionary<string, byte[]> raw = entries.ToDictionary(static entry => entry.FullName, ReadEntry, StringComparer.Ordinal);
            ValidateExtracted(reviewRoot, raw, errors);
            ValidateInternalManifest(raw, errors);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
        {
            errors.Add($"World package evidence is invalid: {exception.Message}");
        }

        ValidateCommandReport(reviewRoot, "cli/world-package-fixture.json", packageIdentity, manifestDigest, snapshot, preState, errors);
        ValidateCommandReport(reviewRoot, "cli/world-package-verify.json", packageIdentity, manifestDigest, snapshot, preState, errors);
        ValidateSuccessOnly(reviewRoot, "cli/world-package-recover.json", errors);
        (appRoundTrip, appStaleLock) = ValidateDoctor(reviewRoot, "app/doctor.json", errors);
        (packagedRoundTrip, packagedStaleLock) = ValidateDoctor(reviewRoot, "package/packaged-doctor.json", errors);

        EvidenceStatus status = errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed;
        return new PersistenceEvidence(
            "emergence persistence-self-test --json <path>", status, algorithm, processors, definition, preState,
            snapshot, packageIdentity, manifestDigest, packageBytes, entryCount, loadedState, rngMatched, nextSequence,
            Array.AsReadOnly(eventIds), finalState, trace, recoveryCount, recoveryPassed, lockCount, lockPassed,
            appRoundTrip, packagedRoundTrip, appStaleLock, packagedStaleLock,
            Array.AsReadOnly(EvidenceFiles), errors.Count == 0
                ? "Phase 0.5R snapshot, package, unchanged continuation/recovery, crash-recoverable lock, and App/package evidence passed independent semantic validation."
                : string.Join(" ", errors));
    }

    private static void ValidateExtracted(string root, IReadOnlyDictionary<string, byte[]> raw, List<string> errors)
    {
        foreach (string name in EntryNames)
        {
            string path = Resolve(root, "persistence/" + name);
            if (!raw.TryGetValue(name, out byte[]? bytes) || !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
                errors.Add($"Extracted package evidence does not exactly match '{name}'.");
        }
        string inventoryPath = Resolve(root, "persistence/package-inventory.json");
        try
        {
            using JsonDocument document = ReadJson(inventoryPath);
            JsonElement[] entries = document.RootElement.EnumerateArray().ToArray();
            if (entries.Length != 3) errors.Add("Package inventory does not contain three entries.");
            for (int index = 0; index < Math.Min(entries.Length, EntryNames.Length); index++)
            {
                Exact(entries[index], "path", "length", "sha256");
                byte[] bytes = raw[EntryNames[index]];
                if (String(entries[index], "path") != EntryNames[index]
                    || entries[index].GetProperty("length").GetInt64() != bytes.LongLength
                    || String(entries[index], "sha256") != Hex(SHA256.HashData(bytes)))
                    errors.Add($"Package inventory entry {index} is inconsistent.");
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            errors.Add($"Package inventory is invalid: {exception.Message}");
        }
    }

    private static void ValidateInternalManifest(IReadOnlyDictionary<string, byte[]> raw, List<string> errors)
    {
        using JsonDocument document = JsonDocument.Parse(StrictUtf8.GetString(raw["package-manifest.json"]));
        JsonElement root = document.RootElement;
        JsonElement[] entries = root.GetProperty("entries").EnumerateArray().ToArray();
        if (entries.Length != 2) errors.Add("World package manifest does not list exactly two data documents.");
        for (int index = 0; index < Math.Min(entries.Length, 2); index++)
        {
            string expected = EntryNames[index];
            byte[] bytes = raw[expected];
            JsonElement entry = entries[index];
            if (String(entry, "path") != expected
                || String(entry, "uncompressedByteLength") != bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture)
                || String(entry, "sha256") != Hex(SHA256.HashData(bytes)))
                errors.Add($"World package manifest entry '{expected}' has a length or hash mismatch.");
        }
    }

    private static void ValidateCommandReport(string root, string relative, string identity, string manifest, string snapshot, string state, List<string> errors)
    {
        try
        {
            using JsonDocument document = ReadJson(Resolve(root, relative));
            JsonElement value = document.RootElement;
            if (value.GetProperty("success").ValueKind != JsonValueKind.True) errors.Add($"{relative} does not report success=true.");
            if (value.TryGetProperty("packageIdentityDigest", out JsonElement identityValue) && identityValue.GetString() != identity) errors.Add($"{relative} package identity mismatch.");
            if (value.TryGetProperty("manifestDigest", out JsonElement manifestValue) && manifestValue.GetString() != manifest) errors.Add($"{relative} manifest mismatch.");
            if (value.TryGetProperty("snapshotDigest", out JsonElement snapshotValue) && snapshotValue.GetString() != snapshot) errors.Add($"{relative} snapshot mismatch.");
            if (value.TryGetProperty("stateDigest", out JsonElement stateValue) && stateValue.GetString() != state) errors.Add($"{relative} state mismatch.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            errors.Add($"{relative} is invalid: {exception.Message}");
        }
    }

    private static void ValidateSuccessOnly(string root, string relative, List<string> errors)
    {
        try
        {
            using JsonDocument document = ReadJson(Resolve(root, relative));
            if (document.RootElement.GetProperty("success").ValueKind != JsonValueKind.True) errors.Add($"{relative} does not report success=true.");
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            errors.Add($"{relative} is invalid: {exception.Message}");
        }
    }

    private static (bool RoundTrip, bool StaleLock) ValidateDoctor(string root, string relative, List<string> errors)
    {
        try
        {
            using JsonDocument document = ReadJson(Resolve(root, relative));
            JsonElement value = document.RootElement;
            JsonElement[] checks = value.GetProperty("checks").EnumerateArray().ToArray();
            string[] ids = ["persistence.round-trip", "persistence.rng-continuation", "persistence.stale-lock", "persistence.sidecars"];
            bool valid = value.GetProperty("success").ValueKind == JsonValueKind.True
                && ids.All(id => checks.Any(check => String(check, "id") == id && String(check, "severity") == "Success"));
            bool staleLock = checks.Any(check =>
                String(check, "id") == "persistence.stale-lock"
                && String(check, "severity") == "Success");
            if (!valid) errors.Add($"{relative} does not prove the required save/load, RNG, stale-lock, and sidecar checks.");
            return (valid, staleLock);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            errors.Add($"{relative} persistence evidence is invalid: {exception.Message}");
            return (false, false);
        }
    }

    private static void RequireLockChecks(IReadOnlyList<JsonElement> checks, List<string> errors)
    {
        string[] required =
        [
            "lock.stale-save",
            "lock.stale-recover",
            "lock.active-save-contention",
            "lock.active-recovery-contention",
            "lock.reacquire-after-release",
            "lock.normal-sidecar-clean",
        ];
        foreach (string id in required)
        {
            if (!checks.Any(check => String(check, "id") == id && IsSuccessCheck(check)))
                errors.Add($"Persistence self-test is missing successful lock check '{id}'.");
        }
    }

    private static JsonDocument ReadJson(string path) => JsonDocument.Parse(StrictUtf8.GetString(File.ReadAllBytes(path)), new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 });
    private static byte[] ReadEntry(ZipArchiveEntry entry) { using Stream input = entry.Open(); using MemoryStream output = new(); input.CopyTo(output); return output.ToArray(); }
    private static bool IsSuccessCheck(JsonElement check) => String(check, "severity") == "Success";
    private static string String(JsonElement element, string property) => element.GetProperty(property).GetString() ?? string.Empty;
    private static string Resolve(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
    private static void Require(string actual, string expected, string name, List<string> errors) { if (actual != expected) errors.Add($"Phase 0.5R {name} digest mismatch."); }
    private static void Exact(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a JSON object.");
        HashSet<string> required = new(expected, StringComparer.Ordinal);
        HashSet<string> actual = element.EnumerateObject().Select(static property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(required) || element.EnumerateObject().Count() != required.Count) throw new JsonException("JSON object has missing, unexpected, or duplicate properties.");
    }
}
