using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Text;
using Emergence.Model;
using Emergence.Model.Environment;

namespace Emergence.Persistence.WorldPackages;

public sealed class WorldPackageReader
{
    public WorldPackageLoadResult Load(string packagePath) => LoadCore(packagePath, requireExtension: true);

    internal WorldPackageLoadResult LoadCandidate(string packagePath) => LoadCore(packagePath, requireExtension: false);

    private static WorldPackageLoadResult LoadCore(string packagePath, bool requireExtension)
    {
        if (!WorldPackagePathPolicy.TryResolve(packagePath, requireExtension, requireExistingParent: false, out string fullPath, out WorldPackageIssue? pathIssue))
            return Failure(pathIssue!);
        if (!File.Exists(fullPath)) return Failure(Issue("world-package.missing", "World package is missing", "The requested package file does not exist."));

        try
        {
            FileAttributes attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.Directory) != 0)
                return Failure(Issue("world-package.path-directory", "Package path is a directory", "A world package must be a regular file."));
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return Failure(Issue("world-package.path-reparse", "Package path is a reparse point", "Reparse-point packages are not supported."));
            long packageLength = new FileInfo(fullPath).Length;
            if (packageLength > WorldPackageTechnicalLimits.MaxEnvironmentPackageBytes)
                return Failure(Issue("world-package.size-limit", "Package size limit exceeded", $"Maximum package size is {WorldPackageTechnicalLimits.MaxEnvironmentPackageBytes} bytes."));

            int entryCount;
            try
            {
                using FileStream inspection = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.SequentialScan);
                using ZipArchive archive = new(inspection, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: Encoding.UTF8);
                entryCount = archive.Entries.Count;
            }
            catch (InvalidDataException) when (packageLength > WorldPackageTechnicalLimits.MaxPackageBytes)
            {
                return Failure(Issue("world-package.size-limit", "Package size limit exceeded", $"Maximum V1 package size is {WorldPackageTechnicalLimits.MaxPackageBytes} bytes; larger files must be valid V2 packages."));
            }
            if (entryCount != WorldPackageTechnicalLimits.ExactEntryCount)
                return LoadEnvironmentCore(fullPath, packageLength);
            if (packageLength > WorldPackageTechnicalLimits.MaxPackageBytes)
                return Failure(Issue("world-package.size-limit", "V1 package size limit exceeded", $"Maximum V1 package size is {WorldPackageTechnicalLimits.MaxPackageBytes} bytes."));

            Dictionary<string, byte[]> bytesByName;
            using (FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.SequentialScan))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: Encoding.UTF8))
            {
                ZipArchiveEntry[] entries = archive.Entries.ToArray();
                if (entries.Length != WorldPackageTechnicalLimits.ExactEntryCount)
                    return Failure(Issue("world-package.entry-count", "Package entry count is invalid", $"Expected exactly {WorldPackageTechnicalLimits.ExactEntryCount} entries; found {entries.Length}."));

                HashSet<string> names = new(StringComparer.Ordinal);
                long total = 0;
                foreach (ZipArchiveEntry entry in entries)
                {
                    if (!names.Add(entry.FullName))
                        return Failure(Issue("world-package.entry-duplicate", "Duplicate package entry", entry.FullName));
                    if (!WorldPackagePaths.EntryOrder.Contains(entry.FullName, StringComparer.Ordinal)
                        || entry.Name != entry.FullName
                        || entry.FullName.Contains('/')
                        || entry.FullName.Contains('\\')
                        || entry.FullName.Contains("..", StringComparison.Ordinal)
                        || entry.FullName.Contains(':'))
                        return Failure(Issue("world-package.entry-unknown", "Unknown or unsafe package entry", entry.FullName));
                    int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                    if (unixType == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                        return Failure(Issue("world-package.entry-link", "Linked package entry rejected", entry.FullName));
                    if (entry.CompressedLength < 0 || entry.CompressedLength > packageLength)
                        return Failure(Issue("world-package.entry-compressed-size", "Invalid compressed entry length", entry.FullName));
                    long limit = EntryLimit(entry.FullName);
                    if (entry.Length < 0 || entry.Length > limit)
                        return Failure(Issue("world-package.entry-size-limit", "Package entry size limit exceeded", $"{entry.FullName} exceeds {limit} bytes."));
                    total = checked(total + entry.Length);
                    if (total > WorldPackageTechnicalLimits.MaxTotalUncompressedBytes)
                        return Failure(Issue("world-package.total-size-limit", "Total uncompressed size limit exceeded", $"Maximum is {WorldPackageTechnicalLimits.MaxTotalUncompressedBytes} bytes."));
                }
                if (!WorldPackagePaths.EntryOrder.All(names.Contains))
                    return Failure(Issue("world-package.entry-missing", "Required package entry is missing", "The package requires definition.json, snapshot.json, and package-manifest.json."));

                bytesByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (ZipArchiveEntry entry in entries)
                    bytesByName.Add(entry.FullName, ReadExact(entry));
            }

            foreach ((string name, byte[] bytes) in bytesByName)
            {
                try { _ = StrictUtf8.GetStringWithoutBom(bytes); }
                catch (DecoderFallbackException exception)
                {
                    string code = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf
                        ? "world-package.utf8-bom"
                        : "world-package.utf8-invalid";
                    return Failure(Issue(code, "Invalid package text encoding", $"{name}: {Normalize(exception.Message)}"));
                }
            }

            WorldPackageManifest manifest;
            try
            {
                ValidateJson(bytesByName[WorldPackagePaths.ManifestEntry]);
                manifest = JsonSerializer.Deserialize<WorldPackageManifest>(bytesByName[WorldPackagePaths.ManifestEntry], Emergence.Foundation.JsonDefaults.Compact)
                    ?? throw new JsonException("World package manifest cannot be null.");
                RequireCanonicalBytes(manifest, bytesByName[WorldPackagePaths.ManifestEntry], WorldPackagePaths.ManifestEntry);
            }
            catch (JsonException exception)
            {
                return Failure(Issue(JsonCode(exception), "Invalid package manifest", Normalize(exception.Message)));
            }

            foreach (WorldPackageFileEntry entry in manifest.Entries)
            {
                byte[] bytes = bytesByName[entry.Path];
                if (entry.UncompressedByteLength != checked((ulong)bytes.Length))
                    return Failure(Issue("world-package.manifest-length", "Manifest entry length mismatch", entry.Path));
                if (entry.Sha256 != Sha256Digest.Compute(bytes))
                    return Failure(Issue("world-package.hash-mismatch", "Manifest entry hash mismatch", entry.Path));
            }

            WorldSessionDefinition definition;
            WorldSessionSnapshot snapshot;
            try
            {
                byte[] definitionBytes = bytesByName[WorldPackagePaths.DefinitionEntry];
                byte[] snapshotBytes = bytesByName[WorldPackagePaths.SnapshotEntry];
                using JsonDocument definitionDocument = ValidateJson(definitionBytes);
                using JsonDocument snapshotDocument = ValidateJson(snapshotBytes);
                byte[] embeddedDefinition = StrictUtf8.GetBytes(snapshotDocument.RootElement.GetProperty("definition").GetRawText());
                if (!definitionBytes.AsSpan().SequenceEqual(embeddedDefinition))
                    return Failure(Issue("world-package.snapshot-definition", "Snapshot definition entry mismatch", "The embedded snapshot definition is not byte-identical to definition.json."));
                definition = JsonSerializer.Deserialize<WorldSessionDefinition>(definitionBytes, Emergence.Foundation.JsonDefaults.Compact)
                    ?? throw new JsonException("World-session definition cannot be null.");
                snapshot = JsonSerializer.Deserialize<WorldSessionSnapshot>(snapshotBytes, Emergence.Foundation.JsonDefaults.Compact)
                    ?? throw new JsonException("World-session snapshot cannot be null.");
                RequireCanonicalBytes(definition, definitionBytes, WorldPackagePaths.DefinitionEntry);
                RequireCanonicalBytes(snapshot, snapshotBytes, WorldPackagePaths.SnapshotEntry);
            }
            catch (KeyNotFoundException exception)
            {
                return Failure(Issue("world-package.snapshot-mismatch", "Snapshot schema mismatch", Normalize(exception.Message)));
            }
            catch (JsonException exception)
            {
                return Failure(Issue(JsonCode(exception), "Invalid package document", Normalize(exception.Message)));
            }

            try
            {
                WorldPackageDocument package = new(manifest, definition, snapshot, fullPath);
                return new(true, package, Array.Empty<WorldPackageIssue>());
            }
            catch (ArgumentException exception)
            {
                return Failure(Issue("world-package.manifest-mismatch", "Package documents disagree", Normalize(exception.Message)));
            }
        }
        catch (InvalidDataException exception)
        {
            return Failure(Issue("world-package.zip-malformed", "Malformed world package", Normalize(exception.Message)));
        }
        catch (OverflowException exception)
        {
            return Failure(Issue("world-package.size-overflow", "Package size arithmetic overflow", Normalize(exception.Message)));
        }
        catch (IOException exception)
        {
            return Failure(Issue("world-package.unavailable", "World package is locked or unavailable", Normalize(exception.Message)));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(Issue("world-package.unavailable", "World package is locked or unavailable", Normalize(exception.Message)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure(Issue("world-package.io-failure", "World package I/O failed", Normalize(exception.Message)));
        }
    }

    private static WorldPackageLoadResult LoadEnvironmentCore(string fullPath, long packageLength)
    {
        try
        {
            Dictionary<string, byte[]> bytesByName;
            string[] archiveOrder;
            using (FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.SequentialScan))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: Encoding.UTF8))
            {
                ZipArchiveEntry[] entries = archive.Entries.ToArray();
                if (entries.Length < 4 || entries.Length > WorldPackageTechnicalLimits.MaxEnvironmentPackageEntries)
                    return Failure(Issue("world-package.entry-count", "Environment package entry count is invalid", $"Found {entries.Length} entries."));
                HashSet<string> names = new(StringComparer.Ordinal);
                long total = 0;
                long chunkTotal = 0;
                foreach (ZipArchiveEntry entry in entries)
                {
                    if (!names.Add(entry.FullName)) return Failure(Issue("world-package.entry-duplicate", "Duplicate package entry", entry.FullName));
                    bool fieldChunk = WorldPackagePaths.IsCanonicalFieldChunkPath(entry.FullName);
                    if (!fieldChunk && entry.FullName is not (WorldPackagePaths.DefinitionEntry or WorldPackagePaths.SnapshotEntry or WorldPackagePaths.ManifestEntry))
                        return Failure(Issue("world-package.entry-unknown", "Unknown or unsafe environment package entry", entry.FullName));
                    if (entry.Name != Path.GetFileName(entry.FullName.Replace('/', Path.DirectorySeparatorChar))
                        || entry.FullName.Contains('\\') || entry.FullName.Contains("..", StringComparison.Ordinal) || entry.FullName.Contains(':'))
                        return Failure(Issue("world-package.entry-unknown", "Unknown or unsafe environment package entry", entry.FullName));
                    int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                    if (unixType == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                        return Failure(Issue("world-package.entry-link", "Linked package entry rejected", entry.FullName));
                    if (entry.CompressedLength < 0 || entry.CompressedLength > packageLength)
                        return Failure(Issue("world-package.entry-compressed-size", "Invalid compressed entry length", entry.FullName));
                    long limit = fieldChunk ? WorldPackageTechnicalLimits.MaxFieldChunkBytes : entry.FullName switch
                    {
                        WorldPackagePaths.DefinitionEntry => WorldPackageTechnicalLimits.MaxEnvironmentDefinitionBytes,
                        WorldPackagePaths.SnapshotEntry => WorldPackageTechnicalLimits.MaxEnvironmentSnapshotBytes,
                        WorldPackagePaths.ManifestEntry => WorldPackageTechnicalLimits.MaxEnvironmentManifestBytes,
                        _ => 0,
                    };
                    if (entry.Length < 0 || entry.Length > limit)
                        return Failure(Issue("world-package.entry-size-limit", "Environment package entry size limit exceeded", $"{entry.FullName} exceeds {limit} bytes."));
                    total = checked(total + entry.Length);
                    if (fieldChunk)
                    {
                        chunkTotal = checked(chunkTotal + entry.Length);
                        if (chunkTotal > WorldPackageTechnicalLimits.MaxTotalFieldChunkBytes)
                            return Failure(Issue("world-package.chunk-total-limit", "Total field chunk size limit exceeded", entry.FullName));
                    }
                }
                if (!names.Contains(WorldPackagePaths.DefinitionEntry) || !names.Contains(WorldPackagePaths.SnapshotEntry) || !names.Contains(WorldPackagePaths.ManifestEntry))
                    return Failure(Issue("world-package.entry-missing", "Required environment package entry is missing", "definition.json, snapshot.json, and package-manifest.json are required."));
                long maximumTotal = checked((long)WorldPackageTechnicalLimits.MaxEnvironmentDefinitionBytes
                    + WorldPackageTechnicalLimits.MaxEnvironmentSnapshotBytes
                    + WorldPackageTechnicalLimits.MaxEnvironmentManifestBytes
                    + WorldPackageTechnicalLimits.MaxTotalFieldChunkBytes);
                if (total > maximumTotal) return Failure(Issue("world-package.total-size-limit", "Total environment package size limit exceeded", total.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                archiveOrder = entries.Select(static entry => entry.FullName).ToArray();
                bytesByName = entries.ToDictionary(static entry => entry.FullName, ReadExact, StringComparer.Ordinal);
            }

            foreach (string jsonName in new[] { WorldPackagePaths.DefinitionEntry, WorldPackagePaths.SnapshotEntry, WorldPackagePaths.ManifestEntry })
            {
                try { _ = StrictUtf8.GetStringWithoutBom(bytesByName[jsonName]); }
                catch (DecoderFallbackException exception)
                {
                    string code = bytesByName[jsonName].AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? "world-package.utf8-bom" : "world-package.utf8-invalid";
                    return Failure(Issue(code, "Invalid package text encoding", $"{jsonName}: {Normalize(exception.Message)}"));
                }
            }

            WorldSessionDefinition definition;
            IReadOnlyList<EnvironmentFieldChunkDescriptor> descriptors;
            WorldPackageManifest manifest;
            try
            {
                byte[] definitionBytes = bytesByName[WorldPackagePaths.DefinitionEntry];
                ValidateJson(definitionBytes);
                definition = JsonSerializer.Deserialize<WorldSessionDefinition>(definitionBytes, Emergence.Foundation.JsonDefaults.Compact)
                    ?? throw new JsonException("Environment definition cannot be null.");
                if (definition.FormatVersion != WorldSessionDefinition.EnvironmentFormatVersion || definition.EnvironmentDefinition is null)
                    throw new JsonException("Environment package requires a V3 session definition.");
                RequireCanonicalBytes(definition, definitionBytes, WorldPackagePaths.DefinitionEntry);
                descriptors = EnvironmentSnapshotPackageJson.ParseMetadata(bytesByName[WorldPackagePaths.SnapshotEntry], definition);
                byte[] manifestBytes = bytesByName[WorldPackagePaths.ManifestEntry];
                ValidateJson(manifestBytes);
                manifest = JsonSerializer.Deserialize<WorldPackageManifest>(manifestBytes, Emergence.Foundation.JsonDefaults.Compact)
                    ?? throw new JsonException("Environment package manifest cannot be null.");
                if (manifest.FormatVersion != WorldPackageManifest.EnvironmentFormatVersion)
                    throw new JsonException("Environment package requires manifest format 2.0.0.");
                RequireCanonicalBytes(manifest, manifestBytes, WorldPackagePaths.ManifestEntry);
            }
            catch (JsonException exception)
            {
                return Failure(Issue(JsonCode(exception), "Invalid environment package metadata", Normalize(exception.Message)));
            }

            EnvironmentDefinition environmentDefinition = definition.EnvironmentDefinition!;
            List<(RegionLatticeDefinition Region, FieldChunkCoordinate Coordinate, string Path)> expected = [];
            foreach (RegionLatticeDefinition region in environmentDefinition.Regions.OrderBy(static region => region.RegionId))
            for (uint chunkY = 0; chunkY < region.ChunkRows; chunkY++)
            for (uint chunkX = 0; chunkX < region.ChunkColumns; chunkX++)
            {
                FieldChunkCoordinate coordinate = new(chunkX, chunkY);
                expected.Add((region, coordinate, FieldChunkCodec.GetPath(region, coordinate)));
            }
            string[] expectedDataPaths = [WorldPackagePaths.DefinitionEntry, WorldPackagePaths.SnapshotEntry, .. expected.Select(static item => item.Path)];
            string[] expectedArchivePaths = [.. expectedDataPaths, WorldPackagePaths.ManifestEntry];
            if (archiveOrder.Length != expectedArchivePaths.Length || !archiveOrder.ToHashSet(StringComparer.Ordinal).SetEquals(expectedArchivePaths))
                return Failure(Issue("world-package.chunk-set", "Environment package chunk set mismatch", "A required chunk is missing, duplicated, or unknown."));
            if (!manifest.Entries.Select(static entry => entry.Path).SequenceEqual(expectedDataPaths))
                return Failure(Issue("world-package.manifest-order", "Environment manifest entry order mismatch", "Expected definition, snapshot, then RegionId/chunk-Y/chunk-X order."));
            if (descriptors.Count != expected.Count)
                return Failure(Issue("world-package.chunk-descriptors", "Environment snapshot chunk descriptor count mismatch", descriptors.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            for (int index = 0; index < manifest.Entries.Count; index++)
            {
                WorldPackageFileEntry entry = manifest.Entries[index];
                byte[] bytes = bytesByName[entry.Path];
                if (entry.UncompressedByteLength != checked((ulong)bytes.Length))
                    return Failure(Issue("world-package.manifest-length", "Manifest entry length mismatch", entry.Path));
                if (entry.Sha256 != Sha256Digest.Compute(bytes))
                    return Failure(Issue("world-package.hash-mismatch", "Manifest entry hash mismatch", entry.Path));
            }

            List<RegionFieldState> regionStates = [];
            try
            {
                int descriptorIndex = 0;
                foreach (IGrouping<RegionLatticeDefinition, (RegionLatticeDefinition Region, FieldChunkCoordinate Coordinate, string Path)> group in expected.GroupBy(static item => item.Region))
                {
                    RegionLatticeDefinition region = group.Key;
                    MatterAmount[][] amounts = region.FieldChannels.Definitions.Select(_ => new MatterAmount[region.CellCount]).ToArray();
                    foreach ((RegionLatticeDefinition _, FieldChunkCoordinate coordinate, string path) in group)
                    {
                        DecodedFieldChunk decoded = FieldChunkCodec.Decode(bytesByName[path], region, coordinate);
                        (uint startX, uint startY, uint width, uint height) = region.GetChunkBounds(coordinate);
                        EnvironmentFieldChunkDescriptor descriptor = descriptors[descriptorIndex++];
                        WorldPackageFileEntry manifestEntry = manifest.Entries[descriptorIndex + 1];
                        EnvironmentFieldChunkDescriptor exactDescriptor = new(path, region.RegionId, coordinate, width, height,
                            checked((ulong)bytesByName[path].Length), Sha256Digest.Compute(bytesByName[path]));
                        if (descriptor != exactDescriptor || manifestEntry.Path != path)
                            throw new InvalidDataException("Environment snapshot chunk descriptor mismatch.");
                        for (int slot = 0; slot < amounts.Length; slot++)
                        for (int localIndex = 0; localIndex < decoded.CellCount; localIndex++)
                        {
                            uint localX = (uint)localIndex % width;
                            uint localY = (uint)localIndex / width;
                            int globalIndex = region.GetLinearIndex(new(startX + localX, startY + localY));
                            amounts[slot][globalIndex] = decoded.GetAmount(slot, localIndex);
                        }
                    }
                    regionStates.Add(new(region, region.FieldChannels.Definitions.Select((channel, slot) =>
                        new RegionFieldChannelAmounts(channel.Id, Array.AsReadOnly(amounts[slot])))));
                }
                WorldEnvironmentState environment = new(environmentDefinition, regionStates);
                WorldSessionSnapshot snapshot = EnvironmentSnapshotPackageJson.Hydrate(
                    bytesByName[WorldPackagePaths.SnapshotEntry], definition, environment, descriptors);
                WorldPackageDocument package = new(manifest, definition, snapshot, fullPath);
                return new(true, package, Array.Empty<WorldPackageIssue>());
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidDataException or JsonException or OverflowException)
            {
                return Failure(Issue("world-package.environment-invalid", "Environment package reconstruction failed", Normalize(exception.Message)));
            }
        }
        catch (InvalidDataException exception) { return Failure(Issue("world-package.zip-malformed", "Malformed environment package", Normalize(exception.Message))); }
        catch (OverflowException exception) { return Failure(Issue("world-package.size-overflow", "Environment package size arithmetic overflow", Normalize(exception.Message))); }
        catch (IOException exception) { return Failure(Issue("world-package.unavailable", "Environment package is unavailable", Normalize(exception.Message))); }
        catch (UnauthorizedAccessException exception) { return Failure(Issue("world-package.unavailable", "Environment package is unavailable", Normalize(exception.Message))); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return Failure(Issue("world-package.io-failure", "Environment package I/O failed", Normalize(exception.Message))); }
    }

    private static JsonDocument ValidateJson(byte[] bytes) => JsonDocument.Parse(bytes, WorldPackageJson.DocumentOptions);

    private static void RequireCanonicalBytes<T>(T value, byte[] actual, string name)
    {
        byte[] expected = WorldPackageJson.SerializeCompact(value);
        if (!actual.AsSpan().SequenceEqual(expected)) throw new JsonException($"{name} is not the exact supported compact serialization.");
    }

    private static byte[] ReadExact(ZipArchiveEntry entry)
    {
        byte[] bytes = GC.AllocateUninitializedArray<byte>(checked((int)entry.Length));
        using Stream stream = entry.Open();
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException($"Entry '{entry.FullName}' exceeded its declared length.");
        return bytes;
    }

    private static long EntryLimit(string name) => name switch
    {
        WorldPackagePaths.DefinitionEntry => WorldPackageTechnicalLimits.MaxDefinitionBytes,
        WorldPackagePaths.SnapshotEntry => WorldPackageTechnicalLimits.MaxSnapshotBytes,
        WorldPackagePaths.ManifestEntry => WorldPackageTechnicalLimits.MaxManifestBytes,
        _ => 0,
    };

    private static string JsonCode(JsonException exception) => exception.Message.Contains("Unsupported", StringComparison.Ordinal)
        ? "world-package.format-unsupported"
        : exception.Message.Contains("digest mismatch", StringComparison.OrdinalIgnoreCase)
            ? "world-package.digest-mismatch"
            : "world-package.json-invalid";

    internal static WorldPackageIssue Issue(string code, string summary, string detail) => new(code, summary, detail);
    internal static WorldPackageLoadResult Failure(WorldPackageIssue issue) => new(false, null, Array.AsReadOnly([issue]));
    internal static string Normalize(string message)
    {
        string text = message.Split(['\r', '\n'], 2)[0];
        return text.Length <= 500 ? text : text[..500];
    }
}

internal static class WorldPackagePathPolicy
{
    public static bool TryResolve(
        string? path,
        bool requireExtension,
        bool requireExistingParent,
        out string fullPath,
        out WorldPackageIssue? issue)
    {
        fullPath = string.Empty;
        issue = null;
        if (string.IsNullOrWhiteSpace(path) || path.Contains("://", StringComparison.Ordinal))
        {
            issue = WorldPackageReader.Issue("world-package.path-invalid", "Invalid world package path", "A local filesystem path is required.");
            return false;
        }
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = WorldPackageReader.Issue("world-package.path-invalid", "Invalid world package path", WorldPackageReader.Normalize(exception.Message));
            return false;
        }
        if (requireExtension && !fullPath.EndsWith(WorldPackagePaths.Extension, StringComparison.OrdinalIgnoreCase))
        {
            issue = WorldPackageReader.Issue("world-package.extension", "Invalid world package extension", $"Package paths must end with {WorldPackagePaths.Extension}.");
            return false;
        }
        string? parent = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(parent) || (requireExistingParent && !Directory.Exists(parent)))
        {
            issue = WorldPackageReader.Issue("world-package.parent-missing", "World package parent directory is missing", "The parent directory must already exist.");
            return false;
        }
        if (Directory.Exists(parent))
        {
            try
            {
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                {
                    issue = WorldPackageReader.Issue("world-package.parent-reparse", "World package parent is a reparse point", "Reparse-point parents are not supported.");
                    return false;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issue = WorldPackageReader.Issue("world-package.parent-unavailable", "World package parent is unavailable", WorldPackageReader.Normalize(exception.Message));
                return false;
            }
        }
        return true;
    }
}
