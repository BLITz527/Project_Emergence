using System.Globalization;
using System.Text.Json;
using Emergence.Foundation;

namespace Emergence.Foundation.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void BuildInformationFieldsArePresentAndStable()
    {
        BuildDetails build = BuildInfo.Current;

        Assert.Equal("Project Emergence", build.ProductName);
        Assert.Equal("0.1.0-dev", build.SemanticVersion);
        Assert.All(
            new[]
            {
                build.AssemblyVersion,
                build.InformationalVersion,
                build.GitCommit,
                build.BuildConfiguration,
                build.TargetFramework,
                build.RuntimeVersion,
                build.OperatingSystem,
                build.Architecture,
            },
            value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void FoundationSelfTestUsesInvariantFormatting(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            FoundationSelfTestReport report = FoundationSelfTest.Run();
            Assert.Equal(FoundationSelfTest.ExpectedInvariantNumber, report.InvariantNumber);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ConfiguredJsonHasStablePropertyOrder()
    {
        FoundationSelfTestReport report = FoundationSelfTest.Run();
        string json = JsonDefaults.Serialize(report, false);
        using JsonDocument document = JsonDocument.Parse(json);
        string[] names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(new[] { "success", "vector", "utf8Hex", "sha256", "invariantNumber", "checks" }, names);
    }

    [Fact]
    public void FoundationSelfTestVectorPasses()
    {
        FoundationSelfTestReport report = FoundationSelfTest.Run();

        Assert.True(report.Success);
        Assert.Equal(FoundationSelfTest.ExpectedSha256, report.Sha256);
        Assert.All(report.Checks, check => Assert.Equal(DiagnosticSeverity.Success, check.Severity));
    }

    [Fact]
    public void DiagnosticSeverityIsStructuredInJson()
    {
        DiagnosticCheck check = new("sample", DiagnosticSeverity.Warning, "warning", "structured");
        string json = JsonDefaults.Serialize(check, false);

        Assert.Contains("\"severity\":\"Warning\"", json, StringComparison.Ordinal);
        Assert.Equal(DiagnosticSeverity.Warning, check.Severity);
    }
}
