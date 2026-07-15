using System.Text;
using Emergence.Foundation;
using Emergence.Foundation.Rulesets;
using Emergence.Persistence.Rulesets;

namespace Emergence.Persistence.Tests;

public sealed class RulesetDirectoryLoaderTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string CanonicalPath = Path.Combine(RepositoryRoot, "rulesets", FoundationReferenceRuleset.FileName);
    private readonly RulesetDirectoryLoader _loader = new();

    [Fact]
    public void CanonicalFoundationRulesetLoadsWithLockedRegistry()
    {
        RulesetDirectoryLoadResult result = _loader.Load(Path.GetDirectoryName(CanonicalPath)!);
        Assert.True(result.Success, string.Join("; ", result.Issues.Select(x => x.Reason)));
        Assert.Single(result.DiscoveredFiles); Assert.Single(result.Registry!.Entries);
        Assert.Equal("0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d", result.Registry.Digest.ToString());
    }

    [Fact]
    public void CanonicalSerializerReproducesTrackedFile()
    {
        string expected = File.ReadAllText(CanonicalPath, new UTF8Encoding(false, true)).Replace("\r\n", "\n", StringComparison.Ordinal);
        string actual = JsonDefaults.Serialize(FoundationReferenceRuleset.Create()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DiscoveryIsOrdinalTopLevelAndIgnoresOtherFiles()
    {
        using TempDirectory temp = new(); WriteCanonical(temp.Path, "b.ruleset.json"); WriteCanonical(temp.Path, "A.ruleset.json");
        File.WriteAllText(Path.Combine(temp.Path, "ignore.json"), "not json"); Directory.CreateDirectory(Path.Combine(temp.Path, "nested")); WriteCanonical(Path.Combine(temp.Path, "nested"), "nested.ruleset.json");
        RulesetDirectoryLoadResult result = _loader.Load(temp.Path);
        Assert.False(result.Success); Assert.Equal(["A.ruleset.json", "b.ruleset.json"], result.DiscoveredFiles); Assert.Contains("Duplicate", result.Issues[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingAndEmptyDirectoriesFailWithoutPartialRegistry()
    {
        using TempDirectory temp = new();
        RulesetDirectoryLoadResult empty = _loader.Load(temp.Path); RulesetDirectoryLoadResult missing = _loader.Load(Path.Combine(temp.Path, "missing"));
        Assert.False(empty.Success); Assert.Null(empty.Registry); Assert.Equal("ruleset.directory.empty", empty.Issues[0].Code);
        Assert.False(missing.Success); Assert.Null(missing.Registry); Assert.Equal("ruleset.directory.missing", missing.Issues[0].Code);
    }

    [Fact]
    public void MoreThanMaximumFilesFailsBeforeParsing()
    {
        using TempDirectory temp = new(); for (int i = 0; i <= RulesetDirectoryLoader.MaximumFileCount; i++) File.WriteAllText(Path.Combine(temp.Path, $"{i:D3}.ruleset.json"), "{}");
        RulesetDirectoryLoadResult result = _loader.Load(temp.Path); Assert.False(result.Success); Assert.Null(result.Registry); Assert.Equal("ruleset.limit.file-count", result.Issues[0].Code);
    }

    [Fact]
    public void OversizeIndividualFileFails()
    {
        using TempDirectory temp = new(); File.WriteAllBytes(Path.Combine(temp.Path, "large.ruleset.json"), new byte[RulesetDirectoryLoader.MaximumFileBytes + 1]);
        RulesetDirectoryLoadResult result = _loader.Load(temp.Path); Assert.False(result.Success); Assert.Equal("large.ruleset.json", result.Issues[0].FileName); Assert.Equal("ruleset.limit.file-size", result.Issues[0].Code);
    }

    [Fact]
    public void OversizeTotalFailsBeforeParsing()
    {
        using TempDirectory temp = new(); byte[] padding = Enumerable.Repeat((byte)' ', (int)RulesetDirectoryLoader.MaximumFileBytes).ToArray();
        for (int i = 0; i < 9; i++) File.WriteAllBytes(Path.Combine(temp.Path, $"{i}.ruleset.json"), padding);
        RulesetDirectoryLoadResult result = _loader.Load(temp.Path); Assert.False(result.Success); Assert.Equal("ruleset.limit.total-size", result.Issues[0].Code);
    }

    [Fact]
    public void ExcessiveJsonDepthFails()
    {
        using TempDirectory temp = new(); string json = new string('[', RulesetDirectoryLoader.MaximumJsonDepth + 1) + new string(']', RulesetDirectoryLoader.MaximumJsonDepth + 1);
        File.WriteAllText(Path.Combine(temp.Path, "deep.ruleset.json"), json);
        RulesetDirectoryLoadResult result = _loader.Load(temp.Path); Assert.False(result.Success); Assert.Equal("ruleset.json.invalid", result.Issues[0].Code);
    }

    [Fact]
    public void InvalidUtf8AndBomFailClosed()
    {
        using TempDirectory invalid = new(); File.WriteAllBytes(Path.Combine(invalid.Path, "invalid.ruleset.json"), [0xff, 0xfe]); Assert.Equal("ruleset.utf8.invalid", _loader.Load(invalid.Path).Issues[0].Code);
        using TempDirectory bom = new(); File.WriteAllBytes(Path.Combine(bom.Path, "bom.ruleset.json"), [0xef, 0xbb, 0xbf, (byte)'{', (byte)'}']); Assert.Equal("ruleset.utf8.bom", _loader.Load(bom.Path).Issues[0].Code);
    }

    [Theory]
    [InlineData("/*comment*/")]
    [InlineData(",\n}")]
    [InlineData("\"unknown\": true,")]
    [InlineData("\"displayName\": \"duplicate\",")]
    public void MalformedOrNonStrictJsonFails(string mutation)
    {
        using TempDirectory temp = new(); string json = File.ReadAllText(CanonicalPath);
        if (mutation.StartsWith("/*", StringComparison.Ordinal)) json = mutation + json;
        else if (mutation.StartsWith(",", StringComparison.Ordinal)) json = json.Replace("\n}", mutation, StringComparison.Ordinal);
        else json = json.Replace("  \"displayName\":", "  " + mutation + "\n  \"displayName\":", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(temp.Path, "bad.ruleset.json"), json, new UTF8Encoding(false));
        RulesetDirectoryLoadResult result = _loader.Load(temp.Path); Assert.False(result.Success); Assert.Null(result.Registry); Assert.Equal("bad.ruleset.json", result.Issues[0].FileName);
    }

    [Fact]
    public void MissingPropertyAndDigestMismatchFail()
    {
        using TempDirectory missing = new(); string json = File.ReadAllText(CanonicalPath).Replace("  \"displayName\": \"Project Emergence Foundation Reference\",\n", string.Empty, StringComparison.Ordinal); File.WriteAllText(Path.Combine(missing.Path, "missing.ruleset.json"), json); Assert.False(_loader.Load(missing.Path).Success);
        using TempDirectory mismatch = new(); json = File.ReadAllText(CanonicalPath).Replace("365db3c8a32ee157ad94b2e3051a8ed4eda28c0863999234b3e9acc1dd846086", new string('0', 64), StringComparison.Ordinal); File.WriteAllText(Path.Combine(mismatch.Path, "digest.ruleset.json"), json); Assert.False(_loader.Load(mismatch.Path).Success);
    }

    [Fact]
    public void DuplicateRulesetKeysReturnStructuredIssueAndNoPartialRegistry()
    {
        using TempDirectory temp = new(); WriteCanonical(temp.Path, "one.ruleset.json"); WriteCanonical(temp.Path, "two.ruleset.json");
        RulesetDirectoryLoadResult result = _loader.Load(temp.Path); Assert.False(result.Success); Assert.Null(result.Registry); Assert.Equal("ruleset.registry.invalid", result.Issues[0].Code); Assert.DoesNotContain("Exception", result.Issues[0].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectoryReparsePointIsRejectedWhenCreated()
    {
        using TempDirectory target = new(); WriteCanonical(target.Path, FoundationReferenceRuleset.FileName);
        using TempDirectory parent = new(); string link = Path.Combine(parent.Path, "link");
        try { Directory.CreateSymbolicLink(link, target.Path); }
        catch (IOException) when (OperatingSystem.IsWindows()) { Assert.Contains("ReparsePoint", File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Emergence.Persistence", "Rulesets", "RulesetDirectoryLoader.cs")), StringComparison.Ordinal); return; }
        RulesetDirectoryLoadResult result = _loader.Load(link); Assert.False(result.Success); Assert.Equal("ruleset.directory.reparse", result.Issues[0].Code);
    }

    private static void WriteCanonical(string directory, string name) => File.Copy(CanonicalPath, Path.Combine(directory, name));
    private static string FindRepositoryRoot() { DirectoryInfo? current = new(AppContext.BaseDirectory); while (current is not null && !File.Exists(Path.Combine(current.FullName, "ProjectEmergence.slnx"))) current = current.Parent; return current?.FullName ?? throw new InvalidOperationException("Repository root not found."); }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Emergence.Persistence.Tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
