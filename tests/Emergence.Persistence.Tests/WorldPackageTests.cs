using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Emergence.Foundation;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Results;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Time;
using Emergence.Model;
using Emergence.Persistence.WorldPackages;
using Emergence.Simulation;

namespace Emergence.Persistence.Tests;

public sealed class WorldPackageTests
{
    [Fact]
    public void FileEntryAndManifestValidateCopyOrderAndLockedIdentity()
    {
        WorldSessionSnapshot snapshot = Snapshot();
        WorldPackageFileEntry definition = new("definition.json", 10, Sha256Digest.Compute([1]));
        WorldPackageFileEntry state = new("snapshot.json", 20, Sha256Digest.Compute([2]));
        WorldPackageFileEntry[] source = [definition, state];
        WorldPackageManifest manifest = WorldPackageManifest.Create(snapshot, source);
        source[0] = state;
        Assert.Equal("definition.json", manifest.Entries[0].Path);
        Assert.Throws<NotSupportedException>(() => ((IList<WorldPackageFileEntry>)manifest.Entries).Clear());
        Assert.Equal("fcfab8b4e95de5f578330eb0d599e8759ebb62ca6fc37210f36197a88927c3d1", manifest.PackageIdentityDigest.ToString());
        Assert.Throws<ArgumentException>(() => new WorldPackageFileEntry("other.json", 0, default));
        Assert.Throws<ArgumentException>(() => WorldPackageManifest.Create(snapshot, [state, definition]));
        Assert.Throws<ArgumentException>(() => WorldPackageManifest.Create(snapshot, [definition, definition]));
    }

    [Fact]
    public void ManifestRoundTripsAndRejectsUnknownDuplicateAndDigestMismatch()
    {
        WorldPackageManifest manifest = ManifestFor(Snapshot());
        string json = JsonSerializer.Serialize(manifest, JsonDefaults.Compact);
        Assert.Equal(manifest, JsonSerializer.Deserialize<WorldPackageManifest>(json, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldPackageManifest>(json.Replace("\"digest\":", "\"unknown\":0,\"digest\":", StringComparison.Ordinal), JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldPackageManifest>(json.Replace(manifest.Digest.ToString(), new string('f', 64), StringComparison.Ordinal), JsonDefaults.Compact));
    }

    [Fact]
    public void ValidPackageRoundTripsWithExactInventoryOrderTimestampAndNoBom()
    {
        using TempDirectory temp = new();
        string path = temp.Package("valid");
        WorldSessionSnapshot snapshot = Snapshot();
        WorldPackageSaveResult save = new WorldPackageWriter().Save(path, snapshot);
        Assert.True(save.Success);
        WorldPackageLoadResult load = new WorldPackageReader().Load(path);
        Assert.True(load.Success);
        Assert.Equal(snapshot, load.Document!.Snapshot);
        Assert.Equal(snapshot.Definition, load.Document.Definition);
        using ZipArchive archive = ZipFile.OpenRead(path);
        Assert.Equal(["definition.json", "snapshot.json", "package-manifest.json"], archive.Entries.Select(static entry => entry.FullName));
        Assert.All(archive.Entries, static entry => Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), entry.LastWriteTime.DateTime));
        Assert.All(archive.Entries, entry => Assert.False(Read(entry).AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })));
    }

    [Fact]
    public void IdenticalSnapshotsProduceIdenticalBytesEntriesAndSemanticDigests()
    {
        using TempDirectory temp = new();
        WorldSessionSnapshot snapshot = Snapshot();
        string first = temp.Package("first");
        string second = temp.Package("second");
        WorldPackageSaveResult a = new WorldPackageWriter().Save(first, snapshot);
        WorldPackageSaveResult b = new WorldPackageWriter().Save(second, snapshot);
        Assert.True(a.Success && b.Success);
        Assert.Equal(a.PackageIdentityDigest, b.PackageIdentityDigest);
        Assert.Equal(a.ManifestDigest, b.ManifestDigest);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        Assert.Equal(ReadEntries(first), ReadEntries(second));
    }

    [Theory]
    [InlineData("unknown.json", "world-package.entry-unknown")]
    [InlineData("../snapshot.json", "world-package.entry-unknown")]
    [InlineData("C:/snapshot.json", "world-package.entry-unknown")]
    public void UnknownAndTraversalEntriesAreRejected(string name, string code)
    {
        using TempDirectory temp = new();
        string path = temp.Package("unsafe");
        CreateRaw(path, [("definition.json", []), ("snapshot.json", []), (name, [])]);
        WorldPackageLoadResult result = new WorldPackageReader().Load(path);
        Assert.False(result.Success);
        Assert.Equal(code, result.Issues[0].Code);
    }

    [Fact]
    public void DuplicateMissingAndMalformedZipAreRejected()
    {
        using TempDirectory temp = new();
        string duplicate = temp.Package("duplicate");
        CreateRaw(duplicate, [("definition.json", []), ("definition.json", []), ("snapshot.json", [])]);
        Assert.Equal("world-package.entry-duplicate", new WorldPackageReader().Load(duplicate).Issues[0].Code);
        string missing = temp.Package("missing");
        CreateRaw(missing, [("definition.json", []), ("snapshot.json", [])]);
        Assert.Equal("world-package.entry-count", new WorldPackageReader().Load(missing).Issues[0].Code);
        string malformed = temp.Package("malformed");
        File.WriteAllBytes(malformed, [0x50, 0x4b, 0x03]);
        Assert.Equal("world-package.zip-malformed", new WorldPackageReader().Load(malformed).Issues[0].Code);
    }

    [Fact]
    public void InvalidUtf8BomCommentsTrailingCommaAndTypeMetadataFailClosed()
    {
        using TempDirectory temp = new();
        string valid = temp.Package("valid");
        Assert.True(new WorldPackageWriter().Save(valid, Snapshot()).Success);
        foreach ((string suffix, byte[] manifest, string expected) in new[]
        {
            ("invalid", new byte[] { 0xff }, "world-package.utf8-invalid"),
            ("bom", new byte[] { 0xef, 0xbb, 0xbf, (byte)'{', (byte)'}' }, "world-package.utf8-bom"),
            ("comment", Encoding.UTF8.GetBytes("/*x*/{}"), "world-package.json-invalid"),
            ("trailing", Encoding.UTF8.GetBytes("{\"formatVersion\":\"1.0.0\",}"), "world-package.json-invalid"),
            ("type", Encoding.UTF8.GetBytes("{\"$type\":\"System.Object\"}"), "world-package.json-invalid"),
        })
        {
            string path = temp.Package(suffix);
            Rewrite(valid, path, name => name == "package-manifest.json" ? manifest : null);
            WorldPackageLoadResult result = new WorldPackageReader().Load(path);
            Assert.False(result.Success);
            Assert.Equal(expected, result.Issues[0].Code);
        }
    }

    [Fact]
    public void ExcessiveJsonDepthAndHashesAreRejected()
    {
        using TempDirectory temp = new();
        string valid = temp.Package("valid");
        new WorldPackageWriter().Save(valid, Snapshot());
        string deep = temp.Package("deep");
        byte[] depth = Encoding.UTF8.GetBytes(new string('[', WorldPackageTechnicalLimits.MaxJsonDepth + 1) + new string(']', WorldPackageTechnicalLimits.MaxJsonDepth + 1));
        Rewrite(valid, deep, name => name == "package-manifest.json" ? depth : null);
        Assert.Equal("world-package.json-invalid", new WorldPackageReader().Load(deep).Issues[0].Code);
        string definition = temp.Package("definition-hash");
        Rewrite(valid, definition, name => name == "definition.json" ? Encoding.UTF8.GetBytes("{}") : null);
        Assert.Equal("world-package.manifest-length", new WorldPackageReader().Load(definition).Issues[0].Code);
        string snapshot = temp.Package("snapshot-hash");
        Rewrite(valid, snapshot, name => name == "snapshot.json" ? Encoding.UTF8.GetBytes("{}") : null);
        Assert.Equal("world-package.manifest-length", new WorldPackageReader().Load(snapshot).Issues[0].Code);
    }

    [Fact]
    public void ManifestIdentityDigestAndCrossDocumentMismatchAreRejected()
    {
        using TempDirectory temp = new();
        string valid = temp.Package("valid");
        new WorldPackageWriter().Save(valid, Snapshot());
        Dictionary<string, byte[]> entries = ReadEntries(valid);
        WorldPackageManifest original = JsonSerializer.Deserialize<WorldPackageManifest>(entries["package-manifest.json"], JsonDefaults.Compact)!;
        JsonObject identity = JsonNode.Parse(entries["package-manifest.json"])!.AsObject();
        identity["packageIdentityDigest"] = new string('f', 64);
        string identityPath = temp.Package("identity");
        Rewrite(valid, identityPath, name => name == "package-manifest.json" ? Encoding.UTF8.GetBytes(identity.ToJsonString()) : null);
        Assert.Equal("world-package.digest-mismatch", new WorldPackageReader().Load(identityPath).Issues[0].Code);

        WorldId otherWorld = WorldId.FromUInt64(99);
        Sha256Digest packageIdentity = ComputePackageIdentity(otherWorld, original);
        Sha256Digest manifestDigest = ComputeManifestDigest(packageIdentity, original.Entries);
        WorldPackageManifest mismatched = WorldPackageManifest.CreateValidated(
            original.FormatVersion, otherWorld, original.BranchId, original.SessionDefinitionDigest, original.SnapshotDigest,
            original.StateDigest, original.RulesetRegistryDigest, original.RuntimeAlgorithmCatalogDigest, packageIdentity,
            original.Entries, manifestDigest);
        string cross = temp.Package("cross");
        Rewrite(valid, cross, name => name == "package-manifest.json" ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(mismatched, JsonDefaults.Compact)) : null);
        Assert.Equal("world-package.manifest-mismatch", new WorldPackageReader().Load(cross).Issues[0].Code);
    }

    [Fact]
    public void PackageAndEntryLimitsAreCheckedBeforeReading()
    {
        using TempDirectory temp = new();
        string oversized = temp.Package("oversized");
        using (FileStream stream = new(oversized, FileMode.CreateNew)) stream.SetLength(WorldPackageTechnicalLimits.MaxPackageBytes + 1);
        Assert.Equal("world-package.size-limit", new WorldPackageReader().Load(oversized).Issues[0].Code);
        string entry = temp.Package("entry");
        byte[] tooLarge = new byte[WorldPackageTechnicalLimits.MaxManifestBytes + 1];
        CreateRaw(entry, [("definition.json", []), ("snapshot.json", []), ("package-manifest.json", tooLarge)], CompressionLevel.Optimal);
        Assert.Equal("world-package.entry-size-limit", new WorldPackageReader().Load(entry).Issues[0].Code);
        Assert.Equal(WorldPackageTechnicalLimits.MaxTotalUncompressedBytes,
            (long)WorldPackageTechnicalLimits.MaxManifestBytes + WorldPackageTechnicalLimits.MaxDefinitionBytes + WorldPackageTechnicalLimits.MaxSnapshotBytes);
    }

    [Fact]
    public void InvalidExtensionMissingParentDirectoryAndReparseAreRejected()
    {
        using TempDirectory temp = new();
        WorldPackageWriter writer = new();
        Assert.False(writer.Save(Path.Combine(temp.Path, "bad.zip"), Snapshot()).Success);
        Assert.False(writer.Save(Path.Combine(temp.Path, "missing", "bad.emergence-world"), Snapshot()).Success);
        string directory = temp.Package("directory");
        Directory.CreateDirectory(directory);
        Assert.False(new WorldPackageReader().Load(directory).Success);
    }

    [Fact]
    public void LockedFileReturnsStructuredFailure()
    {
        using TempDirectory temp = new();
        string path = temp.Package("locked");
        new WorldPackageWriter().Save(path, Snapshot());
        using FileStream held = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        WorldPackageLoadResult result = new WorldPackageReader().Load(path);
        Assert.False(result.Success);
        Assert.Equal("world-package.unavailable", result.Issues[0].Code);
    }

    [Fact]
    public void NewSaveAndReplacementLeaveNoSidecarsAndPreserveLatestSnapshot()
    {
        using TempDirectory temp = new();
        string path = temp.Package("replace");
        WorldSession session = TickTwoSession();
        WorldSessionSnapshot first = session.CaptureSnapshot().Value;
        Assert.True(new WorldPackageWriter().Save(path, first).Success);
        SubmitFuture(session);
        WorldSessionSnapshot second = session.CaptureSnapshot().Value;
        Assert.True(new WorldPackageWriter().Save(path, second).Success);
        Assert.Equal(second.Digest, new WorldPackageReader().Load(path).Document!.Snapshot.Digest);
        Assert.All(Sidecars(path), sidecar => Assert.False(File.Exists(sidecar)));
    }

    [Fact]
    public void InvalidExistingTargetIsNotSilentlyOverwritten()
    {
        using TempDirectory temp = new();
        string path = temp.Package("invalid");
        byte[] invalid = [1, 2, 3];
        File.WriteAllBytes(path, invalid);
        WorldPackageSaveResult result = new WorldPackageWriter().Save(path, Snapshot());
        Assert.False(result.Success);
        Assert.Equal(invalid, File.ReadAllBytes(path));
    }

    [Fact]
    public void LockContentionPreventsInterleavedWriter()
    {
        using TempDirectory temp = new();
        string path = temp.Package("locked-save");
        using FileStream held = new(path + ".lock", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        WorldPackageSaveResult result = new WorldPackageWriter().Save(path, Snapshot());
        Assert.False(result.Success);
        Assert.Equal("world-package.lock-contention", result.Issues[0].Code);
    }

    public static TheoryData<string> FaultPoints
    {
        get
        {
            TheoryData<string> data = new();
            foreach (WorldPackageFaultPoint point in Enum.GetValues<WorldPackageFaultPoint>()) data.Add(point.ToString());
            return data;
        }
    }

    [Theory, MemberData(nameof(FaultPoints))]
    public void EveryInjectedAtomicFailurePreservesOriginalAndIsRecoverable(string pointName)
    {
        WorldPackageFaultPoint point = Enum.Parse<WorldPackageFaultPoint>(pointName);
        using TempDirectory temp = new();
        string path = temp.Package("fault");
        WorldSession session = TickTwoSession();
        WorldSessionSnapshot original = session.CaptureSnapshot().Value;
        Assert.True(new WorldPackageWriter().Save(path, original).Success);
        SubmitFuture(session);
        WorldSessionSnapshot replacement = session.CaptureSnapshot().Value;
        WorldPackageWriter writer = new(hit => { if (hit == point) throw new IOException("Injected failure."); });
        WorldPackageSaveResult result = writer.Save(path, replacement);
        Assert.False(result.Success);
        WorldPackageLoadResult load = new WorldPackageReader().Load(path);
        Assert.True(load.Success);
        Assert.Equal(original.Digest, load.Document!.Snapshot.Digest);
        RecoveryResult recovery = new WorldPackageRecovery().Recover(path);
        Assert.True(recovery.Success);
        Assert.True(new WorldPackageReader().Load(path).Success);
        Assert.True(WorldPackageLockLease.TryAcquire(path, null, out WorldPackageLockLease? lease, out _, out _));
        Assert.Null(lease!.Release());
        Assert.All([path + ".writing", path + ".previous", path + ".lock"], sidecar => Assert.False(File.Exists(sidecar)));
    }

    [Fact]
    public void RecoveryValidTargetCleansStaleWritingAndPreviousButLeavesCorruptEvidence()
    {
        using TempDirectory temp = new();
        string source = temp.Package("source");
        new WorldPackageWriter().Save(source, Snapshot());
        string target = temp.Package("target");
        File.Copy(source, target);
        File.Copy(source, target + ".writing");
        File.Copy(source, target + ".previous");
        File.WriteAllBytes(target + ".corrupt", [9]);
        RecoveryResult result = new WorldPackageRecovery().Recover(target);
        Assert.True(result.Success);
        Assert.False(File.Exists(target + ".writing"));
        Assert.False(File.Exists(target + ".previous"));
        Assert.True(File.Exists(target + ".corrupt"));
    }

    [Theory]
    [InlineData("writing")]
    [InlineData("previous")]
    public void RecoveryPromotesMissingTargetCandidate(string candidate)
    {
        using TempDirectory temp = new();
        string source = temp.Package("source");
        new WorldPackageWriter().Save(source, Snapshot());
        string target = temp.Package("target");
        File.Copy(source, target + "." + candidate);
        RecoveryResult result = new WorldPackageRecovery().Recover(target);
        Assert.True(result.Success);
        Assert.True(new WorldPackageReader().Load(target).Success);
    }

    [Theory]
    [InlineData("previous")]
    [InlineData("writing")]
    public void RecoveryQuarantinesInvalidTargetAndUsesValidCandidate(string candidate)
    {
        using TempDirectory temp = new();
        string source = temp.Package("source");
        new WorldPackageWriter().Save(source, Snapshot());
        string target = temp.Package("target");
        File.WriteAllBytes(target, [1, 2, 3]);
        File.Copy(source, target + "." + candidate);
        RecoveryResult result = new WorldPackageRecovery().Recover(target);
        Assert.True(result.Success);
        Assert.True(File.Exists(target + ".corrupt"));
        Assert.True(new WorldPackageReader().Load(target).Success);
    }

    [Fact]
    public void RecoveryFailsWithoutValidCandidateAndPreservesEvidence()
    {
        using TempDirectory temp = new();
        string target = temp.Package("target");
        File.WriteAllBytes(target, [1]);
        File.WriteAllBytes(target + ".writing", [2]);
        RecoveryResult result = new WorldPackageRecovery().Recover(target);
        Assert.False(result.Success);
        Assert.True(File.Exists(target));
        Assert.True(File.Exists(target + ".writing"));
    }

    [Fact]
    public void RecoveryIgnoresTimestampsAndSelectsWritingBeforePrevious()
    {
        using TempDirectory temp = new();
        WorldSession session = TickTwoSession();
        WorldSessionSnapshot writingSnapshot = session.CaptureSnapshot().Value;
        string writingSource = temp.Package("writing-source");
        new WorldPackageWriter().Save(writingSource, writingSnapshot);
        SubmitFuture(session);
        string previousSource = temp.Package("previous-source");
        new WorldPackageWriter().Save(previousSource, session.CaptureSnapshot().Value);
        string target = temp.Package("target");
        File.Copy(writingSource, target + ".writing");
        File.Copy(previousSource, target + ".previous");
        File.SetLastWriteTimeUtc(target + ".writing", new DateTime(2000, 1, 1));
        File.SetLastWriteTimeUtc(target + ".previous", new DateTime(2030, 1, 1));
        Assert.True(new WorldPackageRecovery().Recover(target).Success);
        Assert.Equal(writingSnapshot.Digest, new WorldPackageReader().Load(target).Document!.Snapshot.Digest);
    }

    [Fact]
    public void CorruptSidecarConflictFailsSafely()
    {
        using TempDirectory temp = new();
        string source = temp.Package("source");
        new WorldPackageWriter().Save(source, Snapshot());
        string target = temp.Package("target");
        File.WriteAllBytes(target, [1]);
        File.Copy(source, target + ".previous");
        File.WriteAllBytes(target + ".corrupt", [9]);
        RecoveryResult result = new WorldPackageRecovery().Recover(target);
        Assert.False(result.Success);
        Assert.True(File.Exists(target));
        Assert.True(File.Exists(target + ".previous"));
    }

    [Fact]
    public void RecoveryActionJsonIsExactAndDeterministic()
    {
        using TempDirectory temp = new();
        string source = temp.Package("source");
        new WorldPackageWriter().Save(source, Snapshot());
        string target = temp.Package("target");
        File.Copy(source, target + ".writing");
        RecoveryResult first = new WorldPackageRecovery().Recover(target);
        string json = JsonDefaults.Serialize(first, false);
        Assert.Contains("\"kind\":\"WritingValidated\"", json, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecoveryActionKind>("0", JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecoveryActionKind>("\"writingvalidated\"", JsonDefaults.Compact));
    }

    private static WorldPackageManifest ManifestFor(WorldSessionSnapshot snapshot)
    {
        byte[] definition = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot.Definition, JsonDefaults.Compact));
        byte[] state = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, JsonDefaults.Compact));
        return WorldPackageManifest.Create(snapshot,
        [
            new("definition.json", (ulong)definition.Length, Sha256Digest.Compute(definition)),
            new("snapshot.json", (ulong)state.Length, Sha256Digest.Compute(state)),
        ]);
    }

    private static WorldSessionSnapshot Snapshot() => TickTwoSession().CaptureSnapshot().Value;

    private static WorldSession TickTwoSession()
    {
        WorldSession session = FoundationSessionFixture.CreatePhase05PausedSession(new RulesetRegistry([FoundationReferenceRuleset.Create()]));
        Submit(session, 0, "gamma");
        Submit(session, 1, "alpha");
        Submit(session, 0, "delta");
        Submit(session, 1, "beta");
        session.Resume();
        Assert.True(session.StepOneTick().Success);
        Assert.True(session.StepOneTick().Success);
        session.Pause();
        return session;
    }

    private static void Submit(WorldSession session, ulong tick, string value) =>
        Assert.True(session.SubmitCommand(new(new(tick), new(FoundationSessionFixture.TraceCommandType), FoundationSessionFixture.TracePayload(value))).Success);
    private static void SubmitFuture(WorldSession session) => Submit(session, 4, "future");

    private static IReadOnlyList<string> Sidecars(string target) =>
        [target + ".writing", target + ".previous", target + ".lock", target + ".corrupt"];

    private static Dictionary<string, byte[]> ReadEntries(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        return archive.Entries.ToDictionary(static entry => entry.FullName, Read, StringComparer.Ordinal);
    }

    private static byte[] Read(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static void Rewrite(string source, string target, Func<string, byte[]?> replacement)
    {
        Dictionary<string, byte[]> entries = ReadEntries(source);
        CreateRaw(target, entries.Select(pair => (pair.Key, replacement(pair.Key) ?? pair.Value)).ToArray());
    }

    private static void CreateRaw(string path, IReadOnlyList<(string Name, byte[] Bytes)> entries, CompressionLevel compression = CompressionLevel.NoCompression)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string name, byte[] bytes) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, compression);
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using Stream stream = entry.Open();
            stream.Write(bytes);
        }
    }

    private static Sha256Digest ComputePackageIdentity(WorldId worldId, WorldPackageManifest original)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(WorldPackageManifest.IdentityDigestDomainMarker);
        writer.WriteString(original.FormatVersion.ToString());
        writer.WriteString(worldId.ToString());
        writer.WriteString(original.BranchId.ToString());
        writer.WriteDigest(original.SessionDefinitionDigest);
        writer.WriteDigest(original.SnapshotDigest);
        writer.WriteDigest(original.StateDigest);
        writer.WriteDigest(original.RulesetRegistryDigest);
        writer.WriteDigest(original.RuntimeAlgorithmCatalogDigest);
        writer.WriteUInt64(2);
        writer.WriteString("definition.json");
        writer.WriteString("snapshot.json");
        return writer.FinalizeDigest();
    }

    private static Sha256Digest ComputeManifestDigest(Sha256Digest packageIdentity, IReadOnlyList<WorldPackageFileEntry> entries)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(WorldPackageManifest.ManifestDigestDomainMarker);
        writer.WriteString("1.0.0");
        writer.WriteDigest(packageIdentity);
        writer.WriteUInt64((ulong)entries.Count);
        foreach (WorldPackageFileEntry entry in entries)
        {
            writer.WriteString(entry.Path);
            writer.WriteUInt64(entry.UncompressedByteLength);
            writer.WriteDigest(entry.Sha256);
        }
        return writer.FinalizeDigest();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Emergence.WorldPackage.Tests", Guid.NewGuid().ToString("N"));
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
