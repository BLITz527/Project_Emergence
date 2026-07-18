using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;

namespace Emergence.Model;

[JsonConverter(typeof(CommandProcessorCatalogJsonConverter))]
public sealed class CommandProcessorCatalog : IEquatable<CommandProcessorCatalog>
{
    public const string DigestDomainMarker = "ProjectEmergence.CommandProcessorCatalog.v1";
    private readonly ReadOnlyCollection<SessionCommandTypeId> _commandTypes;

    public CommandProcessorCatalog(IEnumerable<SessionCommandTypeId> commandTypes)
    {
        ArgumentNullException.ThrowIfNull(commandTypes);
        SessionCommandTypeId[] sorted = commandTypes.ToArray();
        if (sorted.Length > SessionTechnicalLimits.MaxCommandProcessors)
            throw new ArgumentException($"A command processor catalog cannot exceed {SessionTechnicalLimits.MaxCommandProcessors} entries.", nameof(commandTypes));
        if (sorted.Any(static item => !item.IsValid))
            throw new ArgumentException("Command processor catalog entries must be valid.", nameof(commandTypes));
        Array.Sort(sorted);
        if (sorted.Distinct().Count() != sorted.Length)
            throw new ArgumentException("Duplicate command processor catalog entries are not allowed.", nameof(commandTypes));

        _commandTypes = Array.AsReadOnly(sorted);
        Digest = ComputeDigest(sorted);
    }

    public IReadOnlyList<SessionCommandTypeId> CommandTypes => _commandTypes;
    public Sha256Digest Digest { get; }

    public bool Contains(SessionCommandTypeId commandType) =>
        commandType.IsValid && _commandTypes.BinarySearch(commandType) >= 0;

    public bool Equals(CommandProcessorCatalog? other) =>
        other is not null && _commandTypes.SequenceEqual(other._commandTypes);

    public override bool Equals(object? obj) => obj is CommandProcessorCatalog other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private static Sha256Digest ComputeDigest(IReadOnlyList<SessionCommandTypeId> commandTypes)
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteUInt64(checked((ulong)commandTypes.Count));
        foreach (SessionCommandTypeId commandType in commandTypes) writer.WriteString(commandType.ToString());
        return writer.FinalizeDigest();
    }

    internal static CommandProcessorCatalog CreateValidated(IEnumerable<SessionCommandTypeId> commandTypes, Sha256Digest expected)
    {
        CommandProcessorCatalog catalog = new(commandTypes);
        return catalog.Digest == expected ? catalog : throw new JsonException("Command processor catalog digest mismatch.");
    }
}

internal sealed class CommandProcessorCatalogJsonConverter : JsonConverter<CommandProcessorCatalog>
{
    public override CommandProcessorCatalog Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        StrictModelJson.Exact(root, "commandTypes", "digest");
        SessionCommandTypeId[] commandTypes = root.GetProperty("commandTypes").EnumerateArray()
            .Select(item => JsonSerializer.Deserialize<SessionCommandTypeId>(item, options))
            .ToArray();
        return CommandProcessorCatalog.CreateValidated(commandTypes, Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
    }

    public override void Write(Utf8JsonWriter writer, CommandProcessorCatalog value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WritePropertyName("commandTypes");
        writer.WriteStartArray();
        foreach (SessionCommandTypeId commandType in value.CommandTypes) JsonSerializer.Serialize(writer, commandType, options);
        writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }
}

internal static class ReadOnlyListSearchExtensions
{
    public static int BinarySearch<T>(this IReadOnlyList<T> source, T value) where T : IComparable<T>
    {
        int low = 0;
        int high = source.Count - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            int comparison = source[middle].CompareTo(value);
            if (comparison == 0) return middle;
            if (comparison < 0) low = middle + 1;
            else high = middle - 1;
        }
        return ~low;
    }
}
