using System.Buffers.Binary;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Text;
using Emergence.Model.Environment;

namespace Emergence.Persistence.WorldPackages;

/// <summary>Deterministic, bounded little-endian encoding for one rectangular field chunk.</summary>
public static class FieldChunkCodec
{
    public const int HeaderByteLength = 116;
    private static ReadOnlySpan<byte> Magic => "PE-FIELD-CHUNK1\0"u8;

    public static string GetPath(RegionLatticeDefinition definition, FieldChunkCoordinate chunk)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _ = definition.GetChunkBounds(chunk);
        return $"regions/{definition.RegionId}/fields/{chunk.Y:D4}-{chunk.X:D4}.bin";
    }

    public static byte[] Encode(RegionFieldState state, FieldChunkCoordinate chunk)
    {
        ArgumentNullException.ThrowIfNull(state);
        RegionLatticeDefinition definition = state.Definition;
        (uint startX, uint startY, uint width, uint height) = definition.GetChunkBounds(chunk);
        uint localCellCount = checked(width * height);
        int idBytes = 0;
        foreach (FieldChannelDefinition channel in definition.FieldChannels.Definitions)
            idBytes = checked(idBytes + sizeof(ushort) + StrictUtf8.GetBytes(channel.Id.ToString()).Length);
        int length = checked(HeaderByteLength + idBytes
            + checked((int)localCellCount * definition.FieldChannels.Definitions.Count * sizeof(ulong)));
        if (length > WorldPackageTechnicalLimits.MaxFieldChunkBytes)
            throw new ArgumentException("Encoded field chunk exceeds its technical limit.", nameof(state));

        byte[] bytes = GC.AllocateUninitializedArray<byte>(length);
        int offset = 0;
        Magic.CopyTo(bytes); offset += Magic.Length;
        WriteStableId(bytes, ref offset, definition.RegionId.Value);
        WriteUInt32(bytes, ref offset, chunk.X);
        WriteUInt32(bytes, ref offset, chunk.Y);
        WriteUInt16(bytes, ref offset, checked((ushort)width));
        WriteUInt16(bytes, ref offset, checked((ushort)height));
        WriteUInt16(bytes, ref offset, checked((ushort)definition.FieldChannels.Definitions.Count));
        WriteUInt16(bytes, ref offset, 0);
        WriteDigest(bytes, ref offset, definition.Digest);
        WriteDigest(bytes, ref offset, definition.FieldChannels.Digest);
        WriteUInt32(bytes, ref offset, localCellCount);

        for (int slot = 0; slot < definition.FieldChannels.Definitions.Count; slot++)
        {
            byte[] channelId = StrictUtf8.GetBytes(definition.FieldChannels.Definitions[slot].Id.ToString());
            WriteUInt16(bytes, ref offset, checked((ushort)channelId.Length));
            channelId.CopyTo(bytes, offset); offset += channelId.Length;
            for (uint localY = 0; localY < height; localY++)
            for (uint localX = 0; localX < width; localX++)
            {
                int index = definition.GetLinearIndex(new(startX + localX, startY + localY));
                WriteUInt64(bytes, ref offset, state.GetAmount(slot, index).Quanta);
            }
        }
        if (offset != bytes.Length) throw new InvalidOperationException("Field chunk byte-length calculation mismatch.");
        return bytes;
    }

    public static DecodedFieldChunk Decode(
        ReadOnlySpan<byte> bytes,
        RegionLatticeDefinition definition,
        FieldChunkCoordinate expectedChunk)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (bytes.Length > WorldPackageTechnicalLimits.MaxFieldChunkBytes)
            throw new InvalidDataException("Field chunk exceeds its technical limit.");
        if (bytes.Length < HeaderByteLength) throw new InvalidDataException("Field chunk is truncated before its header is complete.");
        int offset = 0;
        Require(bytes, ref offset, Magic);
        StableId128 regionId = ReadStableId(bytes, ref offset);
        if (regionId != definition.RegionId.Value) throw new InvalidDataException("Field chunk region ID mismatch.");
        uint chunkX = ReadUInt32(bytes, ref offset);
        uint chunkY = ReadUInt32(bytes, ref offset);
        FieldChunkCoordinate chunk = new(chunkX, chunkY);
        if (chunk != expectedChunk) throw new InvalidDataException("Field chunk coordinate mismatch.");
        (uint startX, uint startY, uint expectedWidth, uint expectedHeight) = definition.GetChunkBounds(expectedChunk);
        ushort width = ReadUInt16(bytes, ref offset);
        ushort height = ReadUInt16(bytes, ref offset);
        if (width != expectedWidth || height != expectedHeight) throw new InvalidDataException("Field chunk dimensions mismatch.");
        ushort channelCount = ReadUInt16(bytes, ref offset);
        if (channelCount != definition.FieldChannels.Definitions.Count) throw new InvalidDataException("Field chunk channel count mismatch.");
        if (ReadUInt16(bytes, ref offset) != 0) throw new InvalidDataException("Field chunk reserved field must be zero.");
        if (ReadDigest(bytes, ref offset) != definition.Digest) throw new InvalidDataException("Field chunk region-definition digest mismatch.");
        if (ReadDigest(bytes, ref offset) != definition.FieldChannels.Digest) throw new InvalidDataException("Field chunk field-catalog digest mismatch.");
        uint localCellCount = ReadUInt32(bytes, ref offset);
        if (localCellCount != checked((uint)width * height)) throw new InvalidDataException("Field chunk local cell count mismatch.");

        ulong[][] amounts = new ulong[channelCount][];
        for (int slot = 0; slot < channelCount; slot++)
        {
            ushort idLength = ReadUInt16(bytes, ref offset);
            if (idLength == 0 || idLength > 256 || idLength > bytes.Length - offset)
                throw new InvalidDataException("Field chunk channel ID length is invalid.");
            string id;
            try { id = StrictUtf8.GetStringWithoutBom(bytes.Slice(offset, idLength)); }
            catch (Exception exception) when (exception is System.Text.DecoderFallbackException or ArgumentException)
            { throw new InvalidDataException("Field chunk channel ID is not strict UTF-8.", exception); }
            offset += idLength;
            FieldChannelId parsed;
            try { parsed = new(id); }
            catch (ArgumentException exception) { throw new InvalidDataException("Field chunk channel ID is invalid.", exception); }
            if (parsed != definition.FieldChannels.Definitions[slot].Id)
                throw new InvalidDataException("Field chunk channel order or identity mismatch.");

            ulong[] channel = new ulong[localCellCount];
            for (int localIndex = 0; localIndex < channel.Length; localIndex++)
            {
                ulong amount = ReadUInt64(bytes, ref offset);
                uint localX = (uint)localIndex % width;
                uint localY = (uint)localIndex / width;
                if (definition.IsSolid(new(startX + localX, startY + localY)) && amount != 0)
                    throw new InvalidDataException("Solid field cells must decode to zero matter.");
                channel[localIndex] = amount;
            }
            amounts[slot] = channel;
        }
        if (offset != bytes.Length) throw new InvalidDataException("Field chunk contains trailing bytes.");
        return new DecodedFieldChunk(definition, chunk, width, height, amounts);
    }

    private static void Require(ReadOnlySpan<byte> source, ref int offset, ReadOnlySpan<byte> expected)
    {
        if (source.Length - offset < expected.Length || !source.Slice(offset, expected.Length).SequenceEqual(expected))
            throw new InvalidDataException("Field chunk magic is invalid.");
        offset += expected.Length;
    }
    private static ushort ReadUInt16(ReadOnlySpan<byte> source, ref int offset) { Ensure(source, offset, 2); ushort value = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2)); offset += 2; return value; }
    private static uint ReadUInt32(ReadOnlySpan<byte> source, ref int offset) { Ensure(source, offset, 4); uint value = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4)); offset += 4; return value; }
    private static ulong ReadUInt64(ReadOnlySpan<byte> source, ref int offset) { Ensure(source, offset, 8); ulong value = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, 8)); offset += 8; return value; }
    private static StableId128 ReadStableId(ReadOnlySpan<byte> source, ref int offset) { Ensure(source, offset, 16); ulong high = BinaryPrimitives.ReadUInt64BigEndian(source.Slice(offset, 8)); ulong low = BinaryPrimitives.ReadUInt64BigEndian(source.Slice(offset + 8, 8)); offset += 16; return new(high, low); }
    private static Sha256Digest ReadDigest(ReadOnlySpan<byte> source, ref int offset) { Ensure(source, offset, 32); Sha256Digest value = new(source.Slice(offset, 32)); offset += 32; return value; }
    private static void Ensure(ReadOnlySpan<byte> source, int offset, int count) { if (offset < 0 || count < 0 || source.Length - offset < count) throw new InvalidDataException("Field chunk is truncated."); }
    private static void WriteUInt16(Span<byte> target, ref int offset, ushort value) { BinaryPrimitives.WriteUInt16LittleEndian(target.Slice(offset, 2), value); offset += 2; }
    private static void WriteUInt32(Span<byte> target, ref int offset, uint value) { BinaryPrimitives.WriteUInt32LittleEndian(target.Slice(offset, 4), value); offset += 4; }
    private static void WriteUInt64(Span<byte> target, ref int offset, ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(target.Slice(offset, 8), value); offset += 8; }
    private static void WriteStableId(Span<byte> target, ref int offset, StableId128 value) { BinaryPrimitives.WriteUInt64BigEndian(target.Slice(offset, 8), value.High); BinaryPrimitives.WriteUInt64BigEndian(target.Slice(offset + 8, 8), value.Low); offset += 16; }
    private static void WriteDigest(Span<byte> target, ref int offset, Sha256Digest digest) { if (!digest.TryCopyTo(target.Slice(offset, 32))) throw new InvalidOperationException(); offset += 32; }
}

public sealed class DecodedFieldChunk
{
    private readonly ulong[][] _amounts;
    internal DecodedFieldChunk(RegionLatticeDefinition definition, FieldChunkCoordinate coordinate, uint width, uint height, ulong[][] amounts)
    { Definition = definition; Coordinate = coordinate; Width = width; Height = height; _amounts = amounts; }
    public RegionLatticeDefinition Definition { get; }
    public FieldChunkCoordinate Coordinate { get; }
    public uint Width { get; }
    public uint Height { get; }
    public int CellCount => checked((int)(Width * Height));
    public MatterAmount GetAmount(int channelSlot, int localIndex)
    {
        if ((uint)channelSlot >= (uint)_amounts.Length || (uint)localIndex >= (uint)CellCount) throw new ArgumentOutOfRangeException();
        return new(_amounts[channelSlot][localIndex]);
    }
}
