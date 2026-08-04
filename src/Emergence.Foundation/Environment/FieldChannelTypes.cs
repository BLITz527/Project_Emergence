using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emergence.Foundation.Fields;

[JsonConverter(typeof(FieldChannelIdJsonConverter))]
public readonly record struct FieldChannelId : IComparable<FieldChannelId>
{
    public FieldChannelId(string value)
    {
        Validate(value, nameof(value));
        Value = value;
    }

    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);
    public static FieldChannelId Parse(string text) => new(text);
    public static bool TryParse(string? text, out FieldChannelId value)
    {
        try { value = new(text!); return true; }
        catch (ArgumentException) { value = default; return false; }
    }
    public int CompareTo(FieldChannelId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;

    private static void Validate(string? value, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 96)
            throw new ArgumentException("Field channel ID length must be 1 through 96.", parameterName);
        foreach (string segment in value.Split('.'))
        {
            if (segment.Length is 0 or > 32
                || segment[0] is < 'a' or > 'z'
                || segment.Skip(1).Any(static character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
                throw new ArgumentException("Field channel IDs require canonical lowercase ASCII dotted segments.", parameterName);
        }
    }
}

[JsonConverter(typeof(FieldChannelRoleJsonConverter))]
public enum FieldChannelRole
{
    ConservedMaterial = 0,
}

internal sealed class FieldChannelIdJsonConverter : JsonConverter<FieldChannelId>
{
    public override FieldChannelId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("FieldChannelId must be an exact canonical JSON string.");
        try { return new FieldChannelId(reader.GetString()!); }
        catch (ArgumentException exception) { throw new JsonException("Malformed FieldChannelId.", exception); }
    }

    public override void Write(Utf8JsonWriter writer, FieldChannelId value, JsonSerializerOptions options)
    {
        if (!value.IsValid) throw new JsonException("Invalid FieldChannelId values cannot be written.");
        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class FieldChannelRoleJsonConverter : JsonConverter<FieldChannelRole>
{
    public override FieldChannelRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || reader.GetString() != "ConservedMaterial")
            throw new JsonException("FieldChannelRole must be the exact string ConservedMaterial.");
        return FieldChannelRole.ConservedMaterial;
    }

    public override void Write(Utf8JsonWriter writer, FieldChannelRole value, JsonSerializerOptions options)
    {
        if (value != FieldChannelRole.ConservedMaterial) throw new JsonException("Undefined FieldChannelRole values cannot be written.");
        writer.WriteStringValue("ConservedMaterial");
    }
}
