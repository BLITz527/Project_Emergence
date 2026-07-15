using System.Text;
using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Results;
using Emergence.Foundation.Rulesets;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Tests;

public sealed class Phase03Tests
{
    private const string SeedText = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    private static readonly RngSampleAddress Address = new(new("foundation.self-test"), new(new(0x0123456789abcdef, 0xfedcba9876543210)), 42);

    [Fact]
    public void AddressedRngMatchesLockedVectors()
    {
        DeterministicAddressedRng rng = new(RngSeed256.Parse(SeedText), RngDomainCatalog.Phase03);
        Assert.Equal("50452d43414e4f4e4943414c2f3100011c0000000000000050726f6a656374456d657267656e63652e526e67426c6f636b2e7631022000000000000000000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f011400000000000000666f756e646174696f6e2e73656c662d7465737403efcdab8967452301031032547698badcfe042a000000000000000000000000000000030000000000000000", Convert.ToHexStringLower(rng.GetCanonicalEncoding(Address)));
        Assert.Equal("8c39412c47d92f7367ae49de9f122d232aa011d2442e393572265bdc231a34e7", rng.GenerateBlock(Address).ToString());
        Assert.Equal(8300091537975490956UL, rng.SampleUInt64(Address));
        Assert.Equal(6UL, rng.SampleUInt64Below(Address, 10));
        Assert.Equal("794e82ab1c54eea15dd1034956eab26efcdc425bd96c52e347faf0fa6ec8c883", rng.GenerateBlock(new(Address.Domain, Address.Scope, 43)).ToString());
        Assert.Equal("ff9e8235274fe25df39ce5ea51fb575e3673290190f84d1147324d5e613ee0d6", rng.GenerateBlock(Address, 1).ToString());
        Assert.Equal("a7fe7a5be6102fa528b4075300858c4416f9e04438ca18382e9e4b5f111f4b64", rng.GenerateBlock(new(new("foundation.reference"), Address.Scope, 42)).ToString());
    }

    [Fact]
    public void LockedCatalogAndRulesetDigestsMatch()
    {
        Assert.Equal("a8d497cee1881fe786f414ebd2a944c2da4ccb9433430feef675b1aeb17fd6dc", AlgorithmCatalog.Phase02.Digest.ToString());
        Assert.Equal("77ebbb568d4c72fcb1cdc7ace7dbc29b3d9e38f5e65e4a44f4a7d8eb9e050b20", AlgorithmCatalog.Phase03.Digest.ToString());
        Assert.Equal("03d1b76efaa64416b934e5a6e37b194ea1ca11199fe1932989bd493db7f545c7", RngDomainCatalog.Phase03.Digest.ToString());
        RulesetDescriptor descriptor = FoundationReferenceRuleset.Create();
        Assert.Equal("d538f97c802dc0a5338bfd696ac40c115e106c21727e69e11a6fe26a9e0e58d2", descriptor.Configuration.Digest.ToString());
        Assert.Equal("365db3c8a32ee157ad94b2e3051a8ed4eda28c0863999234b3e9acc1dd846086", descriptor.Digest.ToString());
        Assert.Equal("0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d", new RulesetRegistry([descriptor]).Digest.ToString());
    }

    [Fact]
    public void SeedValuesAndAddressesRoundTrip()
    {
        byte[] source = Convert.FromHexString(SeedText); RngSeed256 seed = new(source); source[0] = 255;
        Assert.Equal(SeedText, seed.ToString()); Assert.Equal(seed, RngSeed256.Parse(SeedText.ToUpperInvariant()));
        Assert.Equal(seed, JsonSerializer.Deserialize<RngSeed256>(JsonSerializer.Serialize(seed, JsonDefaults.Compact), JsonDefaults.Compact));
        Assert.Equal(Address, JsonSerializer.Deserialize<RngSampleAddress>(JsonSerializer.Serialize(Address, JsonDefaults.Compact), JsonDefaults.Compact));
        Assert.True(RngSeed256.TryParse(new string('0', 64), out _));
        Assert.False(RngSeed256.TryParse("0", out _));
    }

    [Fact]
    public void CatalogsAreImmutableOrderedAndFailClosed()
    {
        RngDomainId[] domains = [new("foundation.self-test"), new("foundation.reference")];
        RngDomainCatalog catalog = new(domains); domains[0] = new("foundation.changed");
        Assert.Equal("foundation.reference", catalog.Entries[0].ToString());
        Assert.Throws<ArgumentException>(() => new RngDomainCatalog([default]));
        Assert.Throws<ArgumentException>(() => new RngDomainCatalog([new("foundation.reference"), new("foundation.reference")]));
        Assert.Throws<ArgumentException>(() => new AlgorithmCatalog([default]));
        Assert.Throws<ArgumentException>(() => new DeterministicAddressedRng(default, catalog).GenerateBlock(new(new("foundation.unregistered"), Address.Scope, 42)));
    }

    [Fact]
    public void RulesetSerializationIsStrictAndRoundTrips()
    {
        RulesetDescriptor descriptor = FoundationReferenceRuleset.Create();
        string json = JsonSerializer.Serialize(descriptor, JsonDefaults.Indented);
        Assert.Equal(descriptor, JsonSerializer.Deserialize<RulesetDescriptor>(json, JsonDefaults.Indented));
        string unknown = json.Replace("\"digest\":", "\"unknown\":true,\"digest\":", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RulesetDescriptor>(unknown, JsonDefaults.Indented));
        string duplicate = json.Replace("\"displayName\":", "\"displayName\":\"duplicate\",\"displayName\":", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RulesetDescriptor>(duplicate, JsonDefaults.Indented));
    }

    [Fact]
    public void DefaultDurableValuesAreRejectedAndDiagnosticFormattingIsSafe()
    {
        Assert.Equal(string.Empty, default(AlgorithmId).ToString());
        Assert.Equal(string.Empty, default(AlgorithmReference).ToString());
        Assert.Equal(string.Empty, default(ConfigurationSchemaId).ToString());
        Assert.Equal(string.Empty, default(ConfigurationKey).ToString());
        Assert.Equal(string.Empty, default(IssueCode).ToString());
        Assert.Throws<ArgumentException>(() => new AlgorithmReference(default, default));
        Assert.Throws<ArgumentException>(() => new ImmutableConfiguration(default, default, []));
        Assert.Throws<ArgumentException>(() => new ImmutableConfiguration(new("foundation.test"), default, [new(default, ConfigurationValue.FromBoolean(true))]));
        Assert.Throws<ArgumentException>(() => new FoundationIssue(default, IssueSeverity.Error, "summary", "detail"));
    }
}
