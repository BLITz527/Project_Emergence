using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Randomness;

[JsonConverter(typeof(RngSeed256JsonConverter))]
public readonly struct RngSeed256 : IEquatable<RngSeed256>, IComparable<RngSeed256>
{
    private readonly ulong _a, _b, _c, _d;
    public RngSeed256(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32) throw new ArgumentException("An RNG seed must contain exactly 32 bytes.", nameof(bytes));
        _a = BinaryPrimitives.ReadUInt64BigEndian(bytes); _b = BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]);
        _c = BinaryPrimitives.ReadUInt64BigEndian(bytes[16..]); _d = BinaryPrimitives.ReadUInt64BigEndian(bytes[24..]);
    }
    public static RngSeed256 Parse(string text) { Span<byte> bytes = stackalloc byte[32]; return Hex256.TryParse(text, bytes) ? new(bytes) : throw new FormatException("An RNG seed must contain exactly 64 hexadecimal characters."); }
    public static bool TryParse(string? text, out RngSeed256 value) { value = default; Span<byte> bytes = stackalloc byte[32]; if (!Hex256.TryParse(text, bytes)) return false; value = new(bytes); return true; }
    public bool TryCopyTo(Span<byte> destination) { if (destination.Length < 32) return false; BinaryPrimitives.WriteUInt64BigEndian(destination, _a); BinaryPrimitives.WriteUInt64BigEndian(destination[8..], _b); BinaryPrimitives.WriteUInt64BigEndian(destination[16..], _c); BinaryPrimitives.WriteUInt64BigEndian(destination[24..], _d); return true; }
    public byte[] ToByteArray() { byte[] result = new byte[32]; TryCopyTo(result); return result; }
    public int CompareTo(RngSeed256 other) { int c = _a.CompareTo(other._a); if (c != 0) return c; c = _b.CompareTo(other._b); if (c != 0) return c; c = _c.CompareTo(other._c); return c != 0 ? c : _d.CompareTo(other._d); }
    public bool Equals(RngSeed256 other) => _a == other._a && _b == other._b && _c == other._c && _d == other._d;
    public override bool Equals(object? obj) => obj is RngSeed256 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);
    public override string ToString() => Hex256.Format(this, static (value, bytes) => value.TryCopyTo(bytes));
    public static bool operator ==(RngSeed256 left, RngSeed256 right) => left.Equals(right);
    public static bool operator !=(RngSeed256 left, RngSeed256 right) => !left.Equals(right);
}

[JsonConverter(typeof(RngDomainIdJsonConverter))]
public readonly record struct RngDomainId : IComparable<RngDomainId>
{
    public RngDomainId(string value) { DottedName.Validate(value, 96, 32, nameof(value)); Value = value; }
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static RngDomainId Parse(string text) => new(text);
    public static bool TryParse(string? text, out RngDomainId value) { try { value = new(text!); return true; } catch (ArgumentException) { value = default; return false; } }
    public int CompareTo(RngDomainId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(RngScopeKeyJsonConverter))]
public readonly record struct RngScopeKey : IComparable<RngScopeKey>
{
    public RngScopeKey(StableId128 value) { if (value.IsEmpty) throw new ArgumentException("An RNG scope cannot be empty.", nameof(value)); Value = value; }
    public StableId128 Value { get; }
    public bool IsEmpty => Value.IsEmpty;
    public static RngScopeKey FromStableId(StableId128 value) => new(value);
    public static RngScopeKey Parse(string text) => new(StableId128.Parse(text));
    public static bool TryParse(string? text, out RngScopeKey value) { try { value = Parse(text!); return true; } catch (Exception e) when (e is ArgumentException or FormatException) { value = default; return false; } }
    public int CompareTo(RngScopeKey other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(RngSampleAddressJsonConverter))]
public readonly record struct RngSampleAddress : IComparable<RngSampleAddress>
{
    public RngSampleAddress(RngDomainId domain, RngScopeKey scope, UInt128 sampleIndex)
    {
        if (domain.IsEmpty) throw new ArgumentException("RNG domain cannot be empty.", nameof(domain));
        if (scope.IsEmpty) throw new ArgumentException("RNG scope cannot be empty.", nameof(scope));
        Domain = domain; Scope = scope; SampleIndex = sampleIndex;
    }
    public RngDomainId Domain { get; }
    public RngScopeKey Scope { get; }
    public UInt128 SampleIndex { get; }
    public int CompareTo(RngSampleAddress other) { int c = Domain.CompareTo(other.Domain); if (c != 0) return c; c = Scope.CompareTo(other.Scope); return c != 0 ? c : SampleIndex.CompareTo(other.SampleIndex); }
}

[JsonConverter(typeof(RngBlock256JsonConverter))]
public readonly struct RngBlock256 : IEquatable<RngBlock256>, IComparable<RngBlock256>
{
    private readonly ulong _a, _b, _c, _d;
    public RngBlock256(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32) throw new ArgumentException("An RNG block must contain exactly 32 bytes.", nameof(bytes));
        _a = BinaryPrimitives.ReadUInt64BigEndian(bytes); _b = BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]);
        _c = BinaryPrimitives.ReadUInt64BigEndian(bytes[16..]); _d = BinaryPrimitives.ReadUInt64BigEndian(bytes[24..]);
    }
    public static RngBlock256 Parse(string text) { Span<byte> bytes = stackalloc byte[32]; return Hex256.TryParse(text, bytes) ? new(bytes) : throw new FormatException("An RNG block must contain exactly 64 hexadecimal characters."); }
    public static bool TryParse(string? text, out RngBlock256 value) { value = default; Span<byte> bytes = stackalloc byte[32]; if (!Hex256.TryParse(text, bytes)) return false; value = new(bytes); return true; }
    public bool TryCopyTo(Span<byte> destination) { if (destination.Length < 32) return false; BinaryPrimitives.WriteUInt64BigEndian(destination, _a); BinaryPrimitives.WriteUInt64BigEndian(destination[8..], _b); BinaryPrimitives.WriteUInt64BigEndian(destination[16..], _c); BinaryPrimitives.WriteUInt64BigEndian(destination[24..], _d); return true; }
    public ulong GetLane(int index) { if ((uint)index > 3) throw new ArgumentOutOfRangeException(nameof(index)); Span<byte> bytes = stackalloc byte[32]; TryCopyTo(bytes); return BinaryPrimitives.ReadUInt64LittleEndian(bytes[(index * 8)..]); }
    public int CompareTo(RngBlock256 other) { int c = _a.CompareTo(other._a); if (c != 0) return c; c = _b.CompareTo(other._b); if (c != 0) return c; c = _c.CompareTo(other._c); return c != 0 ? c : _d.CompareTo(other._d); }
    public bool Equals(RngBlock256 other) => _a == other._a && _b == other._b && _c == other._c && _d == other._d;
    public override bool Equals(object? obj) => obj is RngBlock256 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);
    public override string ToString() => Hex256.Format(this, static (value, bytes) => value.TryCopyTo(bytes));
}

[JsonConverter(typeof(RngDomainCatalogJsonConverter))]
public sealed class RngDomainCatalog : IEquatable<RngDomainCatalog>
{
    public const string DigestDomainMarker = "ProjectEmergence.RngDomainCatalog.v1";
    private readonly RngDomainId[] _entryArray;
    private readonly IReadOnlyList<RngDomainId> _entries;
    public RngDomainCatalog(IEnumerable<RngDomainId> entries)
    {
        ArgumentNullException.ThrowIfNull(entries); RngDomainId[] sorted = entries.Order().ToArray();
        if (sorted.Any(static x => x.IsEmpty)) throw new ArgumentException("Default RNG domains are not allowed.", nameof(entries));
        if (sorted.Distinct().Count() != sorted.Length) throw new ArgumentException("Duplicate RNG domains are not allowed.", nameof(entries));
        _entryArray = sorted; _entries = Array.AsReadOnly(sorted); Digest = ComputeDigest(sorted);
    }
    public IReadOnlyList<RngDomainId> Entries => _entries;
    public Sha256Digest Digest { get; }
    public bool Contains(RngDomainId domain) => !domain.IsEmpty && Array.BinarySearch(_entryArray, domain) >= 0;
    public static RngDomainCatalog Phase03 { get; } = new([new("foundation.reference"), new("foundation.ruleset-validation"), new("foundation.self-test")]);
    public bool Equals(RngDomainCatalog? other) => other is not null && _entries.SequenceEqual(other._entries);
    public override bool Equals(object? obj) => obj is RngDomainCatalog other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();
    private static Sha256Digest ComputeDigest(IReadOnlyList<RngDomainId> entries) { using CanonicalHashWriter writer = new(); writer.WriteString(DigestDomainMarker); writer.WriteUInt64((ulong)entries.Count); foreach (RngDomainId entry in entries) writer.WriteString(entry.ToString()); return writer.FinalizeDigest(); }
    internal static RngDomainCatalog CreateValidated(IEnumerable<RngDomainId> entries, Sha256Digest digest) { RngDomainCatalog result = new(entries); return result.Digest == digest ? result : throw new JsonException("RNG domain catalog digest mismatch."); }
}

public sealed class DeterministicAddressedRng
{
    private const string DomainMarker = "ProjectEmergence.RngBlock.v1";
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("PE-CANONICAL/1\0");
    public const string AlgorithmReference = "foundation.rng-addressed-sha256@1.0.0";
    public DeterministicAddressedRng(RngSeed256 rootSeed, RngDomainCatalog domains) { RootSeed = rootSeed; Domains = domains ?? throw new ArgumentNullException(nameof(domains)); }
    public RngSeed256 RootSeed { get; }
    public RngDomainCatalog Domains { get; }
    public RngBlock256 GenerateBlock(RngSampleAddress address) => GenerateBlock(address, 0);
    public RngBlock256 GenerateBlock(RngSampleAddress address, ulong attempt)
    {
        Span<byte> encoded = stackalloc byte[256]; int length = Encode(address, attempt, encoded); Span<byte> hash = stackalloc byte[32]; SHA256.HashData(encoded[..length], hash); return new(hash);
    }
    public byte[] GetCanonicalEncoding(RngSampleAddress address, ulong attempt = 0) { Span<byte> encoded = stackalloc byte[256]; int length = Encode(address, attempt, encoded); return encoded[..length].ToArray(); }
    public ulong SampleUInt64(RngSampleAddress address) => GenerateBlock(address).GetLane(0);
    public bool SampleBoolean(RngSampleAddress address) => (SampleUInt64(address) & 1) != 0;
    public ulong SampleUInt64Below(RngSampleAddress address, ulong exclusiveUpperBound)
    {
        return SampleUInt64BelowCore(new AddressCandidateSource(this, address), exclusiveUpperBound, 0);
    }
    internal static ulong SampleUInt64BelowCore<TSource>(TSource source, ulong exclusiveUpperBound, ulong firstAttempt) where TSource : struct, IRngCandidateSource
    {
        if (exclusiveUpperBound == 0) throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
        ulong threshold = unchecked(0UL - exclusiveUpperBound) % exclusiveUpperBound;
        for (ulong attempt = firstAttempt; ; attempt = checked(attempt + 1)) { ulong candidate = source.Candidate(attempt); if (candidate >= threshold) return candidate % exclusiveUpperBound; }
    }
    private int Encode(RngSampleAddress address, ulong attempt, Span<byte> destination)
    {
        if (!Domains.Contains(address.Domain)) throw new ArgumentException("The RNG domain is not registered in this catalog.", nameof(address));
        int offset = 0; Header.CopyTo(destination); offset += Header.Length;
        WriteString(DomainMarker, destination, ref offset);
        destination[offset++] = 0x02; BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], 32); offset += 8; RootSeed.TryCopyTo(destination[offset..]); offset += 32;
        WriteString(address.Domain.ToString(), destination, ref offset);
        destination[offset++] = 0x03; BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], address.Scope.Value.High); offset += 8;
        destination[offset++] = 0x03; BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], address.Scope.Value.Low); offset += 8;
        destination[offset++] = 0x04; BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], (ulong)address.SampleIndex); BinaryPrimitives.WriteUInt64LittleEndian(destination[(offset + 8)..], (ulong)(address.SampleIndex >> 64)); offset += 16;
        destination[offset++] = 0x03; BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], attempt); offset += 8;
        return offset;
    }
    private static void WriteString(string value, Span<byte> destination, ref int offset) { destination[offset++] = 0x01; int count = Encoding.UTF8.GetByteCount(value); BinaryPrimitives.WriteUInt64LittleEndian(destination[offset..], (ulong)count); offset += 8; offset += Encoding.UTF8.GetBytes(value, destination[offset..]); }
    private readonly record struct AddressCandidateSource(DeterministicAddressedRng Rng, RngSampleAddress Address) : IRngCandidateSource { public ulong Candidate(ulong attempt) => Rng.GenerateBlock(Address, attempt).GetLane(0); }
}

internal interface IRngCandidateSource { ulong Candidate(ulong attempt); }

internal static class Hex256
{
    public static bool TryParse(string? text, Span<byte> destination)
    {
        if (text?.Length != 64 || destination.Length < 32) return false;
        for (int index = 0; index < 32; index++)
        {
            int high = HexValue(text[index * 2]); int low = HexValue(text[(index * 2) + 1]);
            if (high < 0 || low < 0) return false;
            destination[index] = (byte)((high << 4) | low);
        }
        return true;
    }
    public static string Format<T>(T value, Func<T, Span<byte>, bool> copy) { Span<byte> bytes = stackalloc byte[32]; copy(value, bytes); return Convert.ToHexStringLower(bytes); }
    private static int HexValue(char value) => value switch { >= '0' and <= '9' => value - '0', >= 'a' and <= 'f' => value - 'a' + 10, >= 'A' and <= 'F' => value - 'A' + 10, _ => -1 };
}

internal sealed class RngSeed256JsonConverter : JsonConverter<RngSeed256> { public override RngSeed256 Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType == JsonTokenType.String ? RngSeed256.Parse(r.GetString()!) : throw new JsonException(); public override void Write(Utf8JsonWriter w, RngSeed256 v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString()); }
internal sealed class RngDomainIdJsonConverter : JsonConverter<RngDomainId> { public override RngDomainId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType == JsonTokenType.String ? RngDomainId.Parse(r.GetString()!) : throw new JsonException(); public override void Write(Utf8JsonWriter w, RngDomainId v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString()); }
internal sealed class RngScopeKeyJsonConverter : JsonConverter<RngScopeKey> { public override RngScopeKey Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType == JsonTokenType.String ? RngScopeKey.Parse(r.GetString()!) : throw new JsonException(); public override void Write(Utf8JsonWriter w, RngScopeKey v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString()); }
internal sealed class RngBlock256JsonConverter : JsonConverter<RngBlock256> { public override RngBlock256 Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType == JsonTokenType.String ? RngBlock256.Parse(r.GetString()!) : throw new JsonException(); public override void Write(Utf8JsonWriter w, RngBlock256 v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString()); }
internal sealed class RngSampleAddressJsonConverter : JsonConverter<RngSampleAddress>
{
    public override RngSampleAddress Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) { using JsonDocument d = JsonDocument.ParseValue(ref r); JsonElement x = d.RootElement; string sample = x.GetProperty("sampleIndex").GetString()!; if (!UInt128.TryParse(sample, NumberStyles.None, CultureInfo.InvariantCulture, out UInt128 index) || index.ToString(CultureInfo.InvariantCulture) != sample) throw new JsonException("RNG sample index must be canonical unsigned decimal text."); return new(new(x.GetProperty("domain").GetString()!), RngScopeKey.Parse(x.GetProperty("scope").GetString()!), index); }
    public override void Write(Utf8JsonWriter w, RngSampleAddress v, JsonSerializerOptions o) { w.WriteStartObject(); w.WriteString("domain", v.Domain.ToString()); w.WriteString("scope", v.Scope.ToString()); w.WriteString("sampleIndex", v.SampleIndex.ToString(CultureInfo.InvariantCulture)); w.WriteEndObject(); }
}
internal sealed class RngDomainCatalogJsonConverter : JsonConverter<RngDomainCatalog>
{
    public override RngDomainCatalog Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) { using JsonDocument d = JsonDocument.ParseValue(ref r); JsonElement x = d.RootElement; return RngDomainCatalog.CreateValidated(x.GetProperty("entries").EnumerateArray().Select(y => new RngDomainId(y.GetString()!)), Sha256Digest.Parse(x.GetProperty("digest").GetString()!)); }
    public override void Write(Utf8JsonWriter w, RngDomainCatalog v, JsonSerializerOptions o) { w.WriteStartObject(); w.WritePropertyName("entries"); w.WriteStartArray(); foreach (RngDomainId x in v.Entries) w.WriteStringValue(x.ToString()); w.WriteEndArray(); w.WriteString("digest", v.Digest.ToString()); w.WriteEndObject(); }
}
