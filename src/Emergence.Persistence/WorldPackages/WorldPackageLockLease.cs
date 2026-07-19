using Emergence.Foundation.Text;
using System.Security.Cryptography;

namespace Emergence.Persistence.WorldPackages;

internal sealed class WorldPackageLockLease : IDisposable
{
    private const int SharingViolation = 32;
    private const int LockViolation = 33;
    private const int MetadataLimit = 512;
    private readonly object _releaseGate = new();
    private readonly Func<WorldPackageIssue?>? _cleanupIssueInjector;
    private FileStream? _handle;
    private WorldPackageIssue? _releaseIssue;
    private bool _released;

    private WorldPackageLockLease(
        string targetPath,
        string lockPath,
        string leaseToken,
        FileStream handle,
        Func<WorldPackageIssue?>? cleanupIssueInjector)
    {
        TargetPath = targetPath;
        LockPath = lockPath;
        LeaseToken = leaseToken;
        _handle = handle;
        _cleanupIssueInjector = cleanupIssueInjector;
    }

    internal string TargetPath { get; }
    internal string LockPath { get; }
    internal string LeaseToken { get; }

    internal static bool TryAcquire(
        string target,
        Func<WorldPackageIssue?>? cleanupIssueInjector,
        out WorldPackageLockLease? lease,
        out WorldPackageIssue? acquisitionIssue,
        out WorldPackageIssue? metadataIssue)
    {
        lease = null;
        acquisitionIssue = null;
        metadataIssue = null;
        string lockPath = WorldPackageSidecars.Lock(target);
        if (!ValidateLockPath(lockPath, out acquisitionIssue)) return false;

        FileStream? handle = null;
        try
        {
            handle = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.WriteThrough | FileOptions.DeleteOnClose);

            FileAttributes attributes = File.GetAttributes(lockPath);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                handle.Dispose();
                acquisitionIssue = UnsafePathIssue();
                return false;
            }

            string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
            lease = new(target, lockPath, token, handle, cleanupIssueInjector);
            handle = null;
            metadataIssue = lease.TryWriteMetadata();
            return true;
        }
        catch (IOException exception) when (IsContention(exception))
        {
            handle?.Dispose();
            acquisitionIssue = WorldPackageReader.Issue(
                "world-package.lock-contention",
                "World package is actively locked",
                "Another live exclusive lock lease owns this package target.");
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            handle?.Dispose();
            acquisitionIssue = WorldPackageReader.Issue(
                "world-package.lock-acquisition-unavailable",
                "World package lock acquisition is unavailable",
                "The regular lock rendezvous could not be opened for exclusive ownership.");
            return false;
        }
    }

    internal WorldPackageIssue? Release()
    {
        lock (_releaseGate)
        {
            if (_released) return _releaseIssue;
            _released = true;

            WorldPackageIssue? injected = null;
            try
            {
                injected = _cleanupIssueInjector?.Invoke();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                injected = CleanupIssue();
            }

            try
            {
                _handle?.Dispose();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _releaseIssue = CleanupIssue();
            }
            finally
            {
                _handle = null;
            }

            _releaseIssue ??= injected;
            return _releaseIssue;
        }
    }

    public void Dispose() => Release();

    private WorldPackageIssue? TryWriteMetadata()
    {
        try
        {
            string text =
                "ProjectEmergence.WorldPackageLock.v1\n" +
                $"processId={Environment.ProcessId}\n" +
                $"leaseToken={LeaseToken}\n";
            byte[] bytes = StrictUtf8.GetBytes(text);
            if (bytes.Length > MetadataLimit) throw new InvalidOperationException("Lock metadata exceeded its internal bound.");
            _handle!.SetLength(0);
            _handle.Position = 0;
            _handle.Write(bytes);
            _handle.Flush(flushToDisk: true);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return WorldPackageReader.Issue(
                "world-package.lock-metadata-warning",
                "World package lock metadata was not written",
                "Exclusive ownership remains valid because metadata is nonauthoritative.");
        }
    }

    private static bool ValidateLockPath(string lockPath, out WorldPackageIssue? issue)
    {
        issue = null;
        if (!File.Exists(lockPath) && !Directory.Exists(lockPath)) return true;
        try
        {
            FileAttributes attributes = File.GetAttributes(lockPath);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                issue = UnsafePathIssue();
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issue = WorldPackageReader.Issue(
                "world-package.lock-acquisition-unavailable",
                "World package lock acquisition is unavailable",
                "The lock rendezvous could not be inspected safely.");
            return false;
        }
    }

    private static bool IsContention(IOException exception)
    {
        int error = exception.HResult & 0xffff;
        return error is SharingViolation or LockViolation;
    }

    private static WorldPackageIssue UnsafePathIssue() => WorldPackageReader.Issue(
        "world-package.lock-path-unsafe",
        "Unsafe world package lock path",
        "The lock rendezvous must be an ordinary non-reparse file.");

    private static WorldPackageIssue CleanupIssue() => WorldPackageReader.Issue(
        "world-package.lock-cleanup-warning",
        "World package lock cleanup warned",
        "The package transaction is unchanged; any leftover regular lock file is stale and reacquirable.");
}
