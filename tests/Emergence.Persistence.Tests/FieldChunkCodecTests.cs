using Emergence.Foundation.Hashing;
using Emergence.Model.Environment;
using Emergence.Persistence.WorldPackages;
using Emergence.Simulation.Fields;
using System.IO.Compression;

namespace Emergence.Persistence.Tests;

public sealed class FieldChunkCodecTests
{
    private static readonly (FieldChunkCoordinate Coordinate, string Path, int Length, string Hash)[] Locked =
    [
        (new(0, 0), "regions/00000000000000000000000000000064/fields/0000-0000.bin", 1720, "e9c9f690eb5d36b9c2532e898dcf04307bfb30c107e61299402af7e64c6ea158"),
        (new(1, 0), "regions/00000000000000000000000000000064/fields/0000-0001.bin", 1720, "7aa20e39a5b11dbd6b66c0a63d626e9d7e6315f7f048e2574061faf0a0034767"),
        (new(0, 1), "regions/00000000000000000000000000000064/fields/0001-0000.bin", 952, "eb9f89e0e1e9c9e2f78ac60db42e78d3d53a6d8c38c0971c2dc9c899996731bd"),
        (new(1, 1), "regions/00000000000000000000000000000064/fields/0001-0001.bin", 952, "74426508ec8e95f63a073abdf9a78cfb0e1ddb234a13e9ecb1aa86e5f2c2b427"),
    ];

    [Fact]
    public void ReferenceChunksMatchEveryLockedPathLengthHashAndAmount()
    {
        RegionFieldState state = ReferenceEnvironmentFixture.CreateStore().Capture().Regions.Single();
        foreach ((FieldChunkCoordinate coordinate, string path, int length, string hash) in Locked)
        {
            byte[] bytes = FieldChunkCodec.Encode(state, coordinate);
            Assert.Equal(path, FieldChunkCodec.GetPath(state.Definition, coordinate));
            Assert.Equal(length, bytes.Length);
            Assert.Equal(hash, Sha256Digest.Compute(bytes).ToString());
            DecodedFieldChunk decoded = FieldChunkCodec.Decode(bytes, state.Definition, coordinate);
            (uint startX, uint startY, uint width, uint height) = state.Definition.GetChunkBounds(coordinate);
            for (int slot = 0; slot < state.Definition.FieldChannels.Definitions.Count; slot++)
            for (int index = 0; index < decoded.CellCount; index++)
            {
                uint localX = (uint)index % width;
                uint localY = (uint)index / width;
                Assert.Equal(state.GetAmount(slot, state.Definition.GetLinearIndex(new(startX + localX, startY + localY))), decoded.GetAmount(slot, index));
            }
        }
    }

    [Fact]
    public void DecoderRejectsWrongMagicReservedTrailingTruncatedAndWrongCoordinate()
    {
        RegionFieldState state = ReferenceEnvironmentFixture.CreateStore().Capture().Regions.Single();
        byte[] valid = FieldChunkCodec.Encode(state, new(0, 0));
        foreach (int length in new[] { 0, 15, FieldChunkCodec.HeaderByteLength - 1, valid.Length - 1 })
            Assert.Throws<InvalidDataException>(() => FieldChunkCodec.Decode(valid.AsSpan(0, length), state.Definition, new(0, 0)));
        byte[] wrongMagic = (byte[])valid.Clone(); wrongMagic[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => FieldChunkCodec.Decode(wrongMagic, state.Definition, new(0, 0)));
        byte[] reserved = (byte[])valid.Clone(); reserved[46] = 1;
        Assert.Throws<InvalidDataException>(() => FieldChunkCodec.Decode(reserved, state.Definition, new(0, 0)));
        byte[] trailing = [.. valid, 0];
        Assert.Throws<InvalidDataException>(() => FieldChunkCodec.Decode(trailing, state.Definition, new(0, 0)));
        Assert.Throws<InvalidDataException>(() => FieldChunkCodec.Decode(valid, state.Definition, new(1, 0)));
        byte[] wrongDefinition = (byte[])valid.Clone(); wrongDefinition[48] ^= 1;
        Assert.Throws<InvalidDataException>(() => FieldChunkCodec.Decode(wrongDefinition, state.Definition, new(0, 0)));
        byte[] matterInSolid = (byte[])valid.Clone(); matterInSolid[141] = 1;
        Assert.Throws<InvalidDataException>(() => FieldChunkCodec.Decode(matterInSolid, state.Definition, new(0, 0)));
    }

    [Fact]
    public void EnvironmentPackageV2HasSevenExactEntriesAndRestoresEveryAmount()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Emergence.EnvironmentPackage.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "reference.emergence-world");
        try
        {
            var snapshot = EnvironmentSessionFixture.CreateSnapshot();
            WorldPackageSaveResult save = new WorldPackageWriter().Save(path, snapshot);
            Assert.True(save.Success, string.Join(Environment.NewLine, save.Issues));
            Assert.Equal("a05a5eb93c9a098dc446f1315a75da1d31b118b2fbe12f203ea6a37476e1f685", save.PackageIdentityDigest);
            Assert.Equal("b8516ab7ddfbe889c2a8f38c3acb3f0b84a3d60922e85b29d3bc2199bb8bcdee", save.ManifestDigest);
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                Assert.Equal(new string[]
                {
                    "definition.json",
                    "snapshot.json",
                    Locked[0].Path,
                    Locked[1].Path,
                    Locked[2].Path,
                    Locked[3].Path,
                    "package-manifest.json",
                }, archive.Entries.Select(static entry => entry.FullName));
                Assert.All(archive.Entries, static entry => Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), entry.LastWriteTime.DateTime));
                Assert.Equal(7, archive.Entries.Count);
            }
            WorldPackageLoadResult load = new WorldPackageReader().Load(path);
            Assert.True(load.Success, string.Join(Environment.NewLine, load.Issues));
            Assert.NotNull(load.Document);
            Assert.Equal(snapshot, load.Document!.Snapshot);
            Assert.Equal(snapshot.EnvironmentState, load.Document.Snapshot.EnvironmentState);
            Assert.Equal(snapshot.StateDigest, load.Document.Snapshot.StateDigest);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void V2ReaderRejectsMissingCorruptAndSwappedChunksButIgnoresZipEnumerationOrder()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Emergence.EnvironmentPackage.Corruption", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string valid = Path.Combine(directory, "valid.emergence-world");
        try
        {
            Assert.True(new WorldPackageWriter().Save(valid, EnvironmentSessionFixture.CreateSnapshot()).Success);
            List<(string Name, byte[] Bytes)> entries = ReadZip(valid);
            string first = Locked[0].Path;
            string second = Locked[1].Path;

            string reordered = Path.Combine(directory, "reordered.emergence-world");
            WriteZip(reordered, entries.AsEnumerable().Reverse());
            Assert.True(new WorldPackageReader().Load(reordered).Success);

            string missing = Path.Combine(directory, "missing.emergence-world");
            WriteZip(missing, entries.Where(entry => entry.Name != Locked[3].Path));
            Assert.False(new WorldPackageReader().Load(missing).Success);

            string corrupt = Path.Combine(directory, "corrupt.emergence-world");
            WriteZip(corrupt, entries.Select(entry => entry.Name == first
                ? (entry.Name, entry.Bytes.Select((value, index) => index == entry.Bytes.Length - 1 ? (byte)(value ^ 1) : value).ToArray())
                : entry));
            Assert.Contains(new WorldPackageReader().Load(corrupt).Issues, static issue => issue.Code == "world-package.hash-mismatch");

            byte[] firstBytes = entries.Single(entry => entry.Name == first).Bytes;
            byte[] secondBytes = entries.Single(entry => entry.Name == second).Bytes;
            string swapped = Path.Combine(directory, "swapped.emergence-world");
            WriteZip(swapped, entries.Select(entry => entry.Name == first ? (entry.Name, secondBytes)
                : entry.Name == second ? (entry.Name, firstBytes) : entry));
            Assert.False(new WorldPackageReader().Load(swapped).Success);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static List<(string Name, byte[] Bytes)> ReadZip(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        return archive.Entries.Select(entry =>
        {
            using Stream stream = entry.Open();
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            return (entry.FullName, buffer.ToArray());
        }).ToList();
    }

    private static void WriteZip(string path, IEnumerable<(string Name, byte[] Bytes)> entries)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string name, byte[] bytes) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using Stream stream = entry.Open();
            stream.Write(bytes);
        }
    }
}
