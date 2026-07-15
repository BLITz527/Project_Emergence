using System.Xml.Linq;

namespace Emergence.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string[] ExpectedProjects =
    [
        "src/Emergence.Foundation/Emergence.Foundation.csproj",
        "src/Emergence.Model/Emergence.Model.csproj",
        "src/Emergence.Simulation/Emergence.Simulation.csproj",
        "src/Emergence.Analytics/Emergence.Analytics.csproj",
        "src/Emergence.History/Emergence.History.csproj",
        "src/Emergence.Persistence/Emergence.Persistence.csproj",
        "src/Emergence.Presentation.Contracts/Emergence.Presentation.Contracts.csproj",
        "src/Emergence.Cli/Emergence.Cli.csproj",
        "src/Emergence.App/Emergence.App.csproj",
        "tests/Emergence.Foundation.Tests/Emergence.Foundation.Tests.csproj",
        "tests/Emergence.Persistence.Tests/Emergence.Persistence.Tests.csproj",
        "tests/Emergence.Architecture.Tests/Emergence.Architecture.Tests.csproj",
        "tests/Emergence.Cli.IntegrationTests/Emergence.Cli.IntegrationTests.csproj",
        "tests/Emergence.ReviewPack.Tests/Emergence.ReviewPack.Tests.csproj",
        "tools/Emergence.ReviewPack/Emergence.ReviewPack.csproj",
    ];

    [Fact]
    public void AllExpectedProjectsExist() =>
        Assert.All(ExpectedProjects, path => Assert.True(File.Exists(At(path)), $"Missing {path}"));

    [Fact]
    public void NoNonAppProjectReferencesGodot()
    {
        string[] projectFiles = Directory.GetFiles(Root, "*.csproj", SearchOption.AllDirectories);
        foreach (string project in projectFiles.Where(path => !path.Contains("Emergence.App", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("GodotSharp", File.ReadAllText(project), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Godot.NET.Sdk", File.ReadAllText(project), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FoundationHasNoProjectReferences() =>
        Assert.Empty(ProjectReferences("src/Emergence.Foundation/Emergence.Foundation.csproj"));

    [Fact]
    public void CoreProjectsDoNotReferenceApp()
    {
        foreach (string project in ExpectedProjects.Where(path => path.StartsWith("src/", StringComparison.Ordinal) && !path.Contains("Emergence.App", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(ProjectReferences(project), reference => reference == "Emergence.App");
        }
    }

    [Fact]
    public void ProjectReferencesFollowApprovedDirections()
    {
        Dictionary<string, string[]> approved = new(StringComparer.Ordinal)
        {
            ["Emergence.Foundation"] = [],
            ["Emergence.Model"] = ["Emergence.Foundation"],
            ["Emergence.Simulation"] = ["Emergence.Foundation", "Emergence.Model"],
            ["Emergence.Analytics"] = ["Emergence.Foundation", "Emergence.Model"],
            ["Emergence.History"] = ["Emergence.Foundation", "Emergence.Model"],
            ["Emergence.Persistence"] = ["Emergence.Foundation"],
            ["Emergence.Presentation.Contracts"] = ["Emergence.Foundation"],
            ["Emergence.Cli"] = ["Emergence.Foundation", "Emergence.Persistence"],
            ["Emergence.App"] = ["Emergence.Foundation", "Emergence.Persistence", "Emergence.Presentation.Contracts"],
        };

        foreach ((string project, string[] allowed) in approved)
        {
            string relative = $"src/{project}/{project}.csproj";
            Assert.Subset(allowed.ToHashSet(StringComparer.Ordinal), ProjectReferences(relative).ToHashSet(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void OnlyApprovedPackagesAreReferenced()
    {
        HashSet<string> approved = new(StringComparer.OrdinalIgnoreCase)
        {
            "coverlet.collector",
            "Microsoft.NET.Test.Sdk",
            "xunit",
            "xunit.runner.visualstudio",
        };

        foreach (string project in Directory.GetFiles(Root, "*.csproj", SearchOption.AllDirectories))
        {
            XDocument document = XDocument.Load(project);
            foreach (XElement package in document.Descendants("PackageReference"))
            {
                string packageName = package.Attribute("Include")?.Value
                    ?? throw new InvalidDataException($"PackageReference in {project} has no Include attribute.");
                Assert.Contains(packageName, approved);
            }
        }
    }

    [Fact]
    public void FoundationDomainCodeContainsNoNondeterministicInputs()
    {
        string[] directories = ["Identifiers", "Time", "Quantities", "Hashing", "Versioning", "Configuration", "Randomness", "Rulesets"];
        string[] prohibited = ["Guid.NewGuid", "DateTime.Now", "DateTime.UtcNow", "System.Random", "Random.Shared", "RandomNumberGenerator", "Time.GetTicks", "Godot"];
        foreach (string directory in directories)
        {
            foreach (string file in Directory.GetFiles(At($"src/Emergence.Foundation/{directory}"), "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                Assert.All(prohibited, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void DomainTypesExistOnlyInFoundation()
    {
        string[] typeNames = ["StableId128", "SimulationTick", "MatterAmount", "CanonicalHashWriter", "ImmutableConfiguration", "OperationResult"];
        foreach (string file in Directory.GetFiles(At("src"), "*.cs", SearchOption.AllDirectories).Where(path => !path.Contains("Emergence.Foundation", StringComparison.Ordinal)))
        {
            string source = File.ReadAllText(file);
            Assert.All(typeNames, type => Assert.DoesNotContain($"class {type}", source, StringComparison.Ordinal));
            Assert.All(typeNames, type => Assert.DoesNotContain($"struct {type}", source, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ModelStillContainsOnlyItsMarker()
    {
        string[] files = Directory.GetFiles(At("src/Emergence.Model"), "*.cs", SearchOption.TopDirectoryOnly);
        Assert.Single(files);
        Assert.Contains("AssemblyMarker", File.ReadAllText(files[0]), StringComparison.Ordinal);
    }

    [Fact]
    public void NoGlobalMutableIdAllocatorExists()
    {
        foreach (string file in Directory.GetFiles(At("src"), "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain("IdAllocator", source, StringComparison.Ordinal);
            Assert.DoesNotContain("static CheckedSequenceCounter", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ConfigurationHasNoCodeExecutionMechanism()
    {
        string source = string.Join("\n", Directory.GetFiles(At("src/Emergence.Foundation/Configuration"), "*.cs").Select(File.ReadAllText));
        string[] prohibited = ["System.Reflection", "System.Linq.Expressions", "CSharpScript", "Assembly.Load", "Delegate", "dynamic"];
        Assert.All(prohibited, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void RulesetLoadingHasNoExecutableOrNetworkMechanism()
    {
        string source = string.Join("\n", Directory.GetFiles(At("src/Emergence.Persistence/Rulesets"), "*.cs").Select(File.ReadAllText));
        string[] prohibited = ["System.Net", "HttpClient", "Assembly.Load", "Activator.CreateInstance", "System.Reflection", "CSharpScript", "Compile", "DllImport"];
        Assert.All(prohibited, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void AddressedRngAndRegistryContainNoGlobalMutableStateOrFallbackApi()
    {
        string rng = File.ReadAllText(At("src/Emergence.Foundation/Randomness/RngValues.cs"));
        string rulesets = File.ReadAllText(At("src/Emergence.Foundation/Rulesets/RulesetTypes.cs"));
        Assert.DoesNotContain("static DeterministicAddressedRng", rng, StringComparison.Ordinal);
        Assert.DoesNotContain("cursor", rng, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stream", rng, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latest", rulesets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wildcard", rulesets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("static RulesetRegistry Instance", rulesets, StringComparison.Ordinal);
        Assert.DoesNotContain("static RulesetRegistry Current", rulesets, StringComparison.Ordinal);
    }

    [Fact]
    public void TestAndReviewScriptsCoverEveryTestProject()
    {
        string[] testProjects = Directory.GetFiles(At("tests"), "*.csproj", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension).OrderBy(static name => name, StringComparer.Ordinal).ToArray()!;
        string testScript = File.ReadAllText(At("eng/test.ps1"));
        string reviewSource = File.ReadAllText(At("tools/Emergence.ReviewPack/ReviewPackApplication.cs"));
        Assert.All(testProjects, project => { Assert.Contains(project, testScript, StringComparison.Ordinal); Assert.Contains(project, reviewSource, StringComparison.Ordinal); });
    }

    private static IEnumerable<string> ProjectReferences(string relativeProject)
    {
        XDocument document = XDocument.Load(At(relativeProject));
        return document.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value));
    }

    private static string At(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ProjectEmergence.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate ProjectEmergence.slnx.");
    }
}
