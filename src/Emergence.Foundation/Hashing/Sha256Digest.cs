using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Emergence.Foundation.Text;

namespace Emergence.Foundation.Hashing;

public readonly struct Sha256Digest : IEquatable<Sha256Digest>, IComparable<Sha256Digest>
{
    private readonly ulong _part0;
    private readonly ulong _part1;
    private readonly ulong _part2;
    private readonly ulong _part3;

    public Sha256Digest(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32) throw new ArgumentException("A SHA-256 digest must contain exactly 32 bytes.", nameof(bytes));
        _part0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes[..8]);
        _part1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes[8..16]);
        _part2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes[16..24]);
        _part3 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes[24..32]);
    }

    public static Sha256Digest Compute(ReadOnlySpan<byte> bytes) => new(SHA256.HashData(bytes));
    public static Sha256Digest ComputeUtf8(string text) { ArgumentNullException.ThrowIfNull(text); return Compute(StrictUtf8.GetBytes(text)); }
    public static Sha256Digest Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out Sha256Digest value) ? value : throw new FormatException("A SHA-256 digest must contain exactly 64 hexadecimal characters.");
    }
    public static bool TryParse(string? text, out Sha256Digest value)
    {
        value = default;
        if (text?.Length != 64) return false;
        Span<byte> bytes = stackalloc byte[32];
        for (int index = 0; index < bytes.Length; index++)
        {
            int high = HexValue(text[index * 2]);
            int low = HexValue(text[(index * 2) + 1]);
            if (high < 0 || low < 0) return false;
            bytes[index] = (byte)((high << 4) | low);
        }
        value = new(bytes);
        return true;
    }
    public bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length < 32) return false;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(destination[..8], _part0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(destination[8..16], _part1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(destination[16..24], _part2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(destination[24..32], _part3);
        return true;
    }
    public byte[] ToByteArray() { byte[] bytes = new byte[32]; TryCopyTo(bytes); return bytes; }
    public override string ToString() => string.Create(64, this, static (span, value) => Convert.TryToHexStringLower(value.ToByteArray(), span, out _));
    public int CompareTo(Sha256Digest other)
    {
        int result = _part0.CompareTo(other._part0); if (result != 0) return result;
        result = _part1.CompareTo(other._part1); if (result != 0) return result;
        result = _part2.CompareTo(other._part2); return result != 0 ? result : _part3.CompareTo(other._part3);
    }
    public bool Equals(Sha256Digest other) => _part0 == other._part0 && _part1 == other._part1 && _part2 == other._part2 && _part3 == other._part3;
    public override bool Equals(object? obj) => obj is Sha256Digest other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_part0, _part1, _part2, _part3);
    public static bool operator ==(Sha256Digest left, Sha256Digest right) => left.Equals(right);
    public static bool operator !=(Sha256Digest left, Sha256Digest right) => !left.Equals(right);
    public static bool operator <(Sha256Digest left, Sha256Digest right) => left.CompareTo(right) < 0;
    public static bool operator >(Sha256Digest left, Sha256Digest right) => left.CompareTo(right) > 0;

    private static int HexValue(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'a' and <= 'f' => value - 'a' + 10,
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => -1,
    };
}
