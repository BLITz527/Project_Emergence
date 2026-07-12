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
    public static string SemanticVersion => SemanticVersionFor(typeof(BuildInfo).Assembly);

    public static BuildDetails Current => ForAssembly(typeof(BuildInfo).Assembly);

    public static BuildDetails ForAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        AssemblyName name = assembly.GetName();
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? SemanticVersion;

        return new BuildDetails(
            ProductName,
            SemanticVersionFor(assembly),
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

    private static string SemanticVersionFor(Assembly assembly)
    {
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        int metadata = informational.IndexOf('+');
        return metadata < 0 ? informational : informational[..metadata];
    }
}
