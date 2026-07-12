using System.Text.Json;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Tests;

public sealed class HashingAndVersionTests
{
    [Fact] public void Sha256AbcVectorPasses() => Assert.Equal(FoundationDomainSelfTest.ExpectedSha256Abc, Sha256Digest.ComputeUtf8("abc").ToString());

    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void DigestRejectsMalformedText(string text) => Assert.False(Sha256Digest.TryParse(text, out _));

    [Fact]
    public void DigestUppercaseRoundTripsAndCopies()
    {
        Sha256Digest digest = Sha256Digest.Parse(FoundationDomainSelfTest.ExpectedSha256Abc.ToUpperInvariant());
        Span<byte> bytes = stackalloc byte[32];
        Assert.True(digest.TryCopyTo(bytes));
        Assert.Equal(FoundationDomainSelfTest.ExpectedSha256Abc, Convert.ToHexStringLower(bytes));
        Assert.Equal(digest, JsonSerializer.Deserialize<Sha256Digest>(JsonDefaults.Serialize(digest, false), JsonDefaults.Compact));
    }

    [Fact]
    public void CanonicalWriterMatchesGoldenBytesAndDigest()
    {
        using CanonicalHashWriter writer = FixtureWriter();
        Assert.Equal(FoundationDomainSelfTest.ExpectedCanonicalEncodingHex, Convert.ToHexStringLower(writer.GetEncodedBytes()));
        Assert.Equal(FoundationDomainSelfTest.ExpectedCanonicalDigest, writer.FinalizeDigest().ToString());
    }

    [Fact]
    public void CanonicalWriterSealsAtFinalization()
    {
        using CanonicalHashWriter writer = new(); writer.FinalizeDigest();
        Assert.Throws<InvalidOperationException>(() => writer.WriteUInt64(1));
        Assert.Equal(writer.FinalizeDigest(), writer.FinalizeDigest());
    }

    [Fact]
    public void CanonicalTagsDifferentiateSimilarValues()
    {
        using CanonicalHashWriter text = new(); text.WriteString("1");
        using CanonicalHashWriter bytes = new(); bytes.WriteBytes("1"u8);
        Assert.NotEqual(text.FinalizeDigest(), bytes.FinalizeDigest());
    }

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.2.3")]
    [InlineData("4294967295.4294967295.4294967295")]
    public void SemanticVersionRoundTrips(string text)
    {
        SemanticVersion version = SemanticVersion.Parse(text);
        Assert.Equal(text, version.ToString());
        Assert.Equal(version, JsonSerializer.Deserialize<SemanticVersion>(JsonDefaults.Serialize(version, false), JsonDefaults.Compact));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("+1.0.0")]
    [InlineData("1.-1.0")]
    [InlineData("4294967296.0.0")]
    public void SemanticVersionRejectsMalformedText(string text) => Assert.False(SemanticVersion.TryParse(text, out _));

    [Fact] public void SemanticVersionComparesNumerically() => Assert.True(new SemanticVersion(2, 0, 0).CompareTo(new SemanticVersion(1, 99, 99)) > 0);

    [Theory]
    [InlineData("a")]
    [InlineData("foundation.canonical-hash")]
    [InlineData("a1.b-2")]
    public void AlgorithmIdAcceptsCanonicalBoundaries(string text) => Assert.Equal(text, new AlgorithmId(text).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1a")]
    [InlineData("a..b")]
    [InlineData("a_b")]
    [InlineData("a.")]
    public void AlgorithmIdRejectsMalformedText(string text) => Assert.False(AlgorithmId.TryParse(text, out _));

    [Fact]
    public void AlgorithmReferenceRoundTrips()
    {
        const string text = "foundation.canonical-hash@1.0.0";
        AlgorithmReference reference = AlgorithmReference.Parse(text);
        Assert.Equal(text, reference.ToString());
        Assert.Equal(reference, JsonSerializer.Deserialize<AlgorithmReference>(JsonDefaults.Serialize(reference, false), JsonDefaults.Compact));
    }

    [Fact]
    public void CatalogOrderDoesNotAffectJsonOrDigest()
    {
        AlgorithmCatalog canonical = AlgorithmCatalog.Phase02;
        AlgorithmCatalog reversed = new(canonical.Entries.Reverse());
        Assert.Equal(FoundationDomainSelfTest.ExpectedAlgorithmCatalogDigest, canonical.Digest.ToString());
        Assert.Equal(JsonDefaults.Serialize(canonical, false), JsonDefaults.Serialize(reversed, false));
    }

    [Fact]
    public void CatalogRejectsDuplicateIds()
    {
        AlgorithmReference first = AlgorithmReference.Parse("foundation.test@1.0.0");
        AlgorithmReference second = AlgorithmReference.Parse("foundation.test@2.0.0");
        Assert.Throws<ArgumentException>(() => new AlgorithmCatalog([first, second]));
    }

    [Fact]
    public void CatalogRejectsDigestMismatch()
    {
        string json = JsonDefaults.Serialize(AlgorithmCatalog.Phase02, false).Replace(FoundationDomainSelfTest.ExpectedAlgorithmCatalogDigest, new string('0', 64), StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AlgorithmCatalog>(json, JsonDefaults.Compact));
    }

    private static CanonicalHashWriter FixtureWriter()
    {
        CanonicalHashWriter writer = new(); writer.WriteString("Project Emergence"); writer.WriteUInt64(42); writer.WriteUInt128((UInt128.One << 80) + 7); writer.WriteBoolean(true); writer.WriteBytes([0, 255, 16]); return writer;
    }
}
