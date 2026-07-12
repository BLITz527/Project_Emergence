using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation;

internal sealed class CanonicalStringJsonConverter<T>(Func<string, T> parse) : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? parse(reader.GetString()!) : throw new JsonException($"{typeof(T).Name} must be a JSON string.");
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(value?.ToString());
}

internal sealed class TypedIdJsonConverter<T> : JsonConverter<T> where T : struct, IStableIdentifier<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException($"{typeof(T).Name} must be a JSON string.");
        try { return T.FromStableId(StableId128.Parse(reader.GetString()!)); }
        catch (FormatException exception) { throw new JsonException($"Malformed {typeof(T).Name}.", exception); }
    }
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value.ToString());
}

internal sealed class CheckedSequenceCounterJsonConverter : JsonConverter<CheckedSequenceCounter>
{
    public override CheckedSequenceCounter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return new CheckedSequenceCounter(SequenceNumber.Parse(document.RootElement.GetProperty("lastIssued").GetString()!));
    }
    public override void Write(Utf8JsonWriter writer, CheckedSequenceCounter value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteString("lastIssued", value.LastIssued.ToString()); writer.WriteEndObject();
    }
}

internal static class FoundationJsonConverters
{
    public static void AddTo(JsonSerializerOptions options)
    {
        options.Converters.Add(new CanonicalStringJsonConverter<StableId128>(StableId128.Parse));
        options.Converters.Add(new TypedIdJsonConverter<WorldId>()); options.Converters.Add(new TypedIdJsonConverter<BranchId>());
        options.Converters.Add(new TypedIdJsonConverter<RegionId>()); options.Converters.Add(new TypedIdJsonConverter<CellId>());
        options.Converters.Add(new TypedIdJsonConverter<GenomeId>()); options.Converters.Add(new TypedIdJsonConverter<LineageId>());
        options.Converters.Add(new TypedIdJsonConverter<BondId>()); options.Converters.Add(new TypedIdJsonConverter<CollectiveId>());
        options.Converters.Add(new TypedIdJsonConverter<OrganismId>()); options.Converters.Add(new TypedIdJsonConverter<EventId>());
        options.Converters.Add(new TypedIdJsonConverter<SnapshotId>()); options.Converters.Add(new TypedIdJsonConverter<RulesetId>());
        options.Converters.Add(new CanonicalStringJsonConverter<SimulationTick>(SimulationTick.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<TickSpan>(TickSpan.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<SequenceNumber>(SequenceNumber.Parse));
        options.Converters.Add(new CheckedSequenceCounterJsonConverter());
        options.Converters.Add(new CanonicalStringJsonConverter<MatterAmount>(MatterAmount.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<EnergyAmount>(EnergyAmount.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<Sha256Digest>(Sha256Digest.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<SemanticVersion>(SemanticVersion.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<AlgorithmId>(AlgorithmId.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<AlgorithmReference>(AlgorithmReference.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<ConfigurationSchemaId>(ConfigurationSchemaId.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<ConfigurationKey>(ConfigurationKey.Parse));
        options.Converters.Add(new CanonicalStringJsonConverter<IssueCode>(IssueCode.Parse));
        options.Converters.Add(new JsonStringEnumConverter<IssueSeverity>());
    }
}
