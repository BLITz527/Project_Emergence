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
            ["Emergence.Cli"] = ["Emergence.Foundation"],
            ["Emergence.App"] = ["Emergence.Foundation", "Emergence.Presentation.Contracts"],
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
