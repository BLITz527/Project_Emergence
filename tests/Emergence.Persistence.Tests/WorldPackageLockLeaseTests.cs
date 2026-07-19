using System.Text;
using Emergence.Foundation.Rulesets;
using Emergence.Model;
using Emergence.Persistence.WorldPackages;
using Emergence.Simulation;

namespace Emergence.Persistence.Tests;

public sealed class WorldPackageLockLeaseTests
{
    [Fact]
    public void StaleEmptyLockDoesNotBlockSave()
    {
        using TempDirectory temp = new();
        string target = temp.Package("save-empty");
        File.WriteAllBytes(target + ".lock", []);

        WorldPackageSaveResult result = new WorldPackageWriter().Save(target, Snapshot());

        Assert.True(result.Success);
        Assert.True(new WorldPackageReader().Load(target).Success);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void StaleArbitraryMetadataDoesNotBlockSave()
    {
        using TempDirectory temp = new();
        string target = temp.Package("save-arbitrary");
        File.WriteAllBytes(target + ".lock", [0xff, 0x00, 0x80, 0x01, 0x7f]);

        WorldPackageSaveResult result = new WorldPackageWriter().Save(target, Snapshot());

        Assert.True(result.Success);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void StaleEmptyLockDoesNotBlockRecoverWithValidTarget()
    {
        using TempDirectory temp = new();
        string target = temp.Package("recover-empty");
        Assert.True(new WorldPackageWriter().Save(target, Snapshot()).Success);
        File.WriteAllBytes(target + ".lock", []);

        RecoveryResult result = new WorldPackageRecovery().Recover(target);

        Assert.True(result.Success);
        Assert.Contains(result.Actions, static action => action.Kind == RecoveryActionKind.TargetValidated);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void StaleTruncatedMetadataDoesNotBlockRecover()
    {
        using TempDirectory temp = new();
        string target = temp.Package("recover-truncated");
        Assert.True(new WorldPackageWriter().Save(target, Snapshot()).Success);
        File.WriteAllBytes(target + ".lock", Encoding.UTF8.GetBytes("ProjectEmergence.WorldPackageLock.v1\nprocessId="));

        RecoveryResult result = new WorldPackageRecovery().Recover(target);

        Assert.True(result.Success);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Theory]
    [InlineData("writing")]
    [InlineData("previous")]
    public void StaleLockWithMissingTargetAppliesRecoveryCandidateTable(string candidate)
    {
        using TempDirectory temp = new();
        string source = temp.Package("source-" + candidate);
        Assert.True(new WorldPackageWriter().Save(source, Snapshot()).Success);
        string target = temp.Package("missing-" + candidate);
        File.Copy(source, target + "." + candidate);
        File.WriteAllBytes(target + ".lock", [0x01, 0x02]);

        RecoveryResult result = new WorldPackageRecovery().Recover(target);

        Assert.True(result.Success);
        Assert.True(new WorldPackageReader().Load(target).Success);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Theory]
    [InlineData("previous")]
    [InlineData("writing")]
    public void StaleLockWithInvalidTargetQuarantinesAndUsesValidCandidate(string candidate)
    {
        using TempDirectory temp = new();
        string source = temp.Package("source-invalid-" + candidate);
        Assert.True(new WorldPackageWriter().Save(source, Snapshot()).Success);
        string target = temp.Package("invalid-" + candidate);
        File.WriteAllBytes(target, [0x01, 0x02, 0x03]);
        File.Copy(source, target + "." + candidate);
        File.WriteAllBytes(target + ".lock", [0xff]);

        RecoveryResult result = new WorldPackageRecovery().Recover(target);

        Assert.True(result.Success);
        Assert.True(new WorldPackageReader().Load(target).Success);
        Assert.True(File.Exists(target + ".corrupt"));
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void ActiveExclusiveOwnerBlocksSaveWithoutMutatingPackageOrSidecars()
    {
        using TempDirectory temp = new();
        string target = temp.Package("active-save");
        Assert.True(new WorldPackageWriter().Save(target, Snapshot()).Success);
        File.WriteAllBytes(target + ".writing", [0x10]);
        File.WriteAllBytes(target + ".previous", [0x20]);
        byte[] targetBefore = File.ReadAllBytes(target);
        byte[] writingBefore = File.ReadAllBytes(target + ".writing");
        byte[] previousBefore = File.ReadAllBytes(target + ".previous");
        using FileStream owner = new(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        WorldPackageSaveResult result = new WorldPackageWriter().Save(target, Snapshot());

        Assert.False(result.Success);
        Assert.Equal("world-package.lock-contention", result.Issues.Single().Code);
        Assert.Equal(targetBefore, File.ReadAllBytes(target));
        Assert.Equal(writingBefore, File.ReadAllBytes(target + ".writing"));
        Assert.Equal(previousBefore, File.ReadAllBytes(target + ".previous"));
    }

    [Fact]
    public void ActiveExclusiveOwnerBlocksRecoverWithoutMutatingPackageOrSidecars()
    {
        using TempDirectory temp = new();
        string target = temp.Package("active-recover");
        Assert.True(new WorldPackageWriter().Save(target, Snapshot()).Success);
        File.WriteAllBytes(target + ".writing", [0x10]);
        File.WriteAllBytes(target + ".previous", [0x20]);
        byte[] targetBefore = File.ReadAllBytes(target);
        byte[] writingBefore = File.ReadAllBytes(target + ".writing");
        byte[] previousBefore = File.ReadAllBytes(target + ".previous");
        using FileStream owner = new(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        RecoveryResult result = new WorldPackageRecovery().Recover(target);

        Assert.False(result.Success);
        Assert.Equal("world-package.lock-contention", result.Issues.Single().Code);
        Assert.Equal(targetBefore, File.ReadAllBytes(target));
        Assert.Equal(writingBefore, File.ReadAllBytes(target + ".writing"));
        Assert.Equal(previousBefore, File.ReadAllBytes(target + ".previous"));
    }

    [Fact]
    public void ReleasedOwnerWithRemainingLockPathAllowsSaveReacquisition()
    {
        using TempDirectory temp = new();
        string target = temp.Package("released-save");
        using (FileStream owner = new(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(new WorldPackageWriter().Save(target, Snapshot()).Success);
        }
        Assert.True(File.Exists(target + ".lock"));

        WorldPackageSaveResult result = new WorldPackageWriter().Save(target, Snapshot());

        Assert.True(result.Success);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void ReleasedOwnerWithRemainingLockPathAllowsRecoverReacquisition()
    {
        using TempDirectory temp = new();
        string target = temp.Package("released-recover");
        Assert.True(new WorldPackageWriter().Save(target, Snapshot()).Success);
        using (FileStream owner = new(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(new WorldPackageRecovery().Recover(target).Success);
        }
        Assert.True(File.Exists(target + ".lock"));

        RecoveryResult result = new WorldPackageRecovery().Recover(target);

        Assert.True(result.Success);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public async Task CoordinatedAcquisitionsProduceExactlyOneOwnerWithoutTimingSleeps()
    {
        using TempDirectory temp = new();
        string target = temp.Package("coordinated");
        using CountdownEvent ready = new(2);
        using ManualResetEventSlim start = new(false);
        using CountdownEvent attempted = new(2);
        using ManualResetEventSlim release = new(false);
        bool[] acquired = new bool[2];
        string?[] issueCodes = new string?[2];

        Task[] tasks = Enumerable.Range(0, 2).Select(index => Task.Run(() =>
        {
            ready.Signal();
            Assert.True(start.Wait(TimeSpan.FromSeconds(10)));
            acquired[index] = WorldPackageLockLease.TryAcquire(
                target, null, out WorldPackageLockLease? lease, out WorldPackageIssue? issue, out _);
            issueCodes[index] = issue?.Code;
            attempted.Signal();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            lease?.Release();
        })).ToArray();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
        start.Set();
        Assert.True(attempted.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, acquired.Count(static value => value));
        Assert.Equal(1, issueCodes.Count(static code => code == "world-package.lock-contention"));
        release.Set();
        await Task.WhenAll(tasks);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void ReleasedLeaseCannotDeleteSuccessorLease()
    {
        using TempDirectory temp = new();
        string target = temp.Package("successor");
        Assert.True(WorldPackageLockLease.TryAcquire(target, null, out WorldPackageLockLease? first, out _, out _));
        Assert.Null(first!.Release());
        Assert.True(WorldPackageLockLease.TryAcquire(target, null, out WorldPackageLockLease? successor, out _, out _));

        Assert.Null(first.Release());
        Assert.False(WorldPackageLockLease.TryAcquire(target, null, out _, out WorldPackageIssue? contention, out _));
        Assert.Equal("world-package.lock-contention", contention!.Code);
        Assert.Null(successor!.Release());
    }

    [Fact]
    public void CommittedSaveCleanupWarningPreservesSuccessIdentityManifestAndBytes()
    {
        using TempDirectory temp = new();
        string target = temp.Package("save-warning");
        WorldPackageWriter writer = new(
            faultInjector: null,
            lockCleanupIssueInjector: static () => new(
                "world-package.lock-cleanup-warning",
                "Injected cleanup warning",
                "Deterministic test seam."));

        WorldPackageSaveResult result = writer.Save(target, Snapshot());

        Assert.True(result.Success);
        Assert.NotEmpty(result.PackageIdentityDigest);
        Assert.NotEmpty(result.ManifestDigest);
        Assert.True(result.PackageBytes > 0);
        Assert.Equal("world-package.lock-cleanup-warning", result.Issues.Single().Code);
        Assert.True(new WorldPackageReader().Load(target).Success);
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void SuccessfulRecoveryCleanupWarningPreservesRecoveredStatus()
    {
        using TempDirectory temp = new();
        string source = temp.Package("recovery-warning-source");
        Assert.True(new WorldPackageWriter().Save(source, Snapshot()).Success);
        string target = temp.Package("recovery-warning");
        File.Copy(source, target + ".writing");
        WorldPackageRecovery recovery = new(static () => new(
            "world-package.lock-cleanup-warning",
            "Injected cleanup warning",
            "Deterministic test seam."));

        RecoveryResult result = recovery.Recover(target);

        Assert.True(result.Success);
        Assert.Contains(result.Actions, static action => action.Kind == RecoveryActionKind.WritingPromoted);
        Assert.Equal("world-package.lock-cleanup-warning", result.Issues.Single().Code);
        Assert.True(new WorldPackageReader().Load(target).Success);
    }

    [Fact]
    public void LeftoverRegularLockFileRemainsReacquirable()
    {
        using TempDirectory temp = new();
        string target = temp.Package("leftover");
        File.WriteAllBytes(target + ".lock", [0xff, 0xfe, 0xfd]);

        Assert.True(WorldPackageLockLease.TryAcquire(target, null, out WorldPackageLockLease? lease, out _, out _));

        Assert.Null(lease!.Release());
        Assert.False(File.Exists(target + ".lock"));
    }

    [Fact]
    public void UnsafeDirectoryLockPathFailsClosedForSaveAndRecover()
    {
        using TempDirectory temp = new();
        string target = temp.Package("directory-lock");
        Assert.True(new WorldPackageWriter().Save(target, Snapshot()).Success);
        Directory.CreateDirectory(target + ".lock");

        WorldPackageSaveResult save = new WorldPackageWriter().Save(target, Snapshot());
        RecoveryResult recover = new WorldPackageRecovery().Recover(target);

        Assert.False(save.Success);
        Assert.Equal("world-package.lock-path-unsafe", save.Issues.Single().Code);
        Assert.False(recover.Success);
        Assert.Equal("world-package.lock-path-unsafe", recover.Issues.Single().Code);
    }

    [Fact]
    public void UnsafeReparseLockPathFailsClosedWhenPlatformCanCreateLink()
    {
        using TempDirectory temp = new();
        string target = temp.Package("reparse-lock");
        Assert.True(new WorldPackageWriter().Save(target, Snapshot()).Success);
        string rendezvousTarget = Path.Combine(temp.Path, "ordinary-lock-target");
        File.WriteAllBytes(rendezvousTarget, []);
        try
        {
            File.CreateSymbolicLink(target + ".lock", rendezvousTarget);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.False(File.Exists(target + ".lock"));
            return;
        }

        WorldPackageSaveResult save = new WorldPackageWriter().Save(target, Snapshot());
        RecoveryResult recover = new WorldPackageRecovery().Recover(target);

        Assert.False(save.Success);
        Assert.Equal("world-package.lock-path-unsafe", save.Issues.Single().Code);
        Assert.False(recover.Success);
        Assert.Equal("world-package.lock-path-unsafe", recover.Issues.Single().Code);
        Assert.True(File.Exists(rendezvousTarget));
    }

    [Fact]
    public void UnavailableRegularLockPathIsDistinctFromActiveContention()
    {
        using TempDirectory temp = new();
        string target = temp.Package("unavailable-lock");
        string lockPath = target + ".lock";
        File.WriteAllBytes(lockPath, []);
        File.SetAttributes(lockPath, FileAttributes.ReadOnly);
        try
        {
            WorldPackageSaveResult result = new WorldPackageWriter().Save(target, Snapshot());

            Assert.False(result.Success);
            Assert.Equal("world-package.lock-acquisition-unavailable", result.Issues.Single().Code);
        }
        finally
        {
            File.SetAttributes(lockPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public void CrashLikeStaleLockDoesNotHideOrDeleteRecoveryEvidence()
    {
        using TempDirectory temp = new();
        string target = temp.Package("preserve-evidence");
        File.WriteAllBytes(target, [0x01]);
        File.WriteAllBytes(target + ".writing", [0x02]);
        File.WriteAllBytes(target + ".previous", [0x03]);
        File.WriteAllBytes(target + ".lock", [0x04]);

        RecoveryResult result = new WorldPackageRecovery().Recover(target);

        Assert.False(result.Success);
        Assert.True(File.Exists(target));
        Assert.True(File.Exists(target + ".writing"));
        Assert.True(File.Exists(target + ".previous"));
        Assert.False(File.Exists(target + ".lock"));
    }

    private static WorldSessionSnapshot Snapshot()
    {
        WorldSession session = FoundationSessionFixture.CreatePhase05PausedSession(
            new RulesetRegistry([FoundationReferenceRuleset.Create()]));
        Submit(session, 0, "gamma");
        Submit(session, 1, "alpha");
        Submit(session, 0, "delta");
        Submit(session, 1, "beta");
        session.Resume();
        Assert.True(session.StepOneTick().Success);
        Assert.True(session.StepOneTick().Success);
        session.Pause();
        return session.CaptureSnapshot().Value;
    }

    private static void Submit(WorldSession session, ulong tick, string value) =>
        Assert.True(session.SubmitCommand(new(
            new(tick),
            new(FoundationSessionFixture.TraceCommandType),
            FoundationSessionFixture.TracePayload(value))).Success);

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Emergence.WorldPackage.LockLease.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public string Package(string name) => System.IO.Path.Combine(Path, name + ".emergence-world");

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
