using System.Text.Json;
using Emergence.Foundation.Identifiers;

namespace Emergence.Foundation.Tests;

public sealed class IdentifierTests
{
    [Fact] public void StableFixtureFormatsExactly() => Assert.Equal("0123456789abcdeffedcba9876543210", new StableId128(0x0123456789abcdef, 0xfedcba9876543210).ToString());
    [Fact] public void SmallFixtureFormatsExactly() => Assert.Equal("0000000000000000000000000000002a", StableId128.FromUInt64(42).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("000000000000000000000000000000000")]
    [InlineData("0000000000000000000000000000000g")]
    [InlineData(" 00000000000000000000000000000000")]
    public void StableIdRejectsMalformedText(string text) => Assert.False(StableId128.TryParse(text, out _));

    [Fact]
    public void UppercaseParsesAndNormalizes()
    {
        StableId128 value = StableId128.Parse("0123456789ABCDEFFEDCBA9876543210");
        Assert.Equal("0123456789abcdeffedcba9876543210", value.ToString());
    }

    [Fact]
    public void StableIdJsonRoundTrips()
    {
        StableId128 value = new(1, 2);
        string json = JsonDefaults.Serialize(value, false);
        Assert.Equal("\"00000000000000010000000000000002\"", json);
        Assert.Equal(value, JsonSerializer.Deserialize<StableId128>(json, JsonDefaults.Compact));
    }

    [Fact]
    public void EveryTypedIdRoundTrips()
    {
        Type[] types = [typeof(WorldId), typeof(BranchId), typeof(RegionId), typeof(CellId), typeof(GenomeId), typeof(LineageId), typeof(BondId), typeof(CollectiveId), typeof(OrganismId), typeof(EventId), typeof(SnapshotId), typeof(RulesetId)];
        foreach (Type type in types)
        {
            object value = Activator.CreateInstance(type, StableId128.FromUInt64(42))!;
            string json = JsonSerializer.Serialize(value, type, JsonDefaults.Compact);
            Assert.Equal("\"0000000000000000000000000000002a\"", json);
            Assert.Equal(value, JsonSerializer.Deserialize(json, type, JsonDefaults.Compact));
        }
    }

    [Fact] public void EmptyIdsAreDetectable() { Assert.True(default(StableId128).IsEmpty); Assert.True(default(WorldId).IsEmpty); Assert.False(WorldId.FromUInt64(1).IsEmpty); }
    [Fact] public void WorldIdentityRejectsEmpty() => Assert.Throws<ArgumentException>(() => new WorldIdentity(default));
    [Fact] public void BranchIdentityRejectsEmptyWorld() => Assert.Throws<ArgumentException>(() => new BranchIdentity(default, BranchId.FromUInt64(1)));
    [Fact] public void BranchIdentityRejectsEmptyBranch() => Assert.Throws<ArgumentException>(() => new BranchIdentity(WorldId.FromUInt64(1), default));

    [Fact]
    public void IdentityRecordsRoundTrip()
    {
        BranchIdentity identity = new(WorldId.FromUInt64(1), BranchId.FromUInt64(2));
        string json = JsonDefaults.Serialize(identity, false);
        Assert.Equal(identity, JsonSerializer.Deserialize<BranchIdentity>(json, JsonDefaults.Compact));
    }

    [Fact]
    public void TypedIdsHaveNoImplicitConversions()
    {
        Type[] types = [typeof(WorldId), typeof(BranchId), typeof(RegionId), typeof(CellId), typeof(GenomeId), typeof(LineageId), typeof(BondId), typeof(CollectiveId), typeof(OrganismId), typeof(EventId), typeof(SnapshotId), typeof(RulesetId)];
        Assert.All(types, type => Assert.DoesNotContain(type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), method => method.Name == "op_Implicit"));
    }

    [Fact]
    public void NumericOrderingUsesHighThenLow()
    {
        Assert.True(new StableId128(0, ulong.MaxValue) < new StableId128(1, 0));
        Assert.True(WorldId.FromUInt64(1).CompareTo(WorldId.FromUInt64(2)) < 0);
    }
}
