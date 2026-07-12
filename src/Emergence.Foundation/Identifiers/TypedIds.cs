namespace Emergence.Foundation.Identifiers;

public interface IStableIdentifier<TSelf> where TSelf : struct, IStableIdentifier<TSelf>
{
    StableId128 Value { get; }
    static abstract TSelf FromStableId(StableId128 value);
}

public readonly record struct WorldId(StableId128 Value) : IComparable<WorldId>, IStableIdentifier<WorldId>
{
    public bool IsEmpty => Value.IsEmpty;
    public static WorldId FromUInt64(ulong value) => new(StableId128.FromUInt64(value));
    public static WorldId FromStableId(StableId128 value) => new(value);
    public static WorldId Parse(string text) => new(StableId128.Parse(text));
    public static bool TryParse(string? text, out WorldId value) => TypedId.TryParse(text, FromStableId, out value);
    public int CompareTo(WorldId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct BranchId(StableId128 Value) : IComparable<BranchId>, IStableIdentifier<BranchId>
{
    public bool IsEmpty => Value.IsEmpty;
    public static BranchId FromUInt64(ulong value) => new(StableId128.FromUInt64(value));
    public static BranchId FromStableId(StableId128 value) => new(value);
    public static BranchId Parse(string text) => new(StableId128.Parse(text));
    public static bool TryParse(string? text, out BranchId value) => TypedId.TryParse(text, FromStableId, out value);
    public int CompareTo(BranchId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct RegionId(StableId128 Value) : IComparable<RegionId>, IStableIdentifier<RegionId>
{
    public bool IsEmpty => Value.IsEmpty; public static RegionId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static RegionId FromStableId(StableId128 value) => new(value); public static RegionId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out RegionId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(RegionId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct CellId(StableId128 Value) : IComparable<CellId>, IStableIdentifier<CellId>
{
    public bool IsEmpty => Value.IsEmpty; public static CellId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static CellId FromStableId(StableId128 value) => new(value); public static CellId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out CellId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(CellId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct GenomeId(StableId128 Value) : IComparable<GenomeId>, IStableIdentifier<GenomeId>
{
    public bool IsEmpty => Value.IsEmpty; public static GenomeId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static GenomeId FromStableId(StableId128 value) => new(value); public static GenomeId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out GenomeId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(GenomeId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct LineageId(StableId128 Value) : IComparable<LineageId>, IStableIdentifier<LineageId>
{
    public bool IsEmpty => Value.IsEmpty; public static LineageId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static LineageId FromStableId(StableId128 value) => new(value); public static LineageId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out LineageId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(LineageId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct BondId(StableId128 Value) : IComparable<BondId>, IStableIdentifier<BondId>
{
    public bool IsEmpty => Value.IsEmpty; public static BondId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static BondId FromStableId(StableId128 value) => new(value); public static BondId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out BondId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(BondId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct CollectiveId(StableId128 Value) : IComparable<CollectiveId>, IStableIdentifier<CollectiveId>
{
    public bool IsEmpty => Value.IsEmpty; public static CollectiveId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static CollectiveId FromStableId(StableId128 value) => new(value); public static CollectiveId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out CollectiveId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(CollectiveId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct OrganismId(StableId128 Value) : IComparable<OrganismId>, IStableIdentifier<OrganismId>
{
    public bool IsEmpty => Value.IsEmpty; public static OrganismId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static OrganismId FromStableId(StableId128 value) => new(value); public static OrganismId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out OrganismId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(OrganismId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct EventId(StableId128 Value) : IComparable<EventId>, IStableIdentifier<EventId>
{
    public bool IsEmpty => Value.IsEmpty; public static EventId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static EventId FromStableId(StableId128 value) => new(value); public static EventId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out EventId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(EventId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct SnapshotId(StableId128 Value) : IComparable<SnapshotId>, IStableIdentifier<SnapshotId>
{
    public bool IsEmpty => Value.IsEmpty; public static SnapshotId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static SnapshotId FromStableId(StableId128 value) => new(value); public static SnapshotId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out SnapshotId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(SnapshotId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}
public readonly record struct RulesetId(StableId128 Value) : IComparable<RulesetId>, IStableIdentifier<RulesetId>
{
    public bool IsEmpty => Value.IsEmpty; public static RulesetId FromUInt64(ulong value) => new(StableId128.FromUInt64(value)); public static RulesetId FromStableId(StableId128 value) => new(value); public static RulesetId Parse(string text) => new(StableId128.Parse(text)); public static bool TryParse(string? text, out RulesetId value) => TypedId.TryParse(text, FromStableId, out value); public int CompareTo(RulesetId other) => Value.CompareTo(other.Value); public override string ToString() => Value.ToString();
}

internal static class TypedId
{
    public static bool TryParse<T>(string? text, Func<StableId128, T> factory, out T value)
    {
        if (StableId128.TryParse(text, out StableId128 stable))
        {
            value = factory(stable);
            return true;
        }
        value = default!;
        return false;
    }
}
