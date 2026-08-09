using System.Buffers;
using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Model.Environment;

namespace Emergence.Persistence.WorldPackages;

internal sealed record EnvironmentFieldChunkDescriptor(
    string Path,
    RegionId RegionId,
    FieldChunkCoordinate Coordinate,
    uint Width,
    uint Height,
    ulong UncompressedByteLength,
    Sha256Digest Sha256);

internal static class EnvironmentSnapshotPackageJson
{
    private static readonly string[] Properties =
    [
        "formatVersion", "definition", "currentTick", "status", "lastCommandSequence", "lastEventSequence",
        "pendingCommands", "faultIssues", "environmentStateDigest", "fieldChunks", "stateDigest", "digest",
    ];

    public static byte[] Serialize(WorldSessionSnapshot snapshot, IReadOnlyList<EnvironmentFieldChunkDescriptor> chunks)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != WorldSessionSnapshot.EnvironmentFormatVersion || snapshot.EnvironmentState is null)
            throw new ArgumentException("Environment snapshot JSON requires a V2 snapshot.", nameof(snapshot));
        byte[] core = WorldPackageJson.SerializeCompact(snapshot);
        using JsonDocument document = JsonDocument.Parse(core, WorldPackageJson.DocumentOptions);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            bool inserted = false;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
                if (property.NameEquals("environmentStateDigest"))
                {
                    writer.WritePropertyName("fieldChunks");
                    writer.WriteStartArray();
                    foreach (EnvironmentFieldChunkDescriptor chunk in chunks)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("path", chunk.Path);
                        writer.WriteString("regionId", chunk.RegionId.ToString());
                        writer.WriteNumber("chunkX", chunk.Coordinate.X);
                        writer.WriteNumber("chunkY", chunk.Coordinate.Y);
                        writer.WriteNumber("width", chunk.Width);
                        writer.WriteNumber("height", chunk.Height);
                        writer.WriteString("uncompressedByteLength", chunk.UncompressedByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        writer.WriteString("sha256", chunk.Sha256.ToString());
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    inserted = true;
                }
            }
            if (!inserted) throw new InvalidOperationException("Environment snapshot serializer did not find its state digest.");
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    public static IReadOnlyList<EnvironmentFieldChunkDescriptor> ParseMetadata(
        byte[] bytes,
        WorldSessionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(definition);
        using JsonDocument document = JsonDocument.Parse(bytes, WorldPackageJson.DocumentOptions);
        JsonElement root = document.RootElement;
        WorldPackageJson.Exact(root, Properties);
        if (SemanticVersion.Parse(root.GetProperty("formatVersion").GetString()!) != WorldSessionSnapshot.EnvironmentFormatVersion)
            throw new JsonException("Environment package requires snapshot format 2.0.0.");
        WorldSessionDefinition embedded = JsonSerializer.Deserialize<WorldSessionDefinition>(root.GetProperty("definition"), JsonDefaults.Compact)
            ?? throw new JsonException("Environment snapshot definition is missing.");
        if (!embedded.Equals(definition)) throw new JsonException("Environment snapshot definition mismatch.");
        Sha256Digest state = Sha256Digest.Parse(root.GetProperty("environmentStateDigest").GetString()!);
        EnvironmentDefinition environment = definition.EnvironmentDefinition ?? throw new JsonException("V3 definition environment is missing.");
        List<EnvironmentFieldChunkDescriptor> chunks = [];
        foreach (JsonElement element in root.GetProperty("fieldChunks").EnumerateArray())
        {
            WorldPackageJson.Exact(element, "path", "regionId", "chunkX", "chunkY", "width", "height", "uncompressedByteLength", "sha256");
            string length = element.GetProperty("uncompressedByteLength").GetString() ?? throw new JsonException("Chunk length is missing.");
            if (length.Length == 0 || (length.Length > 1 && length[0] == '0')
                || !ulong.TryParse(length, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong parsedLength))
                throw new JsonException("Chunk length is not a canonical UInt64 string.");
            chunks.Add(new(
                element.GetProperty("path").GetString() ?? throw new JsonException("Chunk path is missing."),
                RegionId.Parse(element.GetProperty("regionId").GetString()!),
                new(element.GetProperty("chunkX").GetUInt32(), element.GetProperty("chunkY").GetUInt32()),
                element.GetProperty("width").GetUInt32(),
                element.GetProperty("height").GetUInt32(),
                parsedLength,
                Sha256Digest.Parse(element.GetProperty("sha256").GetString()!)));
        }
        if (chunks.Count == 0 || chunks.Count > WorldPackageTechnicalLimits.MaxEnvironmentPackageEntries - 3)
            throw new JsonException("Environment snapshot chunk count is invalid.");
        _ = state;
        return Array.AsReadOnly(chunks.ToArray());
    }

    public static WorldSessionSnapshot Hydrate(
        byte[] bytes,
        WorldSessionDefinition definition,
        WorldEnvironmentState environment,
        IReadOnlyList<EnvironmentFieldChunkDescriptor> expectedChunks)
    {
        IReadOnlyList<EnvironmentFieldChunkDescriptor> parsedChunks = ParseMetadata(bytes, definition);
        if (!parsedChunks.SequenceEqual(expectedChunks)) throw new JsonException("Environment snapshot chunk descriptors mismatch.");
        using JsonDocument document = JsonDocument.Parse(bytes, WorldPackageJson.DocumentOptions);
        JsonElement root = document.RootElement;
        if (Sha256Digest.Parse(root.GetProperty("environmentStateDigest").GetString()!) != environment.Digest)
            throw new JsonException("Environment snapshot state digest mismatch.");
        try
        {
            AcceptedSessionCommand[] pending = root.GetProperty("pendingCommands").EnumerateArray().Select(ParseCommand).ToArray();
            FoundationIssue[] faults = root.GetProperty("faultIssues").EnumerateArray().Select(ParseIssue).ToArray();
            WorldSessionSnapshot snapshot = WorldSessionSnapshot.CreateEnvironmentValidated(
                SemanticVersion.Parse(root.GetProperty("formatVersion").GetString()!),
                definition,
                JsonSerializer.Deserialize<SimulationTick>(root.GetProperty("currentTick"), JsonDefaults.Compact),
                JsonSerializer.Deserialize<WorldSessionStatus>(root.GetProperty("status"), JsonDefaults.Compact),
                JsonSerializer.Deserialize<SequenceNumber>(root.GetProperty("lastCommandSequence"), JsonDefaults.Compact),
                JsonSerializer.Deserialize<SequenceNumber>(root.GetProperty("lastEventSequence"), JsonDefaults.Compact),
                pending,
                faults,
                environment,
                Sha256Digest.Parse(root.GetProperty("stateDigest").GetString()!),
                Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
            byte[] canonical = Serialize(snapshot, expectedChunks);
            if (!bytes.AsSpan().SequenceEqual(canonical)) throw new JsonException("snapshot.json is not the exact supported compact V2 serialization.");
            return snapshot;
        }
        catch (JsonException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        { throw new JsonException("Invalid environment snapshot metadata.", exception); }
    }

    private static AcceptedSessionCommand ParseCommand(JsonElement element)
    {
        WorldPackageJson.Exact(element, "sequenceNumber", "acceptedAtTick", "executeAtTick", "commandType", "payload");
        return new(
            JsonSerializer.Deserialize<SequenceNumber>(element.GetProperty("sequenceNumber"), JsonDefaults.Compact),
            JsonSerializer.Deserialize<SimulationTick>(element.GetProperty("acceptedAtTick"), JsonDefaults.Compact),
            JsonSerializer.Deserialize<SimulationTick>(element.GetProperty("executeAtTick"), JsonDefaults.Compact),
            JsonSerializer.Deserialize<SessionCommandTypeId>(element.GetProperty("commandType"), JsonDefaults.Compact),
            JsonSerializer.Deserialize<ImmutableConfiguration>(element.GetProperty("payload"), JsonDefaults.Compact) ?? throw new JsonException("Command payload is missing."));
    }

    private static FoundationIssue ParseIssue(JsonElement element)
    {
        WorldPackageJson.Exact(element, "code", "severity", "summary", "detail");
        return new(
            IssueCode.Parse(element.GetProperty("code").GetString()!),
            JsonSerializer.Deserialize<IssueSeverity>(element.GetProperty("severity"), JsonDefaults.Compact),
            element.GetProperty("summary").GetString() ?? throw new JsonException("Issue summary is missing."),
            element.GetProperty("detail").GetString() ?? throw new JsonException("Issue detail is missing."));
    }
}
