using System.Globalization;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Configuration;

public readonly record struct ConfigurationSchemaId : IComparable<ConfigurationSchemaId>
{
    public ConfigurationSchemaId(string value) { DottedName.Validate(value, 96, 32, nameof(value)); Value = value; }
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static ConfigurationSchemaId Parse(string text) => new(text);
    public static bool TryParse(string? text, out ConfigurationSchemaId value) { try { value = new(text!); return true; } catch (ArgumentException) { value = default; return false; } }
    public int CompareTo(ConfigurationSchemaId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ConfigurationKey : IComparable<ConfigurationKey>
{
    public ConfigurationKey(string value) { DottedName.Validate(value, 128, 48, nameof(value)); Value = value; }
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static ConfigurationKey Parse(string text) => new(text);
    public static bool TryParse(string? text, out ConfigurationKey value) { try { value = new(text!); return true; } catch (ArgumentException) { value = default; return false; } }
    public int CompareTo(ConfigurationKey other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;
}

public enum ConfigurationValueKind { Boolean, Int64, UInt64, Decimal, String, Digest }

[JsonConverter(typeof(ConfigurationValueJsonConverter))]
public readonly struct ConfigurationValue : IEquatable<ConfigurationValue>
{
    private readonly bool _boolean;
    private readonly long _int64;
    private readonly ulong _uint64;
    private readonly decimal _decimal;
    private readonly string? _string;
    private readonly Sha256Digest _digest;

    private ConfigurationValue(ConfigurationValueKind kind, bool boolean = default, long int64 = default, ulong uint64 = default, decimal decimalValue = default, string? stringValue = default, Sha256Digest digest = default)
    { Kind = kind; _boolean = boolean; _int64 = int64; _uint64 = uint64; _decimal = decimalValue; _string = stringValue; _digest = digest; }

    public ConfigurationValueKind Kind { get; }
    public bool Boolean => Kind == ConfigurationValueKind.Boolean ? _boolean : throw WrongKind();
    public long Int64 => Kind == ConfigurationValueKind.Int64 ? _int64 : throw WrongKind();
    public ulong UInt64 => Kind == ConfigurationValueKind.UInt64 ? _uint64 : throw WrongKind();
    public decimal Decimal => Kind == ConfigurationValueKind.Decimal ? _decimal : throw WrongKind();
    public string String => Kind == ConfigurationValueKind.String ? _string! : throw WrongKind();
    public Sha256Digest Digest => Kind == ConfigurationValueKind.Digest ? _digest : throw WrongKind();

    public static ConfigurationValue FromBoolean(bool value) => new(ConfigurationValueKind.Boolean, boolean: value);
    public static ConfigurationValue FromInt64(long value) => new(ConfigurationValueKind.Int64, int64: value);
    public static ConfigurationValue FromUInt64(ulong value) => new(ConfigurationValueKind.UInt64, uint64: value);
    public static ConfigurationValue FromDecimal(decimal value) => new(ConfigurationValueKind.Decimal, decimalValue: value == 0m ? 0m : value);
    public static ConfigurationValue FromString(string value) { ArgumentNullException.ThrowIfNull(value); return new(ConfigurationValueKind.String, stringValue: value); }
    public static ConfigurationValue FromDigest(Sha256Digest value) => new(ConfigurationValueKind.Digest, digest: value);

    public string CanonicalText() => Kind switch
    {
        ConfigurationValueKind.Boolean => _boolean ? "true" : "false",
        ConfigurationValueKind.Int64 => _int64.ToString(CultureInfo.InvariantCulture),
        ConfigurationValueKind.UInt64 => _uint64.ToString(CultureInfo.InvariantCulture),
        ConfigurationValueKind.Decimal => _decimal.ToString("G29", CultureInfo.InvariantCulture),
        ConfigurationValueKind.String => _string!,
        ConfigurationValueKind.Digest => _digest.ToString(),
        _ => throw new InvalidOperationException("Unknown configuration value kind."),
    };

    public bool Equals(ConfigurationValue other) => Kind == other.Kind && Kind switch
    {
        ConfigurationValueKind.Boolean => _boolean == other._boolean,
        ConfigurationValueKind.Int64 => _int64 == other._int64,
        ConfigurationValueKind.UInt64 => _uint64 == other._uint64,
        ConfigurationValueKind.Decimal => _decimal == other._decimal,
        ConfigurationValueKind.String => string.Equals(_string, other._string, StringComparison.Ordinal),
        ConfigurationValueKind.Digest => _digest == other._digest,
        _ => false,
    };
    public override bool Equals(object? obj) => obj is ConfigurationValue other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Kind, CanonicalText());
    public static bool operator ==(ConfigurationValue left, ConfigurationValue right) => left.Equals(right);
    public static bool operator !=(ConfigurationValue left, ConfigurationValue right) => !left.Equals(right);
    private InvalidOperationException WrongKind() => new($"Configuration value kind is {Kind}.");
}

public sealed record ConfigurationEntry(
    [property: JsonPropertyOrder(0)] ConfigurationKey Key,
    [property: JsonPropertyOrder(1)] ConfigurationValue Value);
