using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emergence.Cli;
using Emergence.Foundation;
using Emergence.Persistence.WorldPackages;

namespace Emergence.ReviewPack.Tests;

public sealed class Phase05EvidenceTests
{
    [Fact]
    public void WorldPackageEvidenceIsAllowedWhileNestedArchivesRemainRejected()
    {
        Assert.False(ReviewPackFilters.IsProhibitedRelativePath("cli/foundation-session.emergence-world"));
        Assert.True(ReviewPackFilters.IsProhibitedRelativePath("cli/nested.zip"));
        Assert.True(ReviewPackFilters.IsProhibitedRelativePath("cli/nested.tar"));
    }

    [Fact]
    public void CompletePersistenceEvidencePassesIndependentValidation()
    {
        using Fixture fixture = new();
        Assert.Equal(EvidenceStatus.Passed, Phase05EvidenceValidator.Evaluate(fixture.Root).Status);
    }

    [Theory]
    [InlineData("self-test-vector")]
    [InlineData("arbitrary-renamed-zip")]
    [InlineData("extra-entry")]
    [InlineData("extracted-snapshot")]
    [InlineData("inventory-hash")]
    [InlineData("verify-digest")]
    [InlineData("recovery-report")]
    [InlineData("source-app-round-trip")]
    [InlineData("packaged-round-trip")]
    public void TamperedPersistenceClaimsFailClosed(string mutation)
    {
        using Fixture fixture = new();
        fixture.Mutate(mutation);
        PersistenceEvidence evidence = Phase05EvidenceValidator.Evaluate(fixture.Root);
        Assert.Equal(EvidenceStatus.Failed, evidence.Status);
        Assert.NotEmpty(evidence.Detail);
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly JsonSerializerOptions Indented = JsonDefaults.Indented;

        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "emergence-phase05-review-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            foreach (string directory in new[] { "cli", "persistence", "app", "package" }) Directory.CreateDirectory(PathOf(directory));

            PersistenceSelfTestReport report = PersistenceSelfTest.Run();
            Assert.True(report.Success);
            WriteJson("cli/persistence-self-test.json", report);
            Write("cli/persistence-self-test.log", "success");

            string package = PathOf("cli/foundation-session.emergence-world");
            WorldPackageSaveResult save = new WorldPackageWriter().Save(package, PersistenceSelfTest.CreateFixtureSnapshot());
            Assert.True(save.Success);
            WriteJson("cli/world-package-fixture.json", save);
            Write("cli/world-package-fixture.log", "success");
            WorldPackageLoadResult load = new WorldPackageReader().Load(package);
            Assert.True(load.Success);
            WorldPackageDocument document = load.Document!;
            WriteJson("cli/world-package-verify.json", new
            {
                success = true,
                packagePath = package,
                packageIdentityDigest = document.Manifest.PackageIdentityDigest.ToString(),
                manifestDigest = document.Manifest.Digest.ToString(),
                snapshotDigest = document.Snapshot.Digest.ToString(),
                stateDigest = document.Snapshot.StateDigest.ToString(),
                issues = Array.Empty<object>(),
            });
            Write("cli/world-package-verify.log", "success");
            WriteJson("cli/world-package-recover.json", new WorldPackageRecovery().Recover(package));
            Write("cli/world-package-recover.log", "success");
            Extract(package);
            WriteDoctor("app/doctor.json");
            WriteDoctor("package/packaged-doctor.json");
        }

        public string Root { get; }

        public void Mutate(string mutation)
        {
            switch (mutation)
            {
                case "self-test-vector":
                    Replace("cli/persistence-self-test.json", Phase05EvidenceValidator.SnapshotDigest, new string('0', 64));
                    break;
                case "arbitrary-renamed-zip":
                    CreateArbitraryZip(extra: false);
                    break;
                case "extra-entry":
                    CreateArbitraryZip(extra: true);
                    break;
                case "extracted-snapshot":
                    Write("persistence/snapshot.json", "{}");
                    break;
                case "inventory-hash":
                    TamperFirstInventoryHash();
                    break;
                case "verify-digest":
                    Replace("cli/world-package-verify.json", Phase05EvidenceValidator.PackageIdentityDigest, new string('a', 64));
                    break;
                case "recovery-report":
                    Write("cli/world-package-recover.json", "{\"success\":false}");
                    break;
                case "source-app-round-trip":
                    Replace("app/doctor.json", "persistence.round-trip", "persistence.round-trip-tampered");
                    break;
                case "packaged-round-trip":
                    Replace("package/packaged-doctor.json", "persistence.sidecars", "persistence.sidecars-tampered");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        }

        private void Extract(string package)
        {
            List<object> inventory = [];
            using ZipArchive archive = ZipFile.OpenRead(package);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream input = entry.Open();
                using MemoryStream output = new();
                input.CopyTo(output);
                byte[] bytes = output.ToArray();
                File.WriteAllBytes(PathOf("persistence/" + entry.FullName), bytes);
                inventory.Add(new { path = entry.FullName, length = bytes.LongLength, sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
            }
            WriteJson("persistence/package-inventory.json", inventory);
        }

        private void CreateArbitraryZip(bool extra)
        {
            string package = PathOf("cli/foundation-session.emergence-world");
            File.Delete(package);
            using ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create);
            foreach (string name in new[] { "definition.json", "snapshot.json", "package-manifest.json" })
            {
                using StreamWriter writer = new(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write("{}");
            }
            if (extra)
            {
                using StreamWriter writer = new(archive.CreateEntry("extra.json").Open(), new UTF8Encoding(false));
                writer.Write("{}");
            }
        }

        private void WriteDoctor(string relative) => WriteJson(relative, new
        {
            success = true,
            checks = new[]
            {
                new { id = "persistence.round-trip", severity = "Success" },
                new { id = "persistence.rng-continuation", severity = "Success" },
                new { id = "persistence.sidecars", severity = "Success" },
            },
        });

        private void Replace(string relative, string oldValue, string newValue) =>
            Write(relative, File.ReadAllText(PathOf(relative)).Replace(oldValue, newValue, StringComparison.Ordinal));

        private void TamperFirstInventoryHash()
        {
            const string marker = "\"sha256\": \"";
            string relative = "persistence/package-inventory.json";
            string text = File.ReadAllText(PathOf(relative));
            int start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            Write(relative, text.Remove(start, 64).Insert(start, new string('f', 64)));
        }

        private void WriteJson<T>(string relative, T value) => Write(relative, JsonSerializer.Serialize(value, Indented));
        private void Write(string relative, string value) => File.WriteAllText(PathOf(relative), value, new UTF8Encoding(false));
        private string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
    }
}
