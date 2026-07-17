using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;

namespace Emergence.Foundation.Configuration;

internal sealed class ConfigurationValueJsonConverter : JsonConverter<ConfigurationValue>
{
    public override ConfigurationValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        JsonElement kindElement = root.GetProperty("kind");
        if (kindElement.ValueKind != JsonValueKind.String) throw new JsonException("Configuration value kind must be an exact canonical string.");
        ConfigurationValueKind kind = kindElement.GetString() switch
        {
            "Boolean" => ConfigurationValueKind.Boolean,
            "Int64" => ConfigurationValueKind.Int64,
            "UInt64" => ConfigurationValueKind.UInt64,
            "Decimal" => ConfigurationValueKind.Decimal,
            "String" => ConfigurationValueKind.String,
            "Digest" => ConfigurationValueKind.Digest,
            _ => throw new JsonException("Unknown configuration value kind."),
        };
        JsonElement value = root.GetProperty("value");
        try
        {
            return kind switch
            {
                ConfigurationValueKind.Boolean => ConfigurationValue.FromBoolean(value.GetBoolean()),
                ConfigurationValueKind.Int64 => ConfigurationValue.FromInt64(ParseInt64(value.GetString()!)),
                ConfigurationValueKind.UInt64 => ConfigurationValue.FromUInt64(ParseUInt64(value.GetString()!)),
                ConfigurationValueKind.Decimal => ConfigurationValue.FromDecimal(ParseDecimal(value.GetString()!)),
                ConfigurationValueKind.String => ConfigurationValue.FromString(value.GetString() ?? throw new JsonException("Configuration strings cannot be null.")),
                ConfigurationValueKind.Digest => ConfigurationValue.FromDigest(Sha256Digest.Parse(value.GetString()!)),
                _ => throw new JsonException("Unknown configuration value kind."),
            };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or InvalidOperationException) { throw new JsonException("Malformed configuration value.", exception); }
    }

    public override void Write(Utf8JsonWriter writer, ConfigurationValue value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value.Kind)) throw new JsonException("Undefined configuration value kinds cannot be written.");
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind.ToString());
        writer.WritePropertyName("value");
        if (value.Kind == ConfigurationValueKind.Boolean) writer.WriteBooleanValue(value.Boolean); else writer.WriteStringValue(value.CanonicalText());
        writer.WriteEndObject();
    }

    private static long ParseInt64(string text) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
        && value.ToString(CultureInfo.InvariantCulture) == text
            ? value : throw new FormatException("Int64 configuration text is not canonical.");

    private static ulong ParseUInt64(string text) =>
        ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
        && value.ToString(CultureInfo.InvariantCulture) == text
            ? value : throw new FormatException("UInt64 configuration text is not canonical.");

    private static decimal ParseDecimal(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)) throw new FormatException("Decimal configuration text is malformed.");
        value = value == 0m ? 0m : value;
        return value.ToString("G29", CultureInfo.InvariantCulture) == text ? value : throw new FormatException("Decimal configuration text is not canonical.");
    }
}
