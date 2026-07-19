using System.IO.Compression;
using Emergence.Foundation.Hashing;
using Emergence.Model;

namespace Emergence.Persistence.WorldPackages;

public sealed class WorldPackageWriter
{
    private readonly WorldPackageReader _reader = new();
    private readonly WorldPackageRecovery _recovery = new();
    private readonly Action<WorldPackageFaultPoint>? _faultInjector;
    private readonly Func<WorldPackageIssue?>? _lockCleanupIssueInjector;

    public WorldPackageWriter()
    {
    }

    internal WorldPackageWriter(Action<WorldPackageFaultPoint> faultInjector) =>
        _faultInjector = faultInjector ?? throw new ArgumentNullException(nameof(faultInjector));

    internal WorldPackageWriter(
        Action<WorldPackageFaultPoint>? faultInjector,
        Func<WorldPackageIssue?> lockCleanupIssueInjector)
    {
        _faultInjector = faultInjector;
        _lockCleanupIssueInjector = lockCleanupIssueInjector ?? throw new ArgumentNullException(nameof(lockCleanupIssueInjector));
    }

    public WorldPackageSaveResult Save(string destinationPath, WorldSessionSnapshot snapshot)
    {
        if (snapshot is null) return Failure(destinationPath ?? string.Empty, "world-package.snapshot-null", "Missing session snapshot", "A validated snapshot is required.");
        if (!snapshot.Definition.IsSaveable)
            return Failure(destinationPath ?? string.Empty, "world-package.snapshot-version", "Snapshot is not saveable", "Definition format 2.0.0 is required.");
        if (!WorldPackagePathPolicy.TryResolve(destinationPath, requireExtension: true, requireExistingParent: true,
                out string target, out WorldPackageIssue? pathIssue))
            return Failure(destinationPath ?? string.Empty, pathIssue!);
        if (!WorldPackageSidecars.ValidateSafe(target, includeLock: false, out WorldPackageIssue? sidecarIssue))
            return Failure(target, sidecarIssue!);

        PackageContent content;
        try { content = PackageContent.Create(snapshot); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Failure(target, "world-package.serialization", "World package serialization failed", WorldPackageReader.Normalize(exception.Message));
        }

        if (!WorldPackageLockLease.TryAcquire(
                target,
                _lockCleanupIssueInjector,
                out WorldPackageLockLease? lease,
                out WorldPackageIssue? lockIssue,
                out WorldPackageIssue? metadataIssue))
            return Failure(target, lockIssue!);
        List<WorldPackageIssue> leaseIssues = metadataIssue is null ? [] : [metadataIssue];

        string writing = WorldPackageSidecars.Writing(target);
        string previous = WorldPackageSidecars.Previous(target);
        WorldPackageSaveResult result;
        try
        {
            RecoveryResult recovery = _recovery.RecoverWithLock(target, allowEmpty: true);
            if (!recovery.Success)
            {
                WorldPackageIssue issue = recovery.Issues.Count == 0
                    ? WorldPackageReader.Issue("world-package.recovery", "Recovery inspection failed", "The target is not coherent for save.")
                    : recovery.Issues[0];
                result = Failure(target, issue);
            }
            else
            {
                result = SaveWithLock(target, writing, previous, content);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            TryRestorePrevious(target, previous);
            result = Failure(
                target,
                "world-package.save-transaction",
                "Atomic world package save transaction failed",
                "A filesystem operation failed; the prior target or recovery evidence remains authoritative.");
        }
        finally
        {
            WorldPackageIssue? cleanupIssue = lease!.Release();
            if (cleanupIssue is not null) leaseIssues.Add(cleanupIssue);
        }

        return AppendIssues(result, leaseIssues);
    }

    private WorldPackageSaveResult SaveWithLock(string target, string writing, string previous, PackageContent content)
    {
        Hit(WorldPackageFaultPoint.BeforeStagingCreation);
        if (File.Exists(writing) || Directory.Exists(writing))
            throw new IOException("The writing sidecar was not resolved before save.");
        WriteStaging(writing, content);
        Hit(WorldPackageFaultPoint.AfterStagingCloseBeforeValidation);
        RequireExpectedPackage(writing, content);

        if (File.Exists(target))
        {
            WorldPackageLoadResult existing = _reader.LoadCandidate(target);
            if (!existing.Success) throw new InvalidDataException("An invalid existing target cannot be overwritten.");
        }
        if (File.Exists(previous) || Directory.Exists(previous))
            throw new IOException("The previous sidecar was not resolved before replacement.");

        bool hadTarget = File.Exists(target);
        if (hadTarget)
        {
            File.Move(target, previous);
            Hit(WorldPackageFaultPoint.AfterOldTargetMovedToPrevious);
        }

        File.Move(writing, target);
        Hit(WorldPackageFaultPoint.AfterWritingPromotedToTarget);
        Hit(WorldPackageFaultPoint.DuringFinalValidation);
        try
        {
            RequireExpectedPackage(target, content);
        }
        catch
        {
            TryRestorePrevious(target, previous);
            throw;
        }

        Hit(WorldPackageFaultPoint.BeforePreviousDeletion);
        if (File.Exists(previous)) File.Delete(previous);
        long packageBytes = new FileInfo(target).Length;
        return new(
            true,
            target,
            content.Manifest.PackageIdentityDigest.ToString(),
            content.Manifest.Digest.ToString(),
            packageBytes,
            Array.Empty<WorldPackageIssue>());
    }

    private void WriteStaging(string path, PackageContent content)
    {
        using FileStream file = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65_536,
            FileOptions.SequentialScan | FileOptions.WriteThrough);
        using (ZipArchive archive = new(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, WorldPackagePaths.DefinitionEntry, content.DefinitionBytes, WorldPackageFaultPoint.DuringDefinitionEntryWrite);
            WriteEntry(archive, WorldPackagePaths.SnapshotEntry, content.SnapshotBytes, WorldPackageFaultPoint.DuringSnapshotEntryWrite);
            Hit(WorldPackageFaultPoint.BeforeManifestWrite);
            WriteEntry(archive, WorldPackagePaths.ManifestEntry, content.ManifestBytes, null);
        }
        file.Flush(flushToDisk: true);
        if (file.Length > WorldPackageTechnicalLimits.MaxPackageBytes)
            throw new InvalidDataException($"Staging package exceeds {WorldPackageTechnicalLimits.MaxPackageBytes} bytes.");
    }

    private void WriteEntry(ZipArchive archive, string name, byte[] bytes, WorldPackageFaultPoint? faultPoint)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        entry.ExternalAttributes = 0;
        using Stream stream = entry.Open();
        if (faultPoint.HasValue && bytes.Length > 0)
        {
            int first = Math.Max(1, bytes.Length / 2);
            stream.Write(bytes.AsSpan(0, first));
            Hit(faultPoint.Value);
            stream.Write(bytes.AsSpan(first));
        }
        else
        {
            stream.Write(bytes);
        }
    }

    private void RequireExpectedPackage(string path, PackageContent content)
    {
        WorldPackageLoadResult validation = _reader.LoadCandidate(path);
        if (!validation.Success || validation.Document is null)
            throw new InvalidDataException(validation.Issues.Count == 0 ? "Package validation failed." : validation.Issues[0].Detail);
        if (validation.Document.Manifest.PackageIdentityDigest != content.Manifest.PackageIdentityDigest
            || validation.Document.Manifest.Digest != content.Manifest.Digest
            || validation.Document.Snapshot.Digest != content.Snapshot.Digest)
            throw new InvalidDataException("Validated staging semantics do not match the requested snapshot.");
    }

    private static void TryRestorePrevious(string target, string previous)
    {
        try
        {
            if (!File.Exists(previous)) return;
            if (File.Exists(target)) File.Delete(target);
            File.Move(previous, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Preserve both paths for the explicit recovery service.
        }
    }

    private void Hit(WorldPackageFaultPoint point) => _faultInjector?.Invoke(point);

    private static WorldPackageSaveResult Failure(string path, string code, string summary, string detail) =>
        Failure(path, WorldPackageReader.Issue(code, summary, detail));
    private static WorldPackageSaveResult Failure(string path, WorldPackageIssue issue) =>
        new(false, path, string.Empty, string.Empty, 0, Array.AsReadOnly([issue]));
    private static WorldPackageSaveResult AppendIssues(WorldPackageSaveResult result, IReadOnlyList<WorldPackageIssue> issues) => issues.Count == 0
        ? result
        : result with { Issues = Array.AsReadOnly(result.Issues.Concat(issues).ToArray()) };

    private sealed class PackageContent
    {
        private PackageContent(
            WorldSessionSnapshot snapshot,
            byte[] definitionBytes,
            byte[] snapshotBytes,
            WorldPackageManifest manifest,
            byte[] manifestBytes)
        {
            Snapshot = snapshot;
            DefinitionBytes = definitionBytes;
            SnapshotBytes = snapshotBytes;
            Manifest = manifest;
            ManifestBytes = manifestBytes;
        }

        public WorldSessionSnapshot Snapshot { get; }
        public byte[] DefinitionBytes { get; }
        public byte[] SnapshotBytes { get; }
        public WorldPackageManifest Manifest { get; }
        public byte[] ManifestBytes { get; }

        public static PackageContent Create(WorldSessionSnapshot snapshot)
        {
            byte[] definition = WorldPackageJson.SerializeCompact(snapshot.Definition);
            byte[] snapshotBytes = WorldPackageJson.SerializeCompact(snapshot);
            if (definition.Length > WorldPackageTechnicalLimits.MaxDefinitionBytes)
                throw new ArgumentException("Definition JSON exceeds its technical limit.", nameof(snapshot));
            if (snapshotBytes.Length > WorldPackageTechnicalLimits.MaxSnapshotBytes)
                throw new ArgumentException("Snapshot JSON exceeds its technical limit.", nameof(snapshot));
            WorldPackageFileEntry[] entries =
            [
                new(WorldPackagePaths.DefinitionEntry, checked((ulong)definition.Length), Sha256Digest.Compute(definition)),
                new(WorldPackagePaths.SnapshotEntry, checked((ulong)snapshotBytes.Length), Sha256Digest.Compute(snapshotBytes)),
            ];
            WorldPackageManifest manifest = WorldPackageManifest.Create(snapshot, entries);
            byte[] manifestBytes = WorldPackageJson.SerializeCompact(manifest);
            if (manifestBytes.Length > WorldPackageTechnicalLimits.MaxManifestBytes)
                throw new ArgumentException("Package manifest JSON exceeds its technical limit.", nameof(snapshot));
            long total = checked((long)definition.Length + snapshotBytes.Length + manifestBytes.Length);
            if (total > WorldPackageTechnicalLimits.MaxTotalUncompressedBytes)
                throw new ArgumentException("Total uncompressed package content exceeds its technical limit.", nameof(snapshot));
            return new(snapshot, definition, snapshotBytes, manifest, manifestBytes);
        }
    }
}

internal enum WorldPackageFaultPoint
{
    BeforeStagingCreation,
    DuringDefinitionEntryWrite,
    DuringSnapshotEntryWrite,
    BeforeManifestWrite,
    AfterStagingCloseBeforeValidation,
    AfterOldTargetMovedToPrevious,
    AfterWritingPromotedToTarget,
    DuringFinalValidation,
    BeforePreviousDeletion,
}
