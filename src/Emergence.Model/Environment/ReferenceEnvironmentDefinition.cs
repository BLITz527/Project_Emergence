using Emergence.Foundation.Fields;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;

namespace Emergence.Model.Environment;

public static class ReferenceEnvironmentDefinition
{
    public const string EnergySubstrateId = "matter.energy-substrate";
    public const string StructuralPrecursorId = "matter.structural-precursor";
    public const string WasteId = "matter.waste";
    public const string ExpectedFieldChannelCatalogDigest = "c9fa1bc20193b72fcbbc7780776018a81d599716fd6673bc71d266d416393429";
    public const string ExpectedRegionDefinitionDigest = "07b963faec60e3b43b97bea182a4770ce079738a987413b1042c1ed103ebffc1";
    public const string ExpectedEnvironmentDefinitionDigest = "04fb13424920862b4be724befadccd8754ed21ff3ef0cc6c887f671ffa8c8e08";
    public static RegionId RegionId { get; } = RegionId.FromUInt64(100);

    public static FieldChannelCatalog CreateFieldChannels() => new(
    [
        new(new(EnergySubstrateId), FieldChannelRole.ConservedMaterial, "Energy-bearing substrate", "Exact conserved energy-bearing substrate amount."),
        new(new(StructuralPrecursorId), FieldChannelRole.ConservedMaterial, "Structural precursor", "Exact conserved structural precursor amount."),
        new(new(WasteId), FieldChannelRole.ConservedMaterial, "Waste material", "Exact conserved waste material amount."),
    ]);

    public static RegionLatticeDefinition CreateRegion(FieldChannelCatalog? channels = null)
    {
        channels ??= CreateFieldChannels();
        VolumeAmount[] volumes = new VolumeAmount[16 * 12];
        for (uint y = 0; y < 12; y++)
        {
            for (uint x = 0; x < 16; x++)
            {
                bool outer = x is 0 or 15 || y is 0 or 11;
                bool barrier = x == 8 && y is >= 2 and <= 9 && y != 6;
                volumes[(y * 16) + x] = new VolumeAmount(outer || barrier ? 0UL : 1024UL);
            }
        }
        return new(RegionId, 16, 12, 8, new VolumeAmount(1024), channels, volumes);
    }

    public static EnvironmentDefinition Create()
    {
        FieldChannelCatalog channels = CreateFieldChannels();
        return new(channels, [CreateRegion(channels)]);
    }
}
