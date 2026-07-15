using System.Text.Json.Serialization;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation;

public sealed record FoundationRngSelfTestReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] string Seed,
    [property: JsonPropertyOrder(2)] string Domain,
    [property: JsonPropertyOrder(3)] string Scope,
    [property: JsonPropertyOrder(4)] string SampleIndex,
    [property: JsonPropertyOrder(5)] string CanonicalEncodingHex,
    [property: JsonPropertyOrder(6)] string Block,
    [property: JsonPropertyOrder(7)] ulong Lane0,
    [property: JsonPropertyOrder(8)] ulong Bounded10,
    [property: JsonPropertyOrder(9)] string DomainCatalogDigest,
    [property: JsonPropertyOrder(10)] string AlgorithmCatalogDigest,
    [property: JsonPropertyOrder(11)] IReadOnlyList<DiagnosticCheck> Checks);

public static class FoundationRngSelfTest
{
    public const string Seed = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    public const string Domain = "foundation.self-test";
    public const string Scope = "0123456789abcdeffedcba9876543210";
    public const string SampleIndex = "42";
    public const string ExpectedEncoding = "50452d43414e4f4e4943414c2f3100011c0000000000000050726f6a656374456d657267656e63652e526e67426c6f636b2e7631022000000000000000000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f011400000000000000666f756e646174696f6e2e73656c662d7465737403efcdab8967452301031032547698badcfe042a000000000000000000000000000000030000000000000000";
    public const string ExpectedBlock = "8c39412c47d92f7367ae49de9f122d232aa011d2442e393572265bdc231a34e7";
    public const ulong ExpectedLane0 = 8300091537975490956UL;
    public const ulong ExpectedBounded10 = 6;
    public const string ExpectedDomainDigest = "03d1b76efaa64416b934e5a6e37b194ea1ca11199fe1932989bd493db7f545c7";
    public const string ExpectedAlgorithmDigest = "77ebbb568d4c72fcb1cdc7ace7dbc29b3d9e38f5e65e4a44f4a7d8eb9e050b20";

    public static FoundationRngSelfTestReport Run()
    {
        RngSampleAddress address = new(new(Domain), new(StableId128.Parse(Scope)), 42);
        DeterministicAddressedRng rng = new(RngSeed256.Parse(Seed), RngDomainCatalog.Phase03);
        string encoding = Convert.ToHexStringLower(rng.GetCanonicalEncoding(address)); string block = rng.GenerateBlock(address).ToString();
        ulong lane0 = rng.SampleUInt64(address); ulong bounded = rng.SampleUInt64Below(address, 10);
        List<DiagnosticCheck> checks =
        [
            Check("rng.encoding", encoding == ExpectedEncoding, "Canonical RNG address encoding", encoding),
            Check("rng.block", block == ExpectedBlock, "Primary RNG block", block),
            Check("rng.lane0", lane0 == ExpectedLane0, "Little-endian lane zero", lane0.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Check("rng.bounded10", bounded == ExpectedBounded10, "Unbiased bounded sample", bounded.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Check("rng.sample43", rng.GenerateBlock(new(address.Domain, address.Scope, 43)).ToString() == "794e82ab1c54eea15dd1034956eab26efcdc425bd96c52e347faf0fa6ec8c883", "Sample-index separation", "sample=43"),
            Check("rng.attempt1", rng.GenerateBlock(address, 1).ToString() == "ff9e8235274fe25df39ce5ea51fb575e3673290190f84d1147324d5e613ee0d6", "Rejection-attempt separation", "attempt=1"),
            Check("rng.domain-separation", rng.GenerateBlock(new(new("foundation.reference"), address.Scope, 42)).ToString() == "a7fe7a5be6102fa528b4075300858c4416f9e04438ca18382e9e4b5f111f4b64", "Domain separation", "foundation.reference"),
            Check("rng.domain-catalog", RngDomainCatalog.Phase03.Digest.ToString() == ExpectedDomainDigest, "RNG domain catalog", RngDomainCatalog.Phase03.Digest.ToString()),
            Check("rng.algorithm-catalog", AlgorithmCatalog.Phase03.Digest.ToString() == ExpectedAlgorithmDigest, "Phase 0.3 algorithm catalog", AlgorithmCatalog.Phase03.Digest.ToString()),
        ];
        return new(checks.All(static x => x.Severity == DiagnosticSeverity.Success), Seed, Domain, Scope, SampleIndex, encoding, block, lane0, bounded, RngDomainCatalog.Phase03.Digest.ToString(), AlgorithmCatalog.Phase03.Digest.ToString(), checks.AsReadOnly());
    }
    private static DiagnosticCheck Check(string id, bool success, string summary, string detail) => new(id, success ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, summary, detail);
}
