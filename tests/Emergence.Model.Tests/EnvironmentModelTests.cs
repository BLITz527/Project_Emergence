using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Model.Environment;

namespace Emergence.Model.Tests;

public sealed class EnvironmentModelTests
{
    private const string ExpectedRegionState = "c22b643d840dc32d6f22e5a6281396292cabb0ebd5b370773f7309efa89da5ca";
    private const string ExpectedEnvironmentState = "cb98e417570c1b46073170128eebfc7b5b84e38bb4a1a1eac622ceb8d1578466";

    [Fact]
    public void ReferenceDefinitionsMatchLockedDigestsAndGeometry()
    {
        EnvironmentDefinition environment = ReferenceEnvironmentDefinition.Create();
        RegionLatticeDefinition region = environment.Regions.Single();
        Assert.Equal(ReferenceEnvironmentDefinition.ExpectedFieldChannelCatalogDigest, environment.FieldChannels.Digest.ToString());
        Assert.Equal(ReferenceEnvironmentDefinition.ExpectedRegionDefinitionDigest, region.Digest.ToString());
        Assert.Equal(ReferenceEnvironmentDefinition.ExpectedEnvironmentDefinitionDigest, environment.Digest.ToString());
        Assert.Equal(192, region.CellCount);
        Assert.Equal(59, region.SolidCellCount);
        Assert.Equal(133, region.FluidCellCount);
        Assert.Equal(4, region.ChunkCount);
        Assert.False(region.IsSolid(new(8, 6)));
        for (uint y = 0; y < region.Height; y++)
        for (uint x = 0; x < region.Width; x++)
        {
            bool expected = x is 0 or 15 || y is 0 or 11 || (x == 8 && y is >= 2 and <= 9 && y != 6);
            Assert.Equal(expected, region.IsSolid(new(x, y)));
        }
    }

    [Fact]
    public void CatalogOrderingAndDigestIgnoreInsertionOrder()
    {
        FieldChannelCatalog canonical = ReferenceEnvironmentDefinition.CreateFieldChannels();
        FieldChannelCatalog reversed = new(canonical.Definitions.Reverse());
        Assert.Equal(canonical, reversed);
        Assert.Equal(canonical.Digest, reversed.Digest);
        Assert.Equal(JsonDefaults.Serialize(canonical, false), JsonDefaults.Serialize(reversed, false));
    }

    [Fact]
    public void CatalogDefensivelyCopiesAndRejectsDuplicateOrNull()
    {
        FieldChannelDefinition[] source = ReferenceEnvironmentDefinition.CreateFieldChannels().Definitions.ToArray();
        FieldChannelCatalog catalog = new(source);
        source[0] = source[1];
        Assert.Equal(ReferenceEnvironmentDefinition.ExpectedFieldChannelCatalogDigest, catalog.Digest.ToString());
        Assert.Throws<ArgumentException>(() => new FieldChannelCatalog([catalog.Definitions[0], catalog.Definitions[0]]));
        Assert.Throws<ArgumentException>(() => new FieldChannelCatalog(new FieldChannelDefinition?[] { null! }!));
    }

    [Fact]
    public void DefinitionJsonIsStrictRoundTripAndDigestValidated()
    {
        EnvironmentDefinition expected = ReferenceEnvironmentDefinition.Create();
        string json = JsonDefaults.Serialize(expected, false);
        EnvironmentDefinition actual = JsonSerializer.Deserialize<EnvironmentDefinition>(json, JsonDefaults.Compact)!;
        Assert.Equal(expected, actual);
        string tampered = json.Replace(expected.Digest.ToString(), Sha256Digest.ComputeUtf8("tampered").ToString(), StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EnvironmentDefinition>(tampered, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EnvironmentDefinition>(json[..^1] + ",\"unknown\":0}", JsonDefaults.Compact));
    }

    [Fact]
    public void RegionDefinitionDefensivelyCopiesVolumeTopology()
    {
        FieldChannelCatalog channels = ReferenceEnvironmentDefinition.CreateFieldChannels();
        VolumeAmount[] volumes = Enumerable.Repeat(new VolumeAmount(1024), 64).ToArray();
        RegionLatticeDefinition region = new(ReferenceEnvironmentDefinition.RegionId, 8, 8, 8, new(1024), channels, volumes);
        Sha256Digest digest = region.Digest;
        volumes[0] = default;
        Assert.Equal(digest, region.Digest);
        Assert.Equal(new VolumeAmount(1024), region.GetEffectiveVolume(new(0, 0)));
    }

    [Fact]
    public void RegionDefinitionRejectsInvalidTopology()
    {
        FieldChannelCatalog channels = ReferenceEnvironmentDefinition.CreateFieldChannels();
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegionLatticeDefinition(ReferenceEnvironmentDefinition.RegionId, 0, 8, 8, new(1), channels, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegionLatticeDefinition(ReferenceEnvironmentDefinition.RegionId, 8, 8, 7, new(1), channels, new VolumeAmount[64]));
        Assert.Throws<ArgumentException>(() => new RegionLatticeDefinition(ReferenceEnvironmentDefinition.RegionId, 8, 8, 8, new(1), channels, new VolumeAmount[63]));
        Assert.Throws<ArgumentException>(() => new RegionLatticeDefinition(ReferenceEnvironmentDefinition.RegionId, 8, 8, 8, new(1), channels, Enumerable.Repeat(new VolumeAmount(2), 64)));
        Assert.Throws<ArgumentException>(() => new RegionLatticeDefinition(ReferenceEnvironmentDefinition.RegionId, 8, 8, 8, new(1), channels, new VolumeAmount[64]));
    }

    [Fact]
    public void CoordinateMappingIsRowMajorAndChunksAreExact()
    {
        RegionLatticeDefinition region = ReferenceEnvironmentDefinition.CreateRegion();
        Assert.Equal(105, region.GetLinearIndex(new(9, 6)));
        Assert.Equal(new LatticeCoordinate(9, 6), region.GetCoordinate(105));
        Assert.Equal(new FieldChunkCoordinate(1, 0), region.GetChunkCoordinate(new(9, 6)));
        Assert.Equal((8U, 8U, 8U, 4U), region.GetChunkBounds(new(1, 1)));
        Assert.True(new LatticeCoordinate(0, 1).CompareTo(new(1, 0)) > 0);
    }

    [Fact]
    public void ReferenceStateMatchesLockedAmountsTotalsAndDigests()
    {
        WorldEnvironmentState environment = CreateReferenceState();
        RegionFieldState region = environment.Regions.Single();
        Assert.Equal(ExpectedRegionState, region.Digest.ToString());
        Assert.Equal(ExpectedEnvironmentState, environment.Digest.ToString());
        Assert.Equal((UInt128)183686, region.GetChannelTotal(new FieldChannelId(ReferenceEnvironmentDefinition.EnergySubstrateId)));
        Assert.Equal((UInt128)120947, region.GetChannelTotal(new FieldChannelId(ReferenceEnvironmentDefinition.StructuralPrecursorId)));
        Assert.Equal((UInt128)6310, region.GetChannelTotal(new FieldChannelId(ReferenceEnvironmentDefinition.WasteId)));
        FieldChannelId energy = new(ReferenceEnvironmentDefinition.EnergySubstrateId);
        Assert.Equal(new MatterAmount(1056), region.GetAmount(energy, new LatticeCoordinate(1, 1)));
        Assert.Equal(new MatterAmount(0), region.GetAmount(energy, new LatticeCoordinate(8, 5)));
        Assert.Equal(new MatterAmount(1410), region.GetAmount(energy, new LatticeCoordinate(8, 6)));
        Assert.Equal(new MatterAmount(1708), region.GetAmount(energy, new LatticeCoordinate(14, 10)));
    }

    [Fact]
    public void V3SessionDefinitionMatchesLockedDigestAndStrictJson()
    {
        WorldSessionDefinition definition = CreateV3Definition();
        Assert.Equal(WorldSessionDefinition.EnvironmentFormatVersion, definition.FormatVersion);
        Assert.Equal("3b3cc11fd0c728ee2d18f2f59406ec3b144c258423bdaae719634d735dd048ac", definition.Digest.ToString());
        Assert.Equal(ReferenceEnvironmentDefinition.ExpectedEnvironmentDefinitionDigest, definition.EnvironmentDefinitionDigest!.Value.ToString());
        string json = JsonDefaults.Serialize(definition, false);
        Assert.Equal(definition, JsonSerializer.Deserialize<WorldSessionDefinition>(json, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldSessionDefinition>(json.Replace("\"environmentDefinitionDigest\"", "\"unexpected\"", StringComparison.Ordinal), JsonDefaults.Compact));
    }

    [Fact]
    public void RegionStateDefensivelyCopiesAndRejectsMatterInSolidCells()
    {
        EnvironmentDefinition definition = ReferenceEnvironmentDefinition.Create();
        RegionLatticeDefinition region = definition.Regions.Single();
        RegionFieldChannelAmounts[] source = CreateAmounts(region);
        RegionFieldState state = new(region, source);
        MatterAmount[] mutable = (MatterAmount[])source[0].Amounts;
        mutable[region.GetLinearIndex(new(1, 1))] = new(999999);
        Assert.Equal(new MatterAmount(1056), state.GetAmount(new FieldChannelId(ReferenceEnvironmentDefinition.EnergySubstrateId), new LatticeCoordinate(1, 1)));

        RegionFieldChannelAmounts[] invalid = CreateAmounts(region);
        ((MatterAmount[])invalid[0].Amounts)[region.GetLinearIndex(new(8, 5))] = new(1);
        Assert.Throws<ArgumentException>(() => new RegionFieldState(region, invalid));
    }

    internal static WorldEnvironmentState CreateReferenceState()
    {
        EnvironmentDefinition definition = ReferenceEnvironmentDefinition.Create();
        return new(definition, [new RegionFieldState(definition.Regions.Single(), CreateAmounts(definition.Regions.Single()))]);
    }

    internal static WorldSessionDefinition CreateV3Definition()
    {
        RulesetRegistry registry = new([FoundationReferenceRuleset.Create()]);
        SchedulerGraph graph = new(
        [
            new(new("foundation.trace.command"), SimulationPhase.Commands, []),
            new(new("foundation.trace.prepare-a"), SimulationPhase.Prepare, []),
            new(new("foundation.trace.prepare-b"), SimulationPhase.Prepare, [new("foundation.trace.prepare-a")]),
            new(new("foundation.trace.evaluate"), SimulationPhase.Evaluate, []),
        ]);
        return new(
            new WorldIdentity(WorldId.FromUInt64(42)),
            new BranchIdentity(WorldId.FromUInt64(42), BranchId.FromUInt64(7)),
            new RulesetKey(RulesetId.FromUInt64(1), new(1, 0, 0)),
            registry,
            RngSeed256.Parse("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"),
            AlgorithmCatalog.Phase11,
            graph,
            new CommandProcessorCatalog([new SessionCommandTypeId("foundation.trace")]),
            ReferenceEnvironmentDefinition.Create());
    }

    private static RegionFieldChannelAmounts[] CreateAmounts(RegionLatticeDefinition region)
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
        return region.FieldChannels.Definitions.Select((definition, slot) => new RegionFieldChannelAmounts(definition.Id, buffers[slot])).ToArray();
    }
}
