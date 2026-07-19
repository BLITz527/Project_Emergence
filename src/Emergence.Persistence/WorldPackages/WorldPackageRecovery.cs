using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emergence.Persistence.WorldPackages;

[JsonConverter(typeof(RecoveryActionKindJsonConverter))]
public enum RecoveryActionKind
{
    TargetValidated,
    WritingValidated,
    PreviousValidated,
    WritingDeleted,
    PreviousDeleted,
    WritingPromoted,
    PreviousRestored,
    InvalidTargetQuarantined,
    EmptyStateAccepted,
}

public sealed record RecoveryAction(RecoveryActionKind Kind, string Path, string Detail);

public sealed class RecoveryResult
{
    private readonly ReadOnlyCollection<RecoveryAction> _actions;
    private readonly ReadOnlyCollection<WorldPackageIssue> _issues;

    internal RecoveryResult(bool success, string targetPath, IEnumerable<RecoveryAction> actions, IEnumerable<WorldPackageIssue> issues)
    {
        Success = success;
        TargetPath = targetPath;
        _actions = Array.AsReadOnly(actions.ToArray());
        _issues = Array.AsReadOnly(issues.ToArray());
    }

    public bool Success { get; }
    public string TargetPath { get; }
    public IReadOnlyList<RecoveryAction> Actions => _actions;
    public IReadOnlyList<WorldPackageIssue> Issues => _issues;
}

public sealed class WorldPackageRecovery
{
    private readonly WorldPackageReader _reader = new();
    private readonly Func<WorldPackageIssue?>? _lockCleanupIssueInjector;

    public WorldPackageRecovery()
    {
    }

    internal WorldPackageRecovery(Func<WorldPackageIssue?> lockCleanupIssueInjector) =>
        _lockCleanupIssueInjector = lockCleanupIssueInjector ?? throw new ArgumentNullException(nameof(lockCleanupIssueInjector));

    public RecoveryResult Recover(string packagePath)
    {
        if (!WorldPackagePathPolicy.TryResolve(packagePath, requireExtension: true, requireExistingParent: true,
                out string target, out WorldPackageIssue? pathIssue))
            return Failure(packagePath ?? string.Empty, [], pathIssue!);
        if (!WorldPackageSidecars.ValidateSafe(target, includeLock: false, out WorldPackageIssue? sidecarIssue))
            return Failure(target, [], sidecarIssue!);

        if (!WorldPackageLockLease.TryAcquire(
                target,
                _lockCleanupIssueInjector,
                out WorldPackageLockLease? lease,
                out WorldPackageIssue? lockIssue,
                out WorldPackageIssue? metadataIssue))
            return Failure(target, [], lockIssue!);
        List<WorldPackageIssue> leaseIssues = metadataIssue is null ? [] : [metadataIssue];
        RecoveryResult result;
        try
        {
            result = RecoverWithLock(target, allowEmpty: false);
        }
        finally
        {
            WorldPackageIssue? cleanupIssue = lease!.Release();
            if (cleanupIssue is not null) leaseIssues.Add(cleanupIssue);
        }

        return AppendIssues(result, leaseIssues);
    }

    internal RecoveryResult RecoverWithLock(string target, bool allowEmpty)
    {
        List<RecoveryAction> actions = [];
        string writing = WorldPackageSidecars.Writing(target);
        string previous = WorldPackageSidecars.Previous(target);
        string corrupt = WorldPackageSidecars.Corrupt(target);
        try
        {
            Candidate targetCandidate = Inspect(target);
            Candidate writingCandidate = Inspect(writing);
            Candidate previousCandidate = Inspect(previous);

            if (!targetCandidate.Exists && !writingCandidate.Exists && !previousCandidate.Exists && allowEmpty)
            {
                actions.Add(new(RecoveryActionKind.EmptyStateAccepted, target, "No package candidates exist; a new save may be created."));
                return Success(target, actions);
            }

            if (targetCandidate.Valid)
            {
                actions.Add(new(RecoveryActionKind.TargetValidated, target, "The valid target remains authoritative."));
                DeleteIfExists(writing, RecoveryActionKind.WritingDeleted, actions);
                DeleteIfExists(previous, RecoveryActionKind.PreviousDeleted, actions);
                return Success(target, actions);
            }

            if (!targetCandidate.Exists && writingCandidate.Valid)
            {
                actions.Add(new(RecoveryActionKind.WritingValidated, writing, "A valid writing candidate was found."));
                File.Move(writing, target);
                actions.Add(new(RecoveryActionKind.WritingPromoted, target, "The valid writing candidate was promoted."));
                RequireValid(target);
                DeleteIfExists(previous, RecoveryActionKind.PreviousDeleted, actions);
                return Success(target, actions);
            }

            if (!targetCandidate.Exists && previousCandidate.Valid)
            {
                actions.Add(new(RecoveryActionKind.PreviousValidated, previous, "A valid previous candidate was found."));
                if (writingCandidate.Exists && !writingCandidate.Valid)
                    DeleteIfExists(writing, RecoveryActionKind.WritingDeleted, actions);
                File.Move(previous, target);
                actions.Add(new(RecoveryActionKind.PreviousRestored, target, "The previous package was restored."));
                RequireValid(target);
                return Success(target, actions);
            }

            if (targetCandidate.Exists && !targetCandidate.Valid && previousCandidate.Valid)
            {
                if (File.Exists(corrupt)) return Failure(target, actions,
                    WorldPackageReader.Issue("world-package.recovery-corrupt-conflict", "Corrupt sidecar conflict", "The corrupt evidence sidecar already exists."));
                File.Move(target, corrupt);
                actions.Add(new(RecoveryActionKind.InvalidTargetQuarantined, corrupt, "The invalid target was quarantined."));
                File.Move(previous, target);
                actions.Add(new(RecoveryActionKind.PreviousRestored, target, "The previous package was restored."));
                RequireValid(target);
                return Success(target, actions);
            }

            if (targetCandidate.Exists && !targetCandidate.Valid && !previousCandidate.Exists && writingCandidate.Valid)
            {
                if (File.Exists(corrupt)) return Failure(target, actions,
                    WorldPackageReader.Issue("world-package.recovery-corrupt-conflict", "Corrupt sidecar conflict", "The corrupt evidence sidecar already exists."));
                File.Move(target, corrupt);
                actions.Add(new(RecoveryActionKind.InvalidTargetQuarantined, corrupt, "The invalid target was quarantined."));
                File.Move(writing, target);
                actions.Add(new(RecoveryActionKind.WritingPromoted, target, "The valid writing package was promoted."));
                RequireValid(target);
                return Success(target, actions);
            }

            return Failure(target, actions, WorldPackageReader.Issue(
                "world-package.recovery-no-candidate",
                "No valid recovery candidate",
                "No valid target, writing, or previous package can be selected without destroying evidence."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Failure(target, actions, WorldPackageReader.Issue(
                "world-package.recovery-transaction",
                "World package recovery transaction failed",
                "A filesystem operation failed; valid candidates and recovery evidence were preserved where possible."));
        }
    }

    private Candidate Inspect(string path)
    {
        bool exists = File.Exists(path);
        if (!exists) return new(false, false);
        WorldPackageLoadResult load = _reader.LoadCandidate(path);
        return new(true, load.Success);
    }

    private void RequireValid(string path)
    {
        WorldPackageLoadResult validation = _reader.LoadCandidate(path);
        if (!validation.Success) throw new InvalidDataException(validation.Issues[0].Detail);
    }

    private static void DeleteIfExists(string path, RecoveryActionKind kind, List<RecoveryAction> actions)
    {
        if (!File.Exists(path)) return;
        File.Delete(path);
        actions.Add(new(kind, path, "Validated stale sidecar removed."));
    }

    private static RecoveryResult Success(string target, IEnumerable<RecoveryAction> actions) => new(true, target, actions, []);
    private static RecoveryResult Failure(string target, IEnumerable<RecoveryAction> actions, WorldPackageIssue issue) => new(false, target, actions, [issue]);
    private static RecoveryResult AppendIssues(RecoveryResult result, IReadOnlyList<WorldPackageIssue> issues) => issues.Count == 0
        ? result
        : new RecoveryResult(result.Success, result.TargetPath, result.Actions, result.Issues.Concat(issues));
    private readonly record struct Candidate(bool Exists, bool Valid);
}

internal static class WorldPackageSidecars
{
    public static string Writing(string target) => target + ".writing";
    public static string Previous(string target) => target + ".previous";
    public static string Lock(string target) => target + ".lock";
    public static string Corrupt(string target) => target + ".corrupt";

    public static bool ValidateSafe(string target, bool includeLock, out WorldPackageIssue? issue)
    {
        issue = null;
        string[] paths = includeLock
            ? [target, Writing(target), Previous(target), Lock(target), Corrupt(target)]
            : [target, Writing(target), Previous(target), Corrupt(target)];
        foreach (string path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) continue;
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                {
                    issue = WorldPackageReader.Issue("world-package.sidecar-unsafe", "Unsafe package or sidecar path", path);
                    return false;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issue = WorldPackageReader.Issue("world-package.sidecar-unavailable", "Package sidecar is unavailable", WorldPackageReader.Normalize(exception.Message));
                return false;
            }
        }
        return true;
    }
}

internal sealed class RecoveryActionKindJsonConverter : JsonConverter<RecoveryActionKind>
{
    public override RecoveryActionKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Recovery action kind must be an exact string.");
        return reader.GetString() switch
        {
            "TargetValidated" => RecoveryActionKind.TargetValidated,
            "WritingValidated" => RecoveryActionKind.WritingValidated,
            "PreviousValidated" => RecoveryActionKind.PreviousValidated,
            "WritingDeleted" => RecoveryActionKind.WritingDeleted,
            "PreviousDeleted" => RecoveryActionKind.PreviousDeleted,
            "WritingPromoted" => RecoveryActionKind.WritingPromoted,
            "PreviousRestored" => RecoveryActionKind.PreviousRestored,
            "InvalidTargetQuarantined" => RecoveryActionKind.InvalidTargetQuarantined,
            "EmptyStateAccepted" => RecoveryActionKind.EmptyStateAccepted,
            _ => throw new JsonException("Unknown recovery action kind."),
        };
    }

    public override void Write(Utf8JsonWriter writer, RecoveryActionKind value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException("Undefined recovery action kind.");
        writer.WriteStringValue(value.ToString());
    }
}
