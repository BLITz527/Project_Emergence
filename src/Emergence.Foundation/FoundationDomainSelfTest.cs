using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Configuration;
using Emergence.Foundation.Hashing;
using Emergence.Foundation.Identifiers;
using Emergence.Foundation.Quantities;
using Emergence.Foundation.Results;
using Emergence.Foundation.Time;
using Emergence.Foundation.Versioning;

namespace Emergence.Foundation;

public sealed record FoundationDomainSelfTestReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] string CanonicalEncodingHex,
    [property: JsonPropertyOrder(2)] string CanonicalDigest,
    [property: JsonPropertyOrder(3)] string StableIdFixture,
    [property: JsonPropertyOrder(4)] string MaxTickText,
    [property: JsonPropertyOrder(5)] string AlgorithmCatalogDigest,
    [property: JsonPropertyOrder(6)] string ConfigurationDigest,
    [property: JsonPropertyOrder(7)] IReadOnlyList<DiagnosticCheck> Checks);

public static class FoundationDomainSelfTest
{
    public const string ExpectedCanonicalEncodingHex = "50452d43414e4f4e4943414c2f310001110000000000000050726f6a65637420456d657267656e6365032a000000000000000407000000000000000000010000000000050102030000000000000000ff10";
    public const string ExpectedCanonicalDigest = "82c8ccdd15e3c521c298553e1fc02360048f5a115ad96dc6d82802e244a7c370";
    public const string ExpectedStableIdFixture = "0123456789abcdeffedcba9876543210";
    public const string ExpectedSha256Abc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    public const string ExpectedAlgorithmCatalogDigest = "a8d497cee1881fe786f414ebd2a944c2da4ccb9433430feef675b1aeb17fd6dc";
    public const string ExpectedConfigurationDigest = "75b8257ce1bbcf5599165648ea4601e64029afb562667639e271dfde14bc2cb5";

    public static FoundationDomainSelfTestReport Run()
    {
        List<DiagnosticCheck> checks = [];
        StableId128 stable = new(0x0123456789abcdef, 0xfedcba9876543210);
        Check(checks, "domain.stable-id", stable.ToString() == ExpectedStableIdFixture, "StableId128 fixture formatting", stable.ToString());

        WorldId worldId = WorldId.FromUInt64(42);
        string worldJson = JsonDefaults.Serialize(worldId, false);
        Check(checks, "domain.typed-id-json", JsonSerializer.Deserialize<WorldId>(worldJson, JsonDefaults.Compact) == worldId, "Typed-ID JSON round-trip", worldJson);

        string maxTick = SimulationTick.MaxValue.ToString();
        Check(checks, "domain.max-tick", SimulationTick.Parse(maxTick) == SimulationTick.MaxValue, "SimulationTick maximum decimal formatting", maxTick);

        CheckedSequenceCounter counter = new(new SequenceNumber(UInt128.MaxValue - UInt128.One));
        SequenceNumber final = counter.IssueNext();
        Check(checks, "domain.counter-exhaustion", final == SequenceNumber.MaxValue && !counter.TryIssueNext(out _), "Checked counter exhaustion", final.ToString());

        MatterAmount matter = new(7); EnergyAmount energy = new(7);
        Check(checks, "domain.quantity-separation", matter.GetType() != energy.GetType() && (matter * 6).Quanta == 42 && (energy * 6).Quanta == 42, "Exact quantity type separation", "matter=42;energy=42");

        string abc = Sha256Digest.ComputeUtf8("abc").ToString();
        Check(checks, "domain.sha256-abc", abc == ExpectedSha256Abc, "SHA-256 UTF-8 abc vector", abc);

        (string encoding, string digest) = CanonicalFixture();
        Check(checks, "domain.canonical-encoding", encoding == ExpectedCanonicalEncodingHex, "Canonical encoding V1 bytes", encoding);
        Check(checks, "domain.canonical-digest", digest == ExpectedCanonicalDigest, "Canonical encoding V1 digest", digest);

        AlgorithmCatalog catalog = AlgorithmCatalog.Phase02;
        AlgorithmCatalog catalogReversed = new(catalog.Entries.Reverse());
        string catalogJson = JsonDefaults.Serialize(catalog, false);
        AlgorithmCatalog? catalogRoundTrip = JsonSerializer.Deserialize<AlgorithmCatalog>(catalogJson, JsonDefaults.Compact);
        Check(checks, "domain.algorithm-catalog", catalog.Digest.ToString() == ExpectedAlgorithmCatalogDigest && catalog.Digest == catalogReversed.Digest && catalog.Equals(catalogRoundTrip), "Algorithm catalog order, digest, and round-trip", catalog.Digest.ToString());

        ImmutableConfiguration configuration = CreateFixtureConfiguration();
        ImmutableConfiguration reordered = new(configuration.SchemaId, configuration.SchemaVersion, configuration.Entries.Reverse());
        string configurationJson = JsonDefaults.Serialize(configuration, false);
        ImmutableConfiguration? configurationRoundTrip = JsonSerializer.Deserialize<ImmutableConfiguration>(configurationJson, JsonDefaults.Compact);
        Check(checks, "domain.configuration", configuration.Digest.ToString() == ExpectedConfigurationDigest && configuration.Digest == reordered.Digest && configuration.Equals(configurationRoundTrip), "Immutable configuration order, digest, and round-trip", configuration.Digest.ToString());

        FoundationIssue warning = new(new IssueCode("foundation.fixture-warning"), IssueSeverity.Warning, "Fixture warning", "Warning-only results remain successful.");
        Check(checks, "domain.warning-result", OperationResult.Succeeded(warning).Success, "Warning-only OperationResult succeeds", "success=true");
        FoundationIssue error = new(new IssueCode("foundation.fixture-error"), IssueSeverity.Error, "Fixture error", "Failed generic results contain no value.");
        OperationResult<int> failed = OperationResult<int>.Failed(error);
        Check(checks, "domain.failed-generic-result", !failed.Success && !failed.HasValue && !failed.TryGetValue(out _), "Failed OperationResult<T> has no value", "hasValue=false");

        return new(
            checks.All(static check => check.Severity == DiagnosticSeverity.Success),
            encoding,
            digest,
            stable.ToString(),
            maxTick,
            catalog.Digest.ToString(),
            configuration.Digest.ToString(),
            checks.AsReadOnly());
    }

    public static ImmutableConfiguration CreateFixtureConfiguration() => new(
        new ConfigurationSchemaId("foundation.test"),
        new SemanticVersion(1, 0, 0),
        [
            new(new ConfigurationKey("alpha.enabled"), ConfigurationValue.FromBoolean(true)),
            new(new ConfigurationKey("alpha.limit"), ConfigurationValue.FromUInt64(42)),
            new(new ConfigurationKey("beta.label"), ConfigurationValue.FromString("stable")),
        ]);

    private static (string Encoding, string Digest) CanonicalFixture()
    {
        using CanonicalHashWriter writer = new();
        writer.WriteString("Project Emergence");
        writer.WriteUInt64(42);
        writer.WriteUInt128((UInt128.One << 80) + 7);
        writer.WriteBoolean(true);
        writer.WriteBytes([0x00, 0xff, 0x10]);
        string encoding = Convert.ToHexStringLower(writer.GetEncodedBytes());
        return (encoding, writer.FinalizeDigest().ToString());
    }

    private static void Check(List<DiagnosticCheck> checks, string id, bool success, string summary, string detail) =>
        checks.Add(new(id, success ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, summary, detail));
}
