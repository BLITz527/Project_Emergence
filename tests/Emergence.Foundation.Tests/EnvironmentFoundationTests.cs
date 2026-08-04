using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Fields;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Tests;

public sealed class EnvironmentFoundationTests
{
    [Fact]
    public void Phase11AlgorithmCatalogIsExactAndContainsNoDeferredDynamics()
    {
        Assert.Equal("b6339de0044a28aa9af9d1f3dde6d29a70e53742f678e2ee08586250cf431c65", AlgorithmCatalog.Phase11.Digest.ToString());
        Assert.Equal(AlgorithmCatalog.Phase05.Entries.Count + 9, AlgorithmCatalog.Phase11.Entries.Count);
        Assert.DoesNotContain(AlgorithmCatalog.Phase11.Entries, entry =>
            entry.Id.ToString().Contains("diffusion", StringComparison.Ordinal)
            || entry.Id.ToString().Contains("flow", StringComparison.Ordinal)
            || entry.Id.ToString().Contains("reaction", StringComparison.Ordinal)
            || entry.Id.ToString().Contains("cell", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(ulong.MaxValue)]
    public void VolumeAmountUsesCanonicalExactJson(ulong quanta)
    {
        VolumeAmount value = new(quanta);
        string json = JsonDefaults.Serialize(value, false);
        Assert.Equal($"\"{quanta}\"", json);
        Assert.Equal(value, JsonSerializer.Deserialize<VolumeAmount>(json, JsonDefaults.Compact));
    }

    [Fact]
    public void VolumeAmountArithmeticIsCheckedAndCannotUnderflow()
    {
        Assert.Equal(new VolumeAmount(12), new VolumeAmount(5) + new VolumeAmount(7));
        Assert.Equal(new VolumeAmount(15), new VolumeAmount(5) * 3UL);
        Assert.Throws<OverflowException>(() => _ = new VolumeAmount(ulong.MaxValue) + new VolumeAmount(1));
        Assert.Throws<OverflowException>(() => _ = new VolumeAmount(0) - new VolumeAmount(1));
        Assert.False(new VolumeAmount(4).TrySubtract(new VolumeAmount(5), out _));
    }

    [Theory]
    [InlineData("matter.energy-substrate")]
    [InlineData("a")]
    [InlineData("a.a1234567890123456789012345678901")]
    public void FieldChannelIdAcceptsCanonicalBoundaries(string text) => Assert.Equal(text, new FieldChannelId(text).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("Matter.energy")]
    [InlineData("matter..energy")]
    [InlineData("matter.energy_substrate")]
    [InlineData("matter.1energy")]
    [InlineData("matter energy")]
    public void FieldChannelIdRejectsNoncanonicalText(string text) => Assert.Throws<ArgumentException>(() => new FieldChannelId(text));

    [Fact]
    public void DefaultFieldChannelIdCannotSerialize() =>
        Assert.Throws<JsonException>(() => JsonDefaults.Serialize(default(FieldChannelId), false));

    [Fact]
    public void FieldChannelRoleUsesOneExactString()
    {
        Assert.Equal("\"ConservedMaterial\"", JsonDefaults.Serialize(FieldChannelRole.ConservedMaterial, false));
        foreach (string malformed in new[] { "0", "\"0\"", "\"conservedmaterial\"", "\" ConservedMaterial\"", "\"Unknown\"" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FieldChannelRole>(malformed, JsonDefaults.Compact));
        Assert.Throws<JsonException>(() => JsonDefaults.Serialize((FieldChannelRole)1, false));
    }
}
