using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Emergence.ReviewPack;

public static class Phase11EvidenceValidator
{
    public const string Phase = "Milestone 1 Phase 1.1";
    public const string Version = "1.1.0-dev";
    public const string ShellMarker = "HABITAT / M1.1";
    private static readonly (string Name, string Value)[] Digests =
    [
        ("fieldChannelCatalogDigest", "c9fa1bc20193b72fcbbc7780776018a81d599716fd6673bc71d266d416393429"),
        ("regionDefinitionDigest", "07b963faec60e3b43b97bea182a4770ce079738a987413b1042c1ed103ebffc1"),
        ("regionStateDigest", "c22b643d840dc32d6f22e5a6281396292cabb0ebd5b370773f7309efa89da5ca"),
        ("environmentDefinitionDigest", "04fb13424920862b4be724befadccd8754ed21ff3ef0cc6c887f671ffa8c8e08"),
        ("environmentStateDigest", "cb98e417570c1b46073170128eebfc7b5b84e38bb4a1a1eac622ceb8d1578466"),
        ("algorithmCatalogDigest", "b6339de0044a28aa9af9d1f3dde6d29a70e53742f678e2ee08586250cf431c65"),
        ("sessionDefinitionDigest", "3b3cc11fd0c728ee2d18f2f59406ec3b144c258423bdaae719634d735dd048ac"),
        ("sessionStateDigest", "ed67529eb33daa70db0ff52ff5d50071aae193222c6c98f26f73839286c827bc"),
        ("snapshotDigest", "710653573b0f996970ea3cd5e9b5632dd822bbae4946702b3624bd84b9c18543"),
        ("packageIdentityDigest", "a05a5eb93c9a098dc446f1315a75da1d31b118b2fbe12f203ea6a37476e1f685"),
        ("manifestDigest", "b8516ab7ddfbe889c2a8f38c3acb3f0b84a3d60922e85b29d3bc2199bb8bcdee"),
    ];
    private static readonly (string Path, int Length, string Hash)[] Chunks =
    [
        ("regions/00000000000000000000000000000064/fields/0000-0000.bin", 1720, "e9c9f690eb5d36b9c2532e898dcf04307bfb30c107e61299402af7e64c6ea158"),
        ("regions/00000000000000000000000000000064/fields/0000-0001.bin", 1720, "7aa20e39a5b11dbd6b66c0a63d626e9d7e6315f7f048e2574061faf0a0034767"),
        ("regions/00000000000000000000000000000064/fields/0001-0000.bin", 952, "eb9f89e0e1e9c9e2f78ac60db42e78d3d53a6d8c38c0971c2dc9c899996731bd"),
        ("regions/00000000000000000000000000000064/fields/0001-0001.bin", 952, "74426508ec8e95f63a073abdf9a78cfb0e1ddb234a13e9ecb1aa86e5f2c2b427"),
    ];

    public static EnvironmentEvidence Evaluate(string reviewRoot)
    {
        List<string> errors = [];
        string selfRelative = "cli/environment-self-test.json";
        string referenceRelative = "reference/phase11-environment-vectors.json";
        string packageRelative = "cli/environment-session.emergence-world";
        string performanceRelative = "cli/environment-performance.json";
        string normalRelative = "app/field-normal.png";
        string rawRelative = "app/field-raw-grid.png";
        JsonElement self = ReadJson(Path.Combine(reviewRoot, selfRelative.Replace('/', Path.DirectorySeparatorChar)), "environment self-test", errors);
        JsonElement reference = ReadJson(Path.Combine(reviewRoot, referenceRelative.Replace('/', Path.DirectorySeparatorChar)), "independent environment vectors", errors);
        foreach ((string name, string value) in Digests)
        {
            RequireString(self, name, value, "self-test", errors);
            RequireString(reference, name, value, "independent reference", errors);
        }
        bool success = Boolean(self, "success") && Boolean(reference, "success");
        bool saveLoad = Boolean(self, "saveLoadMatched");
        bool staticTick = Boolean(self, "oneTickEnvironmentUnchanged");
        int solid = Integer(self, "solidCellCount");
        int fluid = Integer(self, "fluidCellCount");
        if (solid != 59 || fluid != 133) errors.Add("Environment solid/fluid counts do not match 59/133.");
        string[] totals = ReadTotals(self, errors);
        bool independent = errors.Count == 0;
        ValidatePerformance(Path.Combine(reviewRoot, performanceRelative.Replace('/', Path.DirectorySeparatorChar)), errors);
        ValidatePackage(reviewRoot, packageRelative, errors);
        bool normal = Nonempty(Path.Combine(reviewRoot, normalRelative.Replace('/', Path.DirectorySeparatorChar)));
        bool raw = Nonempty(Path.Combine(reviewRoot, rawRelative.Replace('/', Path.DirectorySeparatorChar)));
        if (!normal) errors.Add("Fresh normal field screenshot is missing.");
        if (!raw) errors.Add("Fresh raw-grid field screenshot is missing.");
        if (!success || !saveLoad || !staticTick) errors.Add("Environment self-test does not report complete success/save-load/static-tick evidence.");
        string[] evidence = [selfRelative, referenceRelative, packageRelative, performanceRelative, normalRelative, rawRelative,
            .. Chunks.Select(chunk => "environment/chunks/" + chunk.Path)];
        return new(
            errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed,
            Digests[0].Value, Digests[1].Value, Digests[2].Value, Digests[3].Value, Digests[4].Value,
            Digests[5].Value, Digests[6].Value, Digests[7].Value, Digests[8].Value, Digests[9].Value, Digests[10].Value,
            solid, fluid, totals, Chunks.Select(static chunk => chunk.Path).ToArray(), Chunks.Length,
            saveLoad, staticTick, independent, normal, raw, evidence,
            errors.Count == 0 ? "Phase 1.1 definitions, exact fields, probes/totals, chunks, V2 package, restoration, static tick, screenshots, and independent vectors passed." : string.Join(" ", errors));
    }

    private static void ValidatePackage(string reviewRoot, string relative, List<string> errors)
    {
        string path = Path.Combine(reviewRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) { errors.Add("Environment V2 world package is missing."); return; }
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            string[] expected = ["definition.json", "snapshot.json", .. Chunks.Select(static chunk => chunk.Path), "package-manifest.json"];
            if (!archive.Entries.Select(static entry => entry.FullName).SequenceEqual(expected)) errors.Add("Environment package does not have the exact seven-entry writer order.");
            Dictionary<string, byte[]> data = archive.Entries.ToDictionary(static entry => entry.FullName, static entry =>
            {
                using Stream input = entry.Open(); using MemoryStream output = new(); input.CopyTo(output); return output.ToArray();
            }, StringComparer.Ordinal);
            using JsonDocument definition = JsonDocument.Parse(data["definition.json"]);
            JsonElement def = definition.RootElement;
            if (def.GetProperty("formatVersion").GetString() != "3.0.0" || def.GetProperty("environmentDefinitionDigest").GetString() != Digests[3].Value)
                errors.Add("Environment package definition is not the locked V3 definition.");
            using JsonDocument snapshot = JsonDocument.Parse(data["snapshot.json"]);
            JsonElement snap = snapshot.RootElement;
            if (snap.GetProperty("formatVersion").GetString() != "2.0.0" || snap.GetProperty("digest").GetString() != Digests[8].Value
                || snap.GetProperty("environmentStateDigest").GetString() != Digests[4].Value) errors.Add("Environment package snapshot metadata is not locked V2.");
            JsonElement[] descriptors = snap.GetProperty("fieldChunks").EnumerateArray().ToArray();
            if (descriptors.Length != Chunks.Length) errors.Add("Environment snapshot does not describe four chunks.");
            using JsonDocument manifestDocument = JsonDocument.Parse(data["package-manifest.json"]);
            JsonElement manifest = manifestDocument.RootElement;
            if (manifest.GetProperty("formatVersion").GetString() != "2.0.0"
                || manifest.GetProperty("packageIdentityDigest").GetString() != Digests[9].Value
                || manifest.GetProperty("digest").GetString() != Digests[10].Value) errors.Add("Environment package V2 manifest identity/digest mismatch.");
            JsonElement[] entries = manifest.GetProperty("entries").EnumerateArray().ToArray();
            if (entries.Length != 6) errors.Add("Environment manifest must describe six logical data entries.");
            foreach (JsonElement entry in entries)
            {
                string entryPath = entry.GetProperty("path").GetString() ?? string.Empty;
                if (!data.TryGetValue(entryPath, out byte[]? bytes)
                    || entry.GetProperty("uncompressedByteLength").GetString() != bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    || entry.GetProperty("sha256").GetString() != Convert.ToHexStringLower(SHA256.HashData(bytes))) errors.Add($"Environment manifest entry mismatch: {entryPath}.");
            }
            UInt128[] totals = [0, 0, 0];
            for (int index = 0; index < Chunks.Length; index++)
            {
                (string chunkPath, int length, string hash) = Chunks[index];
                byte[] bytes = data[chunkPath];
                if (bytes.Length != length || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) errors.Add($"Locked chunk mismatch: {chunkPath}.");
                string extracted = Path.Combine(reviewRoot, "environment", "chunks", chunkPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(extracted) || !File.ReadAllBytes(extracted).AsSpan().SequenceEqual(bytes)) errors.Add($"Extracted raw chunk mismatch: {chunkPath}.");
                DecodeChunk(bytes, index % 2, index / 2, totals, errors);
                if (index < descriptors.Length && (descriptors[index].GetProperty("path").GetString() != chunkPath
                    || descriptors[index].GetProperty("sha256").GetString() != hash)) errors.Add($"Snapshot descriptor mismatch: {chunkPath}.");
            }
            if (totals[0] != 183686 || totals[1] != 120947 || totals[2] != 6310) errors.Add("Decoded V2 chunk totals are not exact.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or KeyNotFoundException or ArgumentException or OverflowException)
        { errors.Add($"Environment V2 package validation failed: {exception.Message}"); }
    }

    private static void DecodeChunk(byte[] bytes, int expectedX, int expectedY, UInt128[] totals, List<string> errors)
    {
        ReadOnlySpan<byte> span = bytes; int offset = 0;
        if (span.Length < 116 || !span[..16].SequenceEqual("PE-FIELD-CHUNK1\0"u8)) { errors.Add("Field chunk magic/header mismatch."); return; }
        offset = 32;
        uint x = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        uint y = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)); offset += 2;
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)); offset += 2;
        ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)); offset += 2;
        ushort reserved = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)); offset += 2;
        string regionDigest = Convert.ToHexStringLower(span.Slice(offset, 32)); offset += 32;
        string catalogDigest = Convert.ToHexStringLower(span.Slice(offset, 32)); offset += 32;
        uint cells = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
        if (x != expectedX || y != expectedY || channels != 3 || reserved != 0 || cells != width * height
            || regionDigest != Digests[1].Value || catalogDigest != Digests[0].Value) errors.Add("Field chunk semantic header mismatch.");
        string[] ids = ["matter.energy-substrate", "matter.structural-precursor", "matter.waste"];
        for (int channel = 0; channel < 3; channel++)
        {
            ushort idLength = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)); offset += 2;
            string id = Encoding.UTF8.GetString(span.Slice(offset, idLength)); offset += idLength;
            if (id != ids[channel]) errors.Add("Field chunk channel order mismatch.");
            for (int cell = 0; cell < cells; cell++) { totals[channel] += BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8; }
        }
        if (offset != span.Length) errors.Add("Field chunk has trailing or truncated data.");
    }

    private static void ValidatePerformance(string path, List<string> errors)
    {
        JsonElement root = ReadJson(path, "environment performance", errors);
        if (!Boolean(root, "success") || Integer(root, "bytesPerFieldSlot") != 8
            || Integer(root, "referenceAuthoritativeFieldBytes") != 4608
            || root.GetProperty("maximumContractEstimatedFieldBytes").GetInt64() != 33_554_432)
            errors.Add("Environment performance/memory evidence is incomplete or inconsistent.");
    }

    private static string[] ReadTotals(JsonElement root, List<string> errors)
    {
        try
        {
            string[] totals = root.GetProperty("channelTotals").EnumerateArray().Select(item => $"{item.GetProperty("channelId").GetString()}={item.GetProperty("total").GetString()}").ToArray();
            string[] expected = ["matter.energy-substrate=183686", "matter.structural-precursor=120947", "matter.waste=6310"];
            if (!totals.SequenceEqual(expected)) errors.Add("Environment channel totals are not exact.");
            return totals;
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException) { errors.Add($"Environment totals are invalid: {exception.Message}"); return []; }
    }

    private static JsonElement ReadJson(string path, string name, List<string> errors)
    {
        try { return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone(); }
        catch (Exception exception) when (exception is IOException or JsonException) { errors.Add($"Missing or invalid {name}: {exception.Message}"); return default; }
    }
    private static void RequireString(JsonElement root, string name, string expected, string source, List<string> errors)
    { if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out JsonElement value) || value.GetString() != expected) errors.Add($"{source} {name} mismatch."); }
    private static bool Boolean(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
    private static int Integer(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) ? result : -1;
    private static bool Nonempty(string path) => File.Exists(path) && new FileInfo(path).Length > 0;
}
