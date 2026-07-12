namespace Emergence.Foundation.Tests;

public sealed class DomainSelfTestTests
{
    [Fact]
    public void Phase01VectorRemainsUnchanged()
    {
        FoundationSelfTestReport report = FoundationSelfTest.Run();
        Assert.True(report.Success); Assert.Equal("f4fd4d01fc3f3e82b74c69622c8fed9a8a87bc02ec6ce2f9f18127aec7544ce1", report.Sha256);
    }

    [Fact]
    public void Phase02DomainSelfTestPassesAllVectors()
    {
        FoundationDomainSelfTestReport report = FoundationDomainSelfTest.Run();
        Assert.True(report.Success); Assert.Equal(FoundationDomainSelfTest.ExpectedCanonicalEncodingHex, report.CanonicalEncodingHex); Assert.Equal(FoundationDomainSelfTest.ExpectedCanonicalDigest, report.CanonicalDigest);
        Assert.Equal(FoundationDomainSelfTest.ExpectedAlgorithmCatalogDigest, report.AlgorithmCatalogDigest); Assert.Equal(FoundationDomainSelfTest.ExpectedConfigurationDigest, report.ConfigurationDigest);
        Assert.All(report.Checks, static check => Assert.Equal(DiagnosticSeverity.Success, check.Severity));
    }

    [Fact]
    public void DomainSelfTestIsByteStable()
    {
        Assert.Equal(JsonDefaults.Serialize(FoundationDomainSelfTest.Run(), false), JsonDefaults.Serialize(FoundationDomainSelfTest.Run(), false));
    }
}
