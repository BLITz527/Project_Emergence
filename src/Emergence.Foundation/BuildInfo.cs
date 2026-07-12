using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Emergence.Foundation;

public sealed record BuildDetails(
    string ProductName,
    string SemanticVersion,
    string AssemblyVersion,
    string InformationalVersion,
    string GitCommit,
    string BuildConfiguration,
    string TargetFramework,
    string RuntimeVersion,
    string OperatingSystem,
    string Architecture);

public static class BuildInfo
{
    public const string ProductName = "Project Emergence";
    public const string SemanticVersion = "0.1.0-dev";

    public static BuildDetails Current => ForAssembly(typeof(BuildInfo).Assembly);

    public static BuildDetails ForAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        AssemblyName name = assembly.GetName();
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? SemanticVersion;

        return new BuildDetails(
            ProductName,
            SemanticVersion,
            name.Version?.ToString() ?? "unknown",
            informational,
            Metadata(assembly, "GitCommit"),
            Metadata(assembly, "BuildConfiguration"),
            assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
                ?? AppContext.TargetFrameworkName
                ?? "unknown",
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
    }

    private static string Metadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?.Value
        ?? "unknown";
}
