using System.Text;
using Emergence.ReviewPack;

namespace Emergence.ReviewPack.Tests;

public sealed class Phase03EvidenceTests
{
    [Fact] public void WrongRngBlockIsRejected() => AssertRngMutation(Phase03EvidenceValidator.Block, new string('0', 64));
    [Fact] public void WrongRngEncodedBytesAreRejected() => AssertRngMutation(Phase03EvidenceValidator.Encoded, "00" + Phase03EvidenceValidator.Encoded[2..]);
    [Fact] public void WrongLaneZeroIsRejected() => AssertRngMutation(Phase03EvidenceValidator.Lane0.ToString(), "1");
    [Fact] public void WrongBoundedResultIsRejected() => AssertRngMutation("\"bounded10\":6", "\"bounded10\":7");
    [Fact] public void WrongRngDomainDigestIsRejected() => AssertRngMutation(Phase03EvidenceValidator.DomainDigest, new string('1', 64));
    [Fact] public void WrongAlgorithmDigestIsRejected() => AssertRngMutation(Phase03EvidenceValidator.AlgorithmDigest, new string('2', 64));

    [Fact]
    public void RulesetConfigurationMismatchIsRejected()
    {
        using Fixture fixture = new(); fixture.ReplaceBothRulesets("addressed", "changed");
        Assert.Equal(EvidenceStatus.Failed, Phase03EvidenceValidator.Evaluate(fixture.Root).Rulesets.Status);
    }

    [Fact]
    public void RulesetDescriptorMismatchIsRejected()
    {
        using Fixture fixture = new(); fixture.ReplaceBothRulesets("Project Emergence Foundation Reference", "Project Emergence Foundation Changed");
        Assert.Equal(EvidenceStatus.Failed, Phase03EvidenceValidator.Evaluate(fixture.Root).Rulesets.Status);
    }

    [Fact]
    public void RulesetRegistryMismatchIsRejected()
    {
        using Fixture fixture = new(); fixture.Replace("cli/ruleset-validation.json", Phase03EvidenceValidator.RegistryDigest, new string('3', 64));
        Assert.Equal(EvidenceStatus.Failed, Phase03EvidenceValidator.Evaluate(fixture.Root).Rulesets.Status);
    }

    [Fact]
    public void SourcePackageRulesetMismatchIsRejected()
    {
        using Fixture fixture = new(); fixture.Replace("package/windows-x86_64/rulesets/foundation-reference.ruleset.json", "addressed", "changed");
        Assert.Equal(EvidenceStatus.Failed, Phase03EvidenceValidator.Evaluate(fixture.Root).Rulesets.Status);
    }

    [Fact]
    public void AppPackageRegistryReportMismatchIsRejected()
    {
        using Fixture fixture = new(); fixture.Replace("package/packaged-doctor.json", Phase03EvidenceValidator.RegistryDigest, new string('4', 64));
        Assert.Equal(EvidenceStatus.Failed, Phase03EvidenceValidator.Evaluate(fixture.Root).Rulesets.Status);
    }

    [Fact]
    public void ValidPhase03EvidencePasses()
    {
        using Fixture fixture = new(); (RngEvidence rng, RulesetEvidence rulesets) = Phase03EvidenceValidator.Evaluate(fixture.Root);
        Assert.Equal(EvidenceStatus.Passed, rng.Status); Assert.Equal(EvidenceStatus.Passed, rulesets.Status);
    }

    private static void AssertRngMutation(string oldValue, string newValue)
    {
        using Fixture fixture = new(); fixture.Replace("cli/rng-self-test.json", oldValue, newValue);
        Assert.Equal(EvidenceStatus.Failed, Phase03EvidenceValidator.Evaluate(fixture.Root).Rng.Status);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "emergence-phase03-evidence", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root);
            Write("cli/rng-self-test.log", "passed"); Write("cli/ruleset-validation.log", "passed");
            Write("cli/rng-self-test.json", $$"""
                {"success":true,"seed":"{{Phase03EvidenceValidator.Seed}}","domain":"{{Phase03EvidenceValidator.Domain}}","scope":"{{Phase03EvidenceValidator.Scope}}","sampleIndex":"{{Phase03EvidenceValidator.SampleIndex}}","canonicalEncodingHex":"{{Phase03EvidenceValidator.Encoded}}","block":"{{Phase03EvidenceValidator.Block}}","lane0":{{Phase03EvidenceValidator.Lane0}},"bounded10":6,"domainCatalogDigest":"{{Phase03EvidenceValidator.DomainDigest}}","algorithmCatalogDigest":"{{Phase03EvidenceValidator.AlgorithmDigest}}","checks":[]}
                """);
            Write("cli/ruleset-validation.json", $$"""
                {"success":true,"directory":"fixture","discoveredFiles":["foundation-reference.ruleset.json"],"loadedRulesets":1,"rulesetKeys":["{{Phase03EvidenceValidator.RulesetKey}}"],"algorithmCatalogDigest":"{{Phase03EvidenceValidator.AlgorithmDigest}}","domainCatalogDigest":"{{Phase03EvidenceValidator.DomainDigest}}","configurationDigest":"{{Phase03EvidenceValidator.ConfigurationDigest}}","descriptorDigest":"{{Phase03EvidenceValidator.DescriptorDigest}}","registryDigest":"{{Phase03EvidenceValidator.RegistryDigest}}","issues":[],"checks":[]}
                """);
            string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "rulesets", "foundation-reference.ruleset.json"), new UTF8Encoding(false, true));
            Write("source/rulesets/foundation-reference.ruleset.json", source); Write("package/windows-x86_64/rulesets/foundation-reference.ruleset.json", source);
            string doctor = $$"""{"checks":[{"id":"ruleset.registry","severity":"Success","detail":"count=1;digest={{Phase03EvidenceValidator.RegistryDigest}}"}]}""";
            Write("app/doctor.json", doctor); Write("package/packaged-doctor.json", doctor);
        }
        public string Root { get; }
        public void Replace(string relative, string oldValue, string newValue) { string path = PathOf(relative); File.WriteAllText(path, File.ReadAllText(path).Replace(oldValue, newValue, StringComparison.Ordinal), new UTF8Encoding(false)); }
        public void ReplaceBothRulesets(string oldValue, string newValue) { Replace("source/rulesets/foundation-reference.ruleset.json", oldValue, newValue); Replace("package/windows-x86_64/rulesets/foundation-reference.ruleset.json", oldValue, newValue); }
        private string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        private void Write(string relative, string text) { string path = PathOf(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text, new UTF8Encoding(false)); }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
        private static string FindRepositoryRoot() { DirectoryInfo? current = new(AppContext.BaseDirectory); while (current is not null && !File.Exists(Path.Combine(current.FullName, "ProjectEmergence.slnx"))) current = current.Parent; return current?.FullName ?? throw new InvalidOperationException("Repository root not found."); }
    }
}
