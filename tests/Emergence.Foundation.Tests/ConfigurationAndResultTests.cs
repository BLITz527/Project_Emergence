using System.Globalization;
using System.Text.Json;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Results;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation.Tests;

public sealed class ConfigurationAndResultTests
{
    [Theory]
    [InlineData("foundation.test")]
    [InlineData("a1.b-2")]
    public void ConfigurationNamesAcceptCanonicalText(string text) { Assert.Equal(text, new ConfigurationSchemaId(text).ToString()); Assert.Equal(text, new ConfigurationKey(text).ToString()); }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1a")]
    [InlineData("a..b")]
    [InlineData("a_b")]
    public void ConfigurationNamesRejectMalformedText(string text) { Assert.False(ConfigurationSchemaId.TryParse(text, out _)); Assert.False(ConfigurationKey.TryParse(text, out _)); }

    [Fact]
    public void EveryConfigurationValueKindRoundTrips()
    {
        ConfigurationValue[] values =
        [
            ConfigurationValue.FromBoolean(true), ConfigurationValue.FromInt64(long.MinValue), ConfigurationValue.FromUInt64(ulong.MaxValue),
            ConfigurationValue.FromDecimal(12345.678900m), ConfigurationValue.FromString("exact 🧬 text"), ConfigurationValue.FromDigest(Sha256Digest.ComputeUtf8("fixture")),
        ];
        foreach (ConfigurationValue value in values)
        {
            string json = JsonDefaults.Serialize(value, false);
            Assert.Equal(value, JsonSerializer.Deserialize<ConfigurationValue>(json, JsonDefaults.Compact));
        }
    }

    [Fact]
    public void DecimalCanonicalFormIsInvariantAndNormalizesNegativeZero()
    {
        CultureInfo prior = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Assert.Equal("12345.6789", ConfigurationValue.FromDecimal(12345.678900m).CanonicalText()); Assert.Equal("0", ConfigurationValue.FromDecimal(decimal.Negate(0m)).CanonicalText()); }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void ConfigurationRejectsDuplicateKeys()
    {
        ConfigurationEntry entry = new(new ConfigurationKey("alpha.value"), ConfigurationValue.FromUInt64(1));
        Assert.Throws<ArgumentException>(() => new ImmutableConfiguration(new ConfigurationSchemaId("foundation.test"), new SemanticVersion(1, 0, 0), [entry, entry]));
    }

    [Fact]
    public void ConfigurationOrderDoesNotAffectEqualityJsonOrDigest()
    {
        ImmutableConfiguration first = FoundationDomainSelfTest.CreateFixtureConfiguration();
        ImmutableConfiguration second = new(first.SchemaId, first.SchemaVersion, first.Entries.Reverse());
        Assert.Equal(first, second); Assert.Equal(first.Digest, second.Digest); Assert.Equal(JsonDefaults.Serialize(first, false), JsonDefaults.Serialize(second, false));
        Assert.Equal(FoundationDomainSelfTest.ExpectedConfigurationDigest, first.Digest.ToString());
    }

    [Fact]
    public void ConfigurationDefensivelyCopiesInput()
    {
        List<ConfigurationEntry> source = [new(new ConfigurationKey("alpha.value"), ConfigurationValue.FromUInt64(1))];
        ImmutableConfiguration configuration = new(new ConfigurationSchemaId("foundation.test"), new SemanticVersion(1, 0, 0), source);
        source.Clear();
        Assert.Single(configuration.Entries);
        Assert.False(configuration.Entries is IList<ConfigurationEntry> list && !list.IsReadOnly);
    }

    [Fact]
    public void ConfigurationRejectsDigestMismatch()
    {
        ImmutableConfiguration value = FoundationDomainSelfTest.CreateFixtureConfiguration();
        string json = JsonDefaults.Serialize(value, false).Replace(value.Digest.ToString(), new string('f', 64), StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImmutableConfiguration>(json, JsonDefaults.Compact));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void ConfigurationRoundTripsAcrossCultures(string culture)
    {
        CultureInfo prior = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); ImmutableConfiguration value = FoundationDomainSelfTest.CreateFixtureConfiguration(); Assert.Equal(value, JsonSerializer.Deserialize<ImmutableConfiguration>(JsonDefaults.Serialize(value, false), JsonDefaults.Compact)); }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void ConfigurationPermutationsHaveOneCanonicalDigest()
    {
        ImmutableConfiguration fixture = FoundationDomainSelfTest.CreateFixtureConfiguration();
        foreach (ConfigurationEntry[] permutation in Permute(fixture.Entries.ToArray())) Assert.Equal(fixture.Digest, new ImmutableConfiguration(fixture.SchemaId, fixture.SchemaVersion, permutation).Digest);
    }

    [Fact]
    public void WarningOnlyResultSucceeds()
    {
        FoundationIssue warning = Issue(IssueSeverity.Warning);
        Assert.True(OperationResult.Succeeded(warning).Success);
    }

    [Theory]
    [InlineData(IssueSeverity.Error)]
    [InlineData(IssueSeverity.Critical)]
    public void SeriousIssueCausesFailure(IssueSeverity severity) => Assert.False(OperationResult.Failed(Issue(severity)).Success);

    [Fact] public void SuccessfulGenericResultRequiresValue() => Assert.Throws<ArgumentNullException>(() => OperationResult<string>.Succeeded(null!));
    [Fact] public void FailedGenericResultCannotExposeValue() { OperationResult<int> result = OperationResult<int>.Failed(Issue(IssueSeverity.Error)); Assert.False(result.TryGetValue(out _)); Assert.Throws<InvalidOperationException>(() => result.Value); }

    [Fact]
    public void IssueCollectionIsImmutableDefensiveCopy()
    {
        FoundationIssue[] source = [Issue(IssueSeverity.Warning)]; OperationResult result = OperationResult.FromIssues(source); source[0] = Issue(IssueSeverity.Critical);
        Assert.True(result.Success); Assert.False(result.Issues is IList<FoundationIssue> list && !list.IsReadOnly);
    }

    [Fact]
    public void StructuredJsonPropertyOrderIsStable()
    {
        string issueJson = JsonDefaults.Serialize(Issue(IssueSeverity.Information), false);
        using JsonDocument document = JsonDocument.Parse(issueJson);
        Assert.Equal(["code", "severity", "summary", "detail"], document.RootElement.EnumerateObject().Select(static property => property.Name));

        string resultJson = JsonDefaults.Serialize(OperationResult<int>.Succeeded(0, Issue(IssueSeverity.Warning)), false);
        using JsonDocument resultDocument = JsonDocument.Parse(resultJson);
        Assert.Equal(["success", "hasValue", "value", "issues"], resultDocument.RootElement.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(0, resultDocument.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public void IssueSeverityUsesExactClosedJson()
    {
        foreach (IssueSeverity severity in Enum.GetValues<IssueSeverity>())
        {
            string expected = $"\"{severity}\"";
            Assert.Equal(expected, JsonSerializer.Serialize(severity, JsonDefaults.Compact));
            Assert.Equal(severity, JsonSerializer.Deserialize<IssueSeverity>(expected, JsonDefaults.Compact));
        }

        string[] rejected = ["0", "99", "\"0\"", "\"warning\"", "\" Warning\"", "\"Warning \"", "\"Unknown\""];
        foreach (string json in rejected)
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<IssueSeverity>(json, JsonDefaults.Compact));

        Assert.Throws<ArgumentOutOfRangeException>(() => Issue((IssueSeverity)99));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((IssueSeverity)99, JsonDefaults.Compact));
    }

    private static FoundationIssue Issue(IssueSeverity severity) => new(new IssueCode("foundation.test-issue"), severity, "summary", "detail");

    private static IEnumerable<ConfigurationEntry[]> Permute(ConfigurationEntry[] values)
    {
        for (int first = 0; first < values.Length; first++) for (int second = 0; second < values.Length; second++) if (second != first) for (int third = 0; third < values.Length; third++) if (third != first && third != second) yield return [values[first], values[second], values[third]];
    }
}
