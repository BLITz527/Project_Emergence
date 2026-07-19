using System.Globalization;
using System.Text.Json;
using Emergence.Cli;
using Emergence.Foundation;
using Emergence.Foundation.Rulesets;
using Emergence.Model;
using Emergence.Persistence.WorldPackages;
using Emergence.Simulation;

namespace Emergence.Cli.IntegrationTests;

public sealed class CliIntegrationTests
{
    [Fact]
    public async Task VersionReturnsRequiredFields()
    {
        Invocation result = await Invoke("version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Product: Project Emergence", result.Output, StringComparison.Ordinal);
        Assert.Contains("Version:", result.Output, StringComparison.Ordinal);
        Assert.Contains("Version: 0.5.0-dev", result.Output, StringComparison.Ordinal);
        Assert.Contains("Target framework:", result.Output, StringComparison.Ordinal);
        Assert.Contains("Runtime:", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoctorJsonIsParseable()
    {
        string path = TemporaryJsonPath();
        try
        {
            Invocation result = await Invoke("doctor", "--json", path);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));

            Assert.Equal(0, result.ExitCode);
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("cli", document.RootElement.GetProperty("mode").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SelfTestJsonIsParseableAndSuccessful()
    {
        string path = TemporaryJsonPath();
        try
        {
            Invocation result = await Invoke("self-test", "--json", path);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));

            Assert.Equal(0, result.ExitCode);
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InvalidCommandReturnsUsefulHelp()
    {
        Invocation result = await Invoke("not-a-command");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown command", result.Error, StringComparison.Ordinal);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DomainSelfTestWritesJsonToStdout()
    {
        Invocation result = await Invoke("domain-self-test");
        using JsonDocument document = JsonDocument.Parse(result.Output);
        Assert.Equal(0, result.ExitCode);
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(FoundationDomainSelfTest.ExpectedCanonicalDigest, document.RootElement.GetProperty("canonicalDigest").GetString());
        Assert.Equal(FoundationDomainSelfTest.ExpectedStableIdFixture, document.RootElement.GetProperty("stableIdFixture").GetString());
    }

    [Fact]
    public async Task DomainSelfTestWritesJsonFile()
    {
        string path = TemporaryJsonPath();
        try
        {
            Invocation result = await Invoke("domain-self-test", "--json", path);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal(0, result.ExitCode); Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(FoundationDomainSelfTest.ExpectedConfigurationDigest, document.RootElement.GetProperty("configurationDigest").GetString());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DomainSelfTestRejectsInvalidArguments()
    {
        Invocation result = await Invoke("domain-self-test", "unexpected");
        Assert.Equal(2, result.ExitCode); Assert.Contains("accepts no arguments", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DomainSelfTestOutputIsByteForByteStable()
    {
        Invocation first = await Invoke("domain-self-test"); Invocation second = await Invoke("domain-self-test");
        Assert.Equal(first.Output, second.Output);
    }

    [Fact]
    public async Task Phase01SelfTestVectorRemainsValid()
    {
        Invocation result = await Invoke("self-test");
        using JsonDocument document = JsonDocument.Parse(result.Output);
        Assert.Equal("f4fd4d01fc3f3e82b74c69622c8fed9a8a87bc02ec6ce2f9f18127aec7544ce1", document.RootElement.GetProperty("sha256").GetString());
    }

    [Fact]
    public async Task CultureDoesNotAlterMachineReadableOutput()
    {
        CultureInfo prior = CultureInfo.CurrentCulture;
        string first = TemporaryJsonPath();
        string second = TemporaryJsonPath();
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(0, (await Invoke("self-test", "--json", first)).ExitCode);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(0, (await Invoke("self-test", "--json", second)).ExitCode);
            Assert.Equal(await File.ReadAllTextAsync(first), await File.ReadAllTextAsync(second));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
            File.Delete(first);
            File.Delete(second);
        }
    }

    [Fact]
    public async Task RngSelfTestStdoutMatchesEveryPrimaryVector()
    {
        Invocation result = await Invoke("rng-self-test"); using JsonDocument document = JsonDocument.Parse(result.Output); JsonElement root = document.RootElement;
        Assert.Equal(0, result.ExitCode); Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(FoundationRngSelfTest.ExpectedBlock, root.GetProperty("block").GetString());
        Assert.Equal(FoundationRngSelfTest.ExpectedLane0, root.GetProperty("lane0").GetUInt64());
        Assert.Equal(FoundationRngSelfTest.ExpectedBounded10, root.GetProperty("bounded10").GetUInt64());
        Assert.Equal(FoundationRngSelfTest.ExpectedDomainDigest, root.GetProperty("domainCatalogDigest").GetString());
        Assert.Equal(FoundationRngSelfTest.ExpectedAlgorithmDigest, root.GetProperty("algorithmCatalogDigest").GetString());
        Assert.Equal((await Invoke("rng-self-test")).Output, result.Output);
    }

    [Fact]
    public async Task RngSelfTestJsonFileIsDeterministic()
    {
        string path = TemporaryJsonPath(); try { Assert.Equal(0, (await Invoke("rng-self-test", "--json", path)).ExitCode); using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path)); Assert.Equal(FoundationRngSelfTest.ExpectedEncoding, document.RootElement.GetProperty("canonicalEncodingHex").GetString()); } finally { File.Delete(path); }
    }

    [Fact]
    public async Task RepositoryRulesetValidationMatchesLockedRegistry()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "rulesets"); Invocation result = await Invoke("ruleset", "validate", "--directory", directory); using JsonDocument document = JsonDocument.Parse(result.Output);
        Assert.Equal(0, result.ExitCode); Assert.True(document.RootElement.GetProperty("success").GetBoolean()); Assert.Equal(1, document.RootElement.GetProperty("loadedRulesets").GetInt32());
        Assert.Equal("0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d", document.RootElement.GetProperty("registryDigest").GetString());
    }

    [Fact]
    public async Task InvalidRulesetDirectoryAndArgumentsReturnNonzero()
    {
        Invocation missing = await Invoke("ruleset", "validate", "--directory", Path.Combine(Path.GetTempPath(), "definitely-missing-emergence-rulesets"));
        Assert.Equal(1, missing.ExitCode); using JsonDocument document = JsonDocument.Parse(missing.Output); Assert.False(document.RootElement.GetProperty("success").GetBoolean()); Assert.NotEmpty(document.RootElement.GetProperty("issues").EnumerateArray());
        Assert.Equal(2, (await Invoke("ruleset", "validate")).ExitCode);
        Assert.Equal(2, (await Invoke("rng-self-test", "unexpected")).ExitCode);
    }

    [Fact]
    public async Task SessionSelfTestMatchesEveryPhase04GoldenValue()
    {
        Invocation result = await Invoke("session-self-test");
        using JsonDocument document = JsonDocument.Parse(result.Output);
        JsonElement root = document.RootElement;
        Assert.Equal(0, result.ExitCode);
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("0.5.0-dev", root.GetProperty("version").GetString());
        Assert.Equal("bbaebfc88087fc04ab024d2505b9a50ed7e7a2f21cd34a18eb4e83d56cb1a418", root.GetProperty("algorithmCatalogDigest").GetString());
        Assert.Equal("3ddcda2140c7fed29e2af548b8c71edf988c12a7f65ecdfd73d47c1bab33067a", root.GetProperty("schedulerGraphDigest").GetString());
        Assert.Equal("fcc91152d376a93f558f44c2e76eb8493ab61fb519d598faa8782992d8cd3456", root.GetProperty("sessionDefinitionDigest").GetString());
        Assert.Equal("58f7313342790881b43875ba1bf3461e2aa8b1dd4b23d19278dd32cd973a7491", root.GetProperty("sessionTraceDigest").GetString());
        Assert.Equal("6de0d3bee6901dfdd83b080545ce58efcd86a2b52bf67f21692a947d19fb9ff0", root.GetProperty("finalStateDigest").GetString());
        Assert.Equal(10, root.GetProperty("eventIds").GetArrayLength());
        Assert.Equal(result.Output, (await Invoke("session-self-test")).Output);
    }

    [Fact]
    public async Task SessionSelfTestWritesJsonAndRejectsInvalidArguments()
    {
        string path = TemporaryJsonPath();
        try
        {
            Invocation result = await Invoke("session-self-test", "--json", path);
            Assert.Equal(0, result.ExitCode);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal("2", document.RootElement.GetProperty("finalTick").GetString());
        }
        finally { File.Delete(path); }
        Assert.Equal(2, (await Invoke("session-self-test", "unexpected")).ExitCode);
    }

    [Fact]
    public async Task PersistenceSelfTestStdoutIsDeterministicAndMatchesEveryLockedVector()
    {
        Invocation first = await Invoke("persistence-self-test");
        Invocation second = await Invoke("persistence-self-test");
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first.Output, second.Output);
        using JsonDocument document = JsonDocument.Parse(first.Output);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(PersistenceSelfTest.ExpectedAlgorithmCatalogDigest, root.GetProperty("algorithmCatalogDigest").GetString());
        Assert.Equal(PersistenceSelfTest.ExpectedCommandProcessorCatalogDigest, root.GetProperty("commandProcessorCatalogDigest").GetString());
        Assert.Equal(PersistenceSelfTest.ExpectedDefinitionDigest, root.GetProperty("definitionDigest").GetString());
        Assert.Equal(PersistenceSelfTest.ExpectedPreSaveStateDigest, root.GetProperty("preSaveStateDigest").GetString());
        Assert.Equal(PersistenceSelfTest.ExpectedSnapshotDigest, root.GetProperty("snapshotDigest").GetString());
        Assert.Equal(PersistenceSelfTest.ExpectedPackageIdentityDigest, root.GetProperty("packageIdentityDigest").GetString());
        Assert.Equal(PersistenceSelfTest.ExpectedFinalStateDigest, root.GetProperty("finalStateDigest").GetString());
        Assert.Equal(PersistenceSelfTest.ExpectedPersistenceTraceDigest, root.GetProperty("persistenceTraceDigest").GetString());
        Assert.Equal(5, root.GetProperty("recoveryChecks").GetArrayLength());
        JsonElement lockChecks = root.GetProperty("lockChecks");
        Assert.Equal(6, lockChecks.GetArrayLength());
        Assert.Contains(lockChecks.EnumerateArray(), static check => check.GetProperty("id").GetString() == "lock.stale-save");
        Assert.Contains(lockChecks.EnumerateArray(), static check => check.GetProperty("id").GetString() == "lock.stale-recover");
        Assert.Contains(lockChecks.EnumerateArray(), static check => check.GetProperty("id").GetString() == "lock.active-save-contention");
        Assert.Contains(lockChecks.EnumerateArray(), static check => check.GetProperty("id").GetString() == "lock.active-recovery-contention");
        Assert.Contains(lockChecks.EnumerateArray(), static check => check.GetProperty("id").GetString() == "lock.reacquire-after-release");
        Assert.Contains(lockChecks.EnumerateArray(), static check => check.GetProperty("id").GetString() == "lock.normal-sidecar-clean");
        Assert.DoesNotContain(Path.GetTempPath(), first.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersistenceSelfTestWritesJsonFileAndRejectsInvalidArguments()
    {
        string path = TemporaryJsonPath();
        try
        {
            Invocation result = await Invoke("persistence-self-test", "--json", path);
            Assert.Equal(0, result.ExitCode);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal("5", document.RootElement.GetProperty("nextCommandSequence").GetString());
        }
        finally { File.Delete(path); }
        Assert.Equal(2, (await Invoke("persistence-self-test", "unexpected")).ExitCode);
    }

    [Fact]
    public async Task WorldPackageVerifyAcceptsValidAndRejectsCorruptPackage()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Emergence.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string valid = Path.Combine(directory, "valid.emergence-world");
        string corrupt = Path.Combine(directory, "corrupt.emergence-world");
        try
        {
            Assert.True(new WorldPackageWriter().Save(valid, CreateSnapshot()).Success);
            Invocation verified = await Invoke("world-package", "verify", valid);
            Assert.Equal(0, verified.ExitCode);
            using JsonDocument document = JsonDocument.Parse(verified.Output);
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(PersistenceSelfTest.ExpectedPackageIdentityDigest, document.RootElement.GetProperty("packageIdentityDigest").GetString());
            File.WriteAllBytes(corrupt, [1, 2, 3]);
            Invocation rejected = await Invoke("world-package", "verify", corrupt);
            Assert.Equal(1, rejected.ExitCode);
            Assert.False(JsonDocument.Parse(rejected.Output).RootElement.GetProperty("success").GetBoolean());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task WorldPackageRecoverReportsEveryActionAndArgumentsAreStrict()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Emergence.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.emergence-world");
        string target = Path.Combine(directory, "target.emergence-world");
        try
        {
            Assert.True(new WorldPackageWriter().Save(source, CreateSnapshot()).Success);
            File.Copy(source, target + ".writing");
            Invocation recovered = await Invoke("world-package", "recover", target);
            Assert.Equal(0, recovered.ExitCode);
            using JsonDocument document = JsonDocument.Parse(recovered.Output);
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(2, document.RootElement.GetProperty("actions").GetArrayLength());
            Assert.True(File.Exists(target));
        }
        finally { Directory.Delete(directory, recursive: true); }
        Assert.Equal(2, (await Invoke("world-package", "verify")).ExitCode);
        Assert.Equal(2, (await Invoke("world-package", "recover", "x", "unexpected")).ExitCode);
    }

    private static async Task<Invocation> Invoke(params string[] arguments)
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        int exitCode = await CliApplication.RunAsync(arguments, output, error);
        return new Invocation(exitCode, output.ToString(), error.ToString());
    }

    private static string TemporaryJsonPath() => Path.Combine(Path.GetTempPath(), $"emergence-cli-test-{Guid.NewGuid():N}.json");

    private static WorldSessionSnapshot CreateSnapshot()
    {
        WorldSession session = FoundationSessionFixture.CreatePhase05PausedSession(new RulesetRegistry([FoundationReferenceRuleset.Create()]));
        foreach ((ulong tick, string value) in new[] { (0UL, "gamma"), (1UL, "alpha"), (0UL, "delta"), (1UL, "beta") })
            Assert.True(session.SubmitCommand(new(new(tick), new(FoundationSessionFixture.TraceCommandType), FoundationSessionFixture.TracePayload(value))).Success);
        session.Resume();
        Assert.True(session.StepOneTick().Success);
        Assert.True(session.StepOneTick().Success);
        session.Pause();
        return session.CaptureSnapshot().Value;
    }

    private static string FindRepositoryRoot() { DirectoryInfo? current = new(AppContext.BaseDirectory); while (current is not null && !File.Exists(Path.Combine(current.FullName, "ProjectEmergence.slnx"))) current = current.Parent; return current?.FullName ?? throw new InvalidOperationException("Repository root not found."); }

    private sealed record Invocation(int ExitCode, string Output, string Error);
}
