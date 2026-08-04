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
        "tests/Emergence.Model.Tests/Emergence.Model.Tests.csproj",
        "tests/Emergence.Simulation.Tests/Emergence.Simulation.Tests.csproj",
        "tests/Emergence.Presentation.Contracts.Tests/Emergence.Presentation.Contracts.Tests.csproj",
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
            ["Emergence.Simulation"] = ["Emergence.Foundation", "Emergence.Model", "Emergence.Presentation.Contracts"],
            ["Emergence.Analytics"] = ["Emergence.Foundation", "Emergence.Model"],
            ["Emergence.History"] = ["Emergence.Foundation", "Emergence.Model"],
            ["Emergence.Persistence"] = ["Emergence.Foundation", "Emergence.Model"],
            ["Emergence.Presentation.Contracts"] = ["Emergence.Foundation", "Emergence.Model"],
            ["Emergence.Cli"] = ["Emergence.Foundation", "Emergence.Model", "Emergence.Simulation", "Emergence.Persistence", "Emergence.Presentation.Contracts"],
            ["Emergence.App"] = ["Emergence.Foundation", "Emergence.Model", "Emergence.Simulation", "Emergence.Persistence", "Emergence.Presentation.Contracts"],
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
    public void ModelContainsSessionContractsWithoutBiologicalEntities()
    {
        string source = string.Join("\n", Directory.GetFiles(At("src/Emergence.Model"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("class WorldSessionDefinition", source, StringComparison.Ordinal);
        Assert.Contains("class SchedulerGraph", source, StringComparison.Ordinal);
        string[] prohibited = ["class Cell ", "class Organism ", "class Genome ", "Metabolism", "Reproduction", "Ecology"];
        Assert.All(prohibited, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
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

    [Fact]
    public void SessionAndSchedulerContainNoNondeterministicOrParallelExecutionInputs()
    {
        string source = string.Join("\n", new[] { "src/Emergence.Model", "src/Emergence.Simulation" }
            .SelectMany(directory => Directory.GetFiles(At(directory), "*.cs", SearchOption.AllDirectories))
            .Select(File.ReadAllText));
        string[] prohibited =
        [
            "DateTime.Now", "DateTime.UtcNow", "Stopwatch", "Environment.TickCount", "Guid.NewGuid", "System.Random",
            "Random.Shared", "Task.Run", "Parallel.", "AsParallel", "ThreadPool", "Assembly.GetTypes", "Activator.CreateInstance",
        ];
        Assert.All(prohibited, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void SessionOwnershipAndPresentationBoundariesAreExplicit()
    {
        string simulation = string.Join("\n", Directory.GetFiles(At("src/Emergence.Simulation"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        string presentation = string.Join("\n", Directory.GetFiles(At("src/Emergence.Presentation.Contracts"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("class WorldSession", simulation, StringComparison.Ordinal);
        Assert.DoesNotContain("static WorldSession Current", simulation, StringComparison.Ordinal);
        Assert.DoesNotContain("static CommandProcessorRegistry Current", simulation, StringComparison.Ordinal);
        Assert.DoesNotContain("static CommandProcessorRegistry Instance", simulation, StringComparison.Ordinal);
        Assert.DoesNotContain("static SchedulerGraph Current", simulation, StringComparison.Ordinal);
        Assert.DoesNotContain("static SchedulerGraph Instance", simulation, StringComparison.Ordinal);
        Assert.Contains("HasBiologicalState => false", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("using Godot", presentation, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCallbacksAreDocumentedAndStateless()
    {
        string contracts = File.ReadAllText(At("src/Emergence.Simulation/ExecutionContracts.cs"));
        Assert.Contains("Stateless simulation behavior", contracts, StringComparison.Ordinal);
        Assert.Contains("Stateless command behavior", contracts, StringComparison.Ordinal);
        Assert.Equal(2, contracts.Split("must not retain or mutate an authoritative", StringSplitOptions.None).Length - 1);

        string fixture = File.ReadAllText(At("src/Emergence.Simulation/SessionSelfTest.cs"));
        int start = fixture.IndexOf("private sealed class TraceCommandProcessor", StringComparison.Ordinal);
        int end = fixture.IndexOf("private static FoundationIssue Failure", start, StringComparison.Ordinal);
        string callbackImplementations = fixture[start..end];
        Assert.DoesNotContain("WorldSession", callbackImplementations, StringComparison.Ordinal);
        Assert.DoesNotContain("static WorldSession", callbackImplementations, StringComparison.Ordinal);
        Assert.DoesNotContain("static CommandProcessorRegistry", callbackImplementations, StringComparison.Ordinal);
    }

    [Fact]
    public void AppFrameCallbacksCannotAdvanceLogicalTime()
    {
        string source = File.ReadAllText(At("src/Emergence.App/MainShell.cs"));
        Assert.DoesNotContain("override void _Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("override void _PhysicsProcess", source, StringComparison.Ordinal);
        Assert.Contains("SessionPresentationSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase11EnvironmentOwnershipAndStaticScientificBoundaryAreEnforced()
    {
        string foundation = string.Join("\n", Directory.GetFiles(At("src/Emergence.Foundation/Environment"), "*.cs").Select(File.ReadAllText));
        string model = string.Join("\n", Directory.GetFiles(At("src/Emergence.Model/Environment"), "*.cs").Select(File.ReadAllText));
        string simulation = string.Join("\n", Directory.GetFiles(At("src/Emergence.Simulation/Environment"), "*.cs").Select(File.ReadAllText));
        string persistence = string.Join("\n", Directory.GetFiles(At("src/Emergence.Persistence/WorldPackages"), "*.cs").Select(File.ReadAllText));
        string presentation = string.Join("\n", Directory.GetFiles(At("src/Emergence.Presentation.Contracts"), "*.cs").Select(File.ReadAllText));
        string appViewport = File.ReadAllText(At("src/Emergence.App/FieldViewport.cs"));

        Assert.DoesNotContain("RegionLatticeDefinition", foundation, StringComparison.Ordinal);
        Assert.Contains("class RegionLatticeDefinition", model, StringComparison.Ordinal);
        Assert.Contains("class RegionFieldState", model, StringComparison.Ordinal);
        Assert.Contains("ulong[][] _amountsByChannel", simulation, StringComparison.Ordinal);
        Assert.Contains("class FieldChunkCodec", persistence, StringComparison.Ordinal);
        Assert.Contains("class EnvironmentPresentationSnapshot", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("using Godot", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("AddChild", appViewport, StringComparison.Ordinal);
        Assert.DoesNotContain("_Process", appViewport, StringComparison.Ordinal);

        string authoritative = model + simulation;
        Assert.DoesNotContain("double[] _amounts", authoritative, StringComparison.Ordinal);
        Assert.DoesNotContain("float[] _amounts", authoritative, StringComparison.Ordinal);
        Assert.DoesNotContain("SetAmount", authoritative, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateAmount", authoritative, StringComparison.Ordinal);
        Assert.DoesNotContain("Diffusion", authoritative, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reaction", authoritative, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("namespace Emergence.Biology", authoritative, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase05PersistenceBoundariesAndBiologyExclusionsAreEnforced()
    {
        string[] sourceFiles = new[] { "src/Emergence.Model", "src/Emergence.Simulation", "src/Emergence.Presentation.Contracts" }
            .SelectMany(directory => Directory.GetFiles(At(directory), "*.cs", SearchOption.AllDirectories)).ToArray();
        Assert.DoesNotContain(sourceFiles, path => path.Contains("Biology", StringComparison.OrdinalIgnoreCase));
        string source = string.Join("\n", sourceFiles.Select(File.ReadAllText));
        Assert.DoesNotContain("LoadWorld", source, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Emergence.Biology", source, StringComparison.Ordinal);
        Assert.Contains("class WorldSessionSnapshot", source, StringComparison.Ordinal);

        string persistenceProject = File.ReadAllText(At("src/Emergence.Persistence/Emergence.Persistence.csproj"));
        Assert.DoesNotContain("Emergence.Simulation", persistenceProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Emergence.App", persistenceProject, StringComparison.Ordinal);
        Assert.DoesNotContain("Godot", persistenceProject, StringComparison.Ordinal);
        string simulationProject = File.ReadAllText(At("src/Emergence.Simulation/Emergence.Simulation.csproj"));
        Assert.DoesNotContain("Emergence.Persistence", simulationProject, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistenceUsesNoUnsafeDeserializationExecutableMetadataOrAuthoritativeClockIdentity()
    {
        string source = string.Join("\n", Directory.GetFiles(At("src/Emergence.Persistence"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        string[] prohibited =
        [
            "BinaryFormatter", "NetDataContractSerializer", "LosFormatter", "FormatterServices", "GetUninitializedObject",
            "Assembly.Load", "Activator.CreateInstance", "TypeNameHandling", "JsonExtensionData", "Guid.NewGuid",
            "DateTime.Now", "DateTime.UtcNow", "Random.Shared", "System.Random", "DllImport", "unsafe {",
        ];
        Assert.All(prohibited, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        Assert.DoesNotContain("eventHistory", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("automatic migration", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorldPackageContractsExposeNoMutableByteArraysStreamsOrExecutableProcessors()
    {
        Type[] publicTypes = typeof(Emergence.Persistence.WorldPackages.WorldPackageDocument).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "Emergence.Persistence.WorldPackages")
            .ToArray();
        foreach (Type type in publicTypes)
        {
            foreach (System.Reflection.PropertyInfo property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                Assert.NotEqual(typeof(byte[]), property.PropertyType);
                Assert.False(typeof(Stream).IsAssignableFrom(property.PropertyType));
                Assert.DoesNotContain("ISimulationSystem", property.PropertyType.FullName ?? string.Empty, StringComparison.Ordinal);
                Assert.DoesNotContain("ISessionCommandProcessor", property.PropertyType.FullName ?? string.Empty, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Phase05RLockLeaseUsesExclusiveHandleAndOwnershipSafeCleanup()
    {
        string lease = File.ReadAllText(At("src/Emergence.Persistence/WorldPackages/WorldPackageLockLease.cs"));
        string recovery = File.ReadAllText(At("src/Emergence.Persistence/WorldPackages/WorldPackageRecovery.cs"));

        Assert.Contains("FileMode.OpenOrCreate", lease, StringComparison.Ordinal);
        Assert.Contains("FileShare.None", lease, StringComparison.Ordinal);
        Assert.Contains("FileOptions.DeleteOnClose", lease, StringComparison.Ordinal);
        Assert.Contains("world-package.lock-contention", lease, StringComparison.Ordinal);
        Assert.Contains("world-package.lock-cleanup-warning", lease, StringComparison.Ordinal);
        Assert.DoesNotContain("FileMode.CreateNew", lease, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLastWriteTime", lease, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLastWriteTime", lease, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", lease, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldPackageLock.Delete", recovery, StringComparison.Ordinal);
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
