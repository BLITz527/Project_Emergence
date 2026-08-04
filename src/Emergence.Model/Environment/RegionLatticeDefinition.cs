using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Versioning;

namespace Emergence.Model.Environment;

[JsonConverter(typeof(LatticeCoordinateJsonConverter))]
public readonly record struct LatticeCoordinate(uint X, uint Y) : IComparable<LatticeCoordinate>
{
    public int CompareTo(LatticeCoordinate other)
    {
        int y = Y.CompareTo(other.Y);
        return y != 0 ? y : X.CompareTo(other.X);
    }
}

[JsonConverter(typeof(FieldChunkCoordinateJsonConverter))]
public readonly record struct FieldChunkCoordinate(uint X, uint Y) : IComparable<FieldChunkCoordinate>
{
    public int CompareTo(FieldChunkCoordinate other)
    {
        int y = Y.CompareTo(other.Y);
        return y != 0 ? y : X.CompareTo(other.X);
    }
}

[JsonConverter(typeof(RegionLatticeDefinitionJsonConverter))]
public sealed class RegionLatticeDefinition : IEquatable<RegionLatticeDefinition>
{
    public const string DigestDomainMarker = "ProjectEmergence.RegionLatticeDefinition.v1";
    public static SemanticVersion SupportedFormatVersion { get; } = new(1, 0, 0);
    private readonly ReadOnlyCollection<VolumeAmount> _effectiveVolumes;

    public RegionLatticeDefinition(
        RegionId regionId,
        uint width,
        uint height,
        uint chunkEdge,
        VolumeAmount baseCellVolume,
        FieldChannelCatalog fieldChannels,
        IEnumerable<VolumeAmount> effectiveVolumes)
    {
        if (regionId.IsEmpty) throw new ArgumentException("Region ID cannot be empty.", nameof(regionId));
        if (width is 0 or > EnvironmentTechnicalLimits.MaxLatticeWidth) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is 0 or > EnvironmentTechnicalLimits.MaxLatticeHeight) throw new ArgumentOutOfRangeException(nameof(height));
        if (!EnvironmentTechnicalLimits.AllowedChunkEdges.Contains(chunkEdge)) throw new ArgumentOutOfRangeException(nameof(chunkEdge));
        if (baseCellVolume.Quanta == 0) throw new ArgumentException("Base cell volume must be nonzero.", nameof(baseCellVolume));
        FieldChannels = fieldChannels ?? throw new ArgumentNullException(nameof(fieldChannels));
        ArgumentNullException.ThrowIfNull(effectiveVolumes);

        ulong cellCount = checked((ulong)width * height);
        if (cellCount > EnvironmentTechnicalLimits.MaxCellsPerRegion) throw new ArgumentException("Region cell count exceeds its technical limit.");
        VolumeAmount[] volumes = effectiveVolumes.ToArray();
        if ((ulong)volumes.Length != cellCount) throw new ArgumentException("Effective-volume array length must equal width times height.", nameof(effectiveVolumes));
        if (volumes.Any(volume => volume.Quanta > baseCellVolume.Quanta)) throw new ArgumentException("Effective volume cannot exceed base cell volume.", nameof(effectiveVolumes));
        if (!volumes.Any(static volume => volume.Quanta != 0)) throw new ArgumentException("A region requires at least one fluid-accessible cell.", nameof(effectiveVolumes));

        uint chunkColumns = checked((width + chunkEdge - 1) / chunkEdge);
        uint chunkRows = checked((height + chunkEdge - 1) / chunkEdge);
        ulong chunkCount = checked((ulong)chunkColumns * chunkRows);
        if (chunkCount > EnvironmentTechnicalLimits.MaxChunksPerRegion) throw new ArgumentException("Region chunk count exceeds its technical limit.");
        ulong fieldSlots = checked(cellCount * (ulong)fieldChannels.Definitions.Count);
        if (fieldSlots > EnvironmentTechnicalLimits.MaxFieldSlotsPerRegion) throw new ArgumentException("Region field-slot count exceeds its technical limit.");

        FormatVersion = SupportedFormatVersion;
        RegionId = regionId;
        Width = width;
        Height = height;
        ChunkEdge = chunkEdge;
        BaseCellVolume = baseCellVolume;
        CellCount = checked((int)cellCount);
        ChunkColumns = chunkColumns;
        ChunkRows = chunkRows;
        ChunkCount = checked((int)chunkCount);
        SolidCellCount = volumes.Count(static volume => volume.Quanta == 0);
        FluidCellCount = volumes.Length - SolidCellCount;
        _effectiveVolumes = Array.AsReadOnly(volumes);
        Digest = ComputeDigest();
    }

    public SemanticVersion FormatVersion { get; }
    public RegionId RegionId { get; }
    public uint Width { get; }
    public uint Height { get; }
    public uint ChunkEdge { get; }
    public VolumeAmount BaseCellVolume { get; }
    public FieldChannelCatalog FieldChannels { get; }
    public IReadOnlyList<VolumeAmount> EffectiveVolumes => _effectiveVolumes;
    public int CellCount { get; }
    public uint ChunkColumns { get; }
    public uint ChunkRows { get; }
    public int ChunkCount { get; }
    public int SolidCellCount { get; }
    public int FluidCellCount { get; }
    public Sha256Digest Digest { get; }

    public bool Contains(LatticeCoordinate coordinate) => coordinate.X < Width && coordinate.Y < Height;
    public int GetLinearIndex(LatticeCoordinate coordinate) => Contains(coordinate)
        ? checked((int)((coordinate.Y * Width) + coordinate.X))
        : throw new ArgumentOutOfRangeException(nameof(coordinate));
    public LatticeCoordinate GetCoordinate(int linearIndex)
    {
        if ((uint)linearIndex >= (uint)CellCount) throw new ArgumentOutOfRangeException(nameof(linearIndex));
        return new((uint)linearIndex % Width, (uint)linearIndex / Width);
    }
    public VolumeAmount GetEffectiveVolume(LatticeCoordinate coordinate) => _effectiveVolumes[GetLinearIndex(coordinate)];
    public bool IsSolid(LatticeCoordinate coordinate) => GetEffectiveVolume(coordinate).Quanta == 0;
    public FieldChunkCoordinate GetChunkCoordinate(LatticeCoordinate coordinate)
    {
        _ = GetLinearIndex(coordinate);
        return new(coordinate.X / ChunkEdge, coordinate.Y / ChunkEdge);
    }
    public (uint StartX, uint StartY, uint Width, uint Height) GetChunkBounds(FieldChunkCoordinate chunk)
    {
        if (chunk.X >= ChunkColumns || chunk.Y >= ChunkRows) throw new ArgumentOutOfRangeException(nameof(chunk));
        uint startX = checked(chunk.X * ChunkEdge);
        uint startY = checked(chunk.Y * ChunkEdge);
        return (startX, startY, Math.Min(ChunkEdge, Width - startX), Math.Min(ChunkEdge, Height - startY));
    }

    public bool Equals(RegionLatticeDefinition? other) => other is not null
        && FormatVersion == other.FormatVersion && RegionId == other.RegionId && Width == other.Width && Height == other.Height
        && ChunkEdge == other.ChunkEdge && BaseCellVolume == other.BaseCellVolume && FieldChannels.Equals(other.FieldChannels)
        && _effectiveVolumes.SequenceEqual(other._effectiveVolumes) && Digest == other.Digest;
    public override bool Equals(object? obj) => obj is RegionLatticeDefinition other && Equals(other);
    public override int GetHashCode() => Digest.GetHashCode();

    private Sha256Digest ComputeDigest()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString(DigestDomainMarker);
        writer.WriteString(FormatVersion.ToString());
        writer.WriteString(RegionId.ToString());
        writer.WriteUInt64(Width);
        writer.WriteUInt64(Height);
        writer.WriteUInt64(ChunkEdge);
        writer.WriteUInt64(BaseCellVolume.Quanta);
        writer.WriteDigest(FieldChannels.Digest);
        writer.WriteUInt64(checked((ulong)CellCount));
        foreach (VolumeAmount volume in _effectiveVolumes) writer.WriteUInt64(volume.Quanta);
        return writer.FinalizeDigest();
    }

    internal static RegionLatticeDefinition CreateValidated(
        SemanticVersion formatVersion, RegionId regionId, uint width, uint height, uint chunkEdge,
        VolumeAmount baseCellVolume, FieldChannelCatalog fieldChannels, IEnumerable<VolumeAmount> volumes, Sha256Digest expected)
    {
        if (formatVersion != SupportedFormatVersion) throw new JsonException($"Unsupported region lattice definition format '{formatVersion}'.");
        RegionLatticeDefinition definition = new(regionId, width, height, chunkEdge, baseCellVolume, fieldChannels, volumes);
        return definition.Digest == expected ? definition : throw new JsonException("Region lattice definition digest mismatch.");
    }
}

internal abstract class CoordinateJsonConverter<T> : JsonConverter<T>
{
    protected abstract T Create(uint x, uint y);
    protected abstract uint X(T value);
    protected abstract uint Y(T value);
    public sealed override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        StrictModelJson.Exact(root, "x", "y");
        if (!root.GetProperty("x").TryGetUInt32(out uint x) || !root.GetProperty("y").TryGetUInt32(out uint y))
            throw new JsonException("Coordinates require UInt32 x and y values.");
        return Create(x, y);
    }
    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteNumber("x", X(value)); writer.WriteNumber("y", Y(value)); writer.WriteEndObject();
    }
}

internal sealed class LatticeCoordinateJsonConverter : CoordinateJsonConverter<LatticeCoordinate>
{
    protected override LatticeCoordinate Create(uint x, uint y) => new(x, y);
    protected override uint X(LatticeCoordinate value) => value.X;
    protected override uint Y(LatticeCoordinate value) => value.Y;
}

internal sealed class FieldChunkCoordinateJsonConverter : CoordinateJsonConverter<FieldChunkCoordinate>
{
    protected override FieldChunkCoordinate Create(uint x, uint y) => new(x, y);
    protected override uint X(FieldChunkCoordinate value) => value.X;
    protected override uint Y(FieldChunkCoordinate value) => value.Y;
}

internal sealed class RegionLatticeDefinitionJsonConverter : JsonConverter<RegionLatticeDefinition>
{
    public override RegionLatticeDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        StrictModelJson.Exact(root, "formatVersion", "regionId", "width", "height", "chunkEdge", "baseCellVolume", "fieldChannels", "effectiveVolumes", "digest");
        try
        {
            return RegionLatticeDefinition.CreateValidated(
                SemanticVersion.Parse(root.GetProperty("formatVersion").GetString()!),
                RegionId.Parse(root.GetProperty("regionId").GetString()!),
                root.GetProperty("width").GetUInt32(), root.GetProperty("height").GetUInt32(), root.GetProperty("chunkEdge").GetUInt32(),
                VolumeAmount.Parse(root.GetProperty("baseCellVolume").GetString()!),
                JsonSerializer.Deserialize<FieldChannelCatalog>(root.GetProperty("fieldChannels"), options) ?? throw new JsonException("Missing field channel catalog."),
                root.GetProperty("effectiveVolumes").EnumerateArray().Select(element => VolumeAmount.Parse(element.GetString()!)),
                Sha256Digest.Parse(root.GetProperty("digest").GetString()!));
        }
        catch (JsonException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new JsonException("Invalid region lattice definition.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, RegionLatticeDefinition value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("formatVersion", value.FormatVersion.ToString());
        writer.WriteString("regionId", value.RegionId.ToString());
        writer.WriteNumber("width", value.Width); writer.WriteNumber("height", value.Height); writer.WriteNumber("chunkEdge", value.ChunkEdge);
        writer.WriteString("baseCellVolume", value.BaseCellVolume.ToString());
        writer.WritePropertyName("fieldChannels"); JsonSerializer.Serialize(writer, value.FieldChannels, options);
        writer.WritePropertyName("effectiveVolumes"); writer.WriteStartArray();
        foreach (VolumeAmount volume in value.EffectiveVolumes) writer.WriteStringValue(volume.ToString());
        writer.WriteEndArray();
        writer.WriteString("digest", value.Digest.ToString());
        writer.WriteEndObject();
    }
}
