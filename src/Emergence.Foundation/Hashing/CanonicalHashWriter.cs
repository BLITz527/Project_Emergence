using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Emergence.Foundation.Hashing;

public sealed class CanonicalHashWriter : IDisposable
{
    public const string AlgorithmReference = "foundation.canonical-hash@1.0.0";
    private static readonly byte[] Header = [.. Encoding.ASCII.GetBytes("PE-CANONICAL/1"), 0];
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly MemoryStream _encoded = new();
    private Sha256Digest? _digest;
    private bool _disposed;

    public CanonicalHashWriter() => Append(Header);

    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value); EnsureWritable();
        byte[] bytes = Encoding.UTF8.GetBytes(value); WriteLengthPrefixed(0x01, bytes);
    }
    public void WriteBytes(ReadOnlySpan<byte> value) { EnsureWritable(); WriteLengthPrefixed(0x02, value); }
    public void WriteUInt64(ulong value)
    {
        EnsureWritable(); Span<byte> bytes = stackalloc byte[9]; bytes[0] = 0x03; BinaryPrimitives.WriteUInt64LittleEndian(bytes[1..], value); Append(bytes);
    }
    public void WriteUInt128(UInt128 value)
    {
        EnsureWritable(); Span<byte> bytes = stackalloc byte[17]; bytes[0] = 0x04; BinaryPrimitives.WriteUInt64LittleEndian(bytes[1..9], (ulong)value); BinaryPrimitives.WriteUInt64LittleEndian(bytes[9..], (ulong)(value >> 64)); Append(bytes);
    }
    public void WriteBoolean(bool value) { EnsureWritable(); Span<byte> bytes = stackalloc byte[2]; bytes[0] = 0x05; bytes[1] = value ? (byte)1 : (byte)0; Append(bytes); }
    public void WriteDigest(Sha256Digest value) { EnsureWritable(); Span<byte> bytes = stackalloc byte[33]; bytes[0] = 0x06; value.TryCopyTo(bytes[1..]); Append(bytes); }

    public Sha256Digest FinalizeDigest()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_digest is null) _digest = new Sha256Digest(_hash.GetHashAndReset());
        return _digest.Value;
    }

    public byte[] GetEncodedBytes() { ObjectDisposedException.ThrowIf(_disposed, this); return _encoded.ToArray(); }

    private void WriteLengthPrefixed(byte tag, ReadOnlySpan<byte> value)
    {
        Span<byte> prefix = stackalloc byte[9]; prefix[0] = tag; BinaryPrimitives.WriteUInt64LittleEndian(prefix[1..], checked((ulong)value.Length)); Append(prefix); Append(value);
    }
    private void Append(ReadOnlySpan<byte> bytes) { _hash.AppendData(bytes); _encoded.Write(bytes); }
    private void EnsureWritable() { ObjectDisposedException.ThrowIf(_disposed, this); if (_digest is not null) throw new InvalidOperationException("The canonical writer has been finalized."); }
    public void Dispose() { if (_disposed) return; _hash.Dispose(); _encoded.Dispose(); _disposed = true; }
}
