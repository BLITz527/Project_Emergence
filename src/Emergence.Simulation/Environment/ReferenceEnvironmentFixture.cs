using Emergence.Foundation.Quantities;
using Emergence.Model.Environment;

namespace Emergence.Simulation.Fields;

public static class ReferenceEnvironmentFixture
{
    public const string ExpectedRegionStateDigest = "c22b643d840dc32d6f22e5a6281396292cabb0ebd5b370773f7309efa89da5ca";
    public const string ExpectedEnvironmentStateDigest = "cb98e417570c1b46073170128eebfc7b5b84e38bb4a1a1eac622ceb8d1578466";
    public const ulong EnergyTotal = 183_686;
    public const ulong StructuralTotal = 120_947;
    public const ulong WasteTotal = 6_310;

    public static WorldEnvironmentStore CreateStore()
    {
        EnvironmentDefinition definition = ReferenceEnvironmentDefinition.Create();
        RegionLatticeDefinition region = definition.Regions.Single();
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
        RegionFieldChannelAmounts[] channels = region.FieldChannels.Definitions
            .Select((channel, slot) => new RegionFieldChannelAmounts(channel.Id, buffers[slot])).ToArray();
        return new(definition, new RegionFieldStore(region, channels));
    }
}
