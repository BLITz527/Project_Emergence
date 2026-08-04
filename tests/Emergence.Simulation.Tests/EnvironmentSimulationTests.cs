using System.Reflection;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Model.Environment;
using Emergence.Simulation.Fields;

namespace Emergence.Simulation.Tests;

public sealed class EnvironmentSimulationTests
{
    [Fact]
    public void ReferenceStoreMatchesEveryAmountTotalAndLockedDigest()
    {
        WorldEnvironmentStore environment = ReferenceEnvironmentFixture.CreateStore();
        RegionFieldStore region = environment.Region;
        Assert.Equal(ReferenceEnvironmentFixture.ExpectedRegionStateDigest, region.Digest.ToString());
        Assert.Equal(ReferenceEnvironmentFixture.ExpectedEnvironmentStateDigest, environment.Digest.ToString());
        Assert.Equal(4608, region.AllocatedFieldBytes);
        for (int index = 0; index < region.Definition.CellCount; index++)
        {
            LatticeCoordinate coordinate = region.Definition.GetCoordinate(index);
            bool solid = region.Definition.IsSolid(coordinate);
            ulong x = coordinate.X;
            ulong y = coordinate.Y;
            Assert.Equal(solid ? 0UL : 1000 + (37 * x) + (19 * y), region.GetAmount(0, index).Quanta);
            Assert.Equal(solid ? 0UL : 700 + (11 * (15 - x)) + (23 * y), region.GetAmount(1, index).Quanta);
            Assert.Equal(solid ? 0UL : ((17 * x) + (29 * y)) % 97, region.GetAmount(2, index).Quanta);
        }
        Assert.Equal((UInt128)ReferenceEnvironmentFixture.EnergyTotal, region.GetChannelTotal(0));
        Assert.Equal((UInt128)ReferenceEnvironmentFixture.StructuralTotal, region.GetChannelTotal(1));
        Assert.Equal((UInt128)ReferenceEnvironmentFixture.WasteTotal, region.GetChannelTotal(2));
    }

    [Fact]
    public void ConstructionOrderAndSourceMutationCannotChangeState()
    {
        EnvironmentDefinition definition = ReferenceEnvironmentDefinition.Create();
        RegionLatticeDefinition region = definition.Regions.Single();
        RegionFieldChannelAmounts[] channels = CreateChannels(region);
        RegionFieldStore store = new(region, channels.Reverse());
        string digest = store.Digest.ToString();
        ((MatterAmount[])channels[0].Amounts)[region.GetLinearIndex(new(1, 1))] = new(999_999);
        Assert.Equal(ReferenceEnvironmentFixture.ExpectedRegionStateDigest, digest);
        Assert.Equal(new MatterAmount(1056), store.GetAmount(
            new FieldChannelId(ReferenceEnvironmentDefinition.EnergySubstrateId), new LatticeCoordinate(1, 1)));
    }

    [Fact]
    public void DenseStoreExposesNoPublicMutationOrWritableBuffers()
    {
        MethodInfo[] publicMethods = typeof(RegionFieldStore).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(publicMethods, method => method.Name.Contains("Set", StringComparison.Ordinal)
            || method.Name.Contains("Update", StringComparison.Ordinal)
            || method.ReturnType.IsArray
            || method.ReturnType.Name.Contains("Span", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(RegionFieldStore).GetProperties(), property => property.PropertyType.IsArray);
    }

    [Fact]
    public void RectangleCopyUsesExactRowMajorMapping()
    {
        RegionFieldStore region = ReferenceEnvironmentFixture.CreateStore().Region;
        MatterAmount[] copied = new MatterAmount[6];
        region.CopyRectangle(new(ReferenceEnvironmentDefinition.EnergySubstrateId), new(7, 5), 3, 2, copied);
        Assert.Equal(new ulong[] { 1354, 0, 1428, 1373, 1410, 1447 }, copied.Select(static amount => amount.Quanta));
    }

    [Fact]
    public void LockedProbesAreExactAndSolidConcentrationIsUnavailable()
    {
        WorldEnvironmentStore environment = ReferenceEnvironmentFixture.CreateStore();
        FieldProbeService service = new();
        FieldChannelId energy = new(ReferenceEnvironmentDefinition.EnergySubstrateId);
        FieldChannelId structural = new(ReferenceEnvironmentDefinition.StructuralPrecursorId);
        FieldChannelId waste = new(ReferenceEnvironmentDefinition.WasteId);
        AssertProbe(service.Probe(environment, ReferenceEnvironmentDefinition.RegionId, new(1, 1), energy), 1056, 1024, true);
        AssertProbe(service.Probe(environment, ReferenceEnvironmentDefinition.RegionId, new(1, 1), structural), 877, 1024, true);
        AssertProbe(service.Probe(environment, ReferenceEnvironmentDefinition.RegionId, new(1, 1), waste), 46, 1024, true);
        FieldProbeResult solid = service.Probe(environment, ReferenceEnvironmentDefinition.RegionId, new(8, 5), energy);
        Assert.True(solid.Success);
        Assert.True(solid.IsSolid);
        Assert.Equal(new MatterAmount(0), solid.Amount);
        Assert.Equal(new VolumeAmount(0), solid.EffectiveVolume);
        Assert.Null(solid.Concentration);
        AssertProbe(service.Probe(environment, ReferenceEnvironmentDefinition.RegionId, new(8, 6), energy), 1410, 1024, true);
        AssertProbe(service.Probe(environment, ReferenceEnvironmentDefinition.RegionId, new(14, 10), waste), 43, 1024, true);
    }

    [Fact]
    public void InvalidProbeIsStructuredAndDoesNotMutateState()
    {
        WorldEnvironmentStore environment = ReferenceEnvironmentFixture.CreateStore();
        string before = environment.Digest.ToString();
        FieldProbeResult result = new FieldProbeService().Probe(environment, RegionId.FromUInt64(999), new(1, 1), new(ReferenceEnvironmentDefinition.EnergySubstrateId));
        Assert.False(result.Success);
        Assert.Single(result.Issues);
        Assert.Equal(before, environment.Digest.ToString());
    }

    [Fact]
    public void ConservationAuditMatchesExactTotalsAndNeverRepairs()
    {
        WorldEnvironmentStore environment = ReferenceEnvironmentFixture.CreateStore();
        string before = environment.Digest.ToString();
        EnvironmentConservationAuditReport report = new EnvironmentConservationAudit().Run(environment);
        Assert.True(report.Success);
        Assert.Equal(new UInt128[] { 183686, 120947, 6310 }, report.Channels.Select(static channel => channel.Total));
        Assert.All(report.Channels, channel =>
        {
            Assert.Equal(UInt128.Zero, channel.SolidCellTotal);
            Assert.Equal(0, channel.SolidCellViolationCount);
            Assert.Empty(channel.Issues);
        });
        Assert.Equal(before, environment.Digest.ToString());
    }

    [Fact]
    public void RepeatedCaptureAndProbeOrderPreserveState()
    {
        WorldEnvironmentStore environment = ReferenceEnvironmentFixture.CreateStore();
        string before = environment.Digest.ToString();
        RegionFieldState first = environment.Capture().Regions.Single();
        FieldProbeService probe = new();
        foreach (FieldChannelId channel in environment.Definition.FieldChannels.Definitions.Select(static definition => definition.Id).Reverse())
            _ = probe.Probe(environment, ReferenceEnvironmentDefinition.RegionId, new(14, 10), channel);
        RegionFieldState second = environment.Capture().Regions.Single();
        Assert.Equal(first, second);
        Assert.Equal(before, environment.Digest.ToString());
    }

    private static void AssertProbe(FieldProbeResult probe, ulong amount, ulong volume, bool concentration)
    {
        Assert.True(probe.Success);
        Assert.Equal(new MatterAmount(amount), probe.Amount);
        Assert.Equal(new VolumeAmount(volume), probe.EffectiveVolume);
        Assert.Equal(concentration, probe.Concentration.HasValue);
    }

    private static RegionFieldChannelAmounts[] CreateChannels(RegionLatticeDefinition region)
    {
        MatterAmount[][] buffers = region.FieldChannels.Definitions.Select(_ => new MatterAmount[region.CellCount]).ToArray();
        for (int index = 0; index < region.CellCount; index++)
        {
            LatticeCoordinate coordinate = region.GetCoordinate(index);
            if (region.IsSolid(coordinate)) continue;
            ulong x = coordinate.X;
            ulong y = coordinate.Y;
            buffers[0][index] = new(1000 + (37 * x) + (19 * y));
            buffers[1][index] = new(700 + (11 * (15 - x)) + (23 * y));
            buffers[2][index] = new(((17 * x) + (29 * y)) % 97);
        }
        return region.FieldChannels.Definitions.Select((channel, slot) => new RegionFieldChannelAmounts(channel.Id, buffers[slot])).ToArray();
    }
}
