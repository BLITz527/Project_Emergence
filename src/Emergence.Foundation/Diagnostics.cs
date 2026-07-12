using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Emergence.Foundation;

[JsonConverter(typeof(JsonStringEnumConverter<DiagnosticSeverity>))]
public enum DiagnosticSeverity
{
    Success,
    Warning,
    Failure,
}

public sealed record DiagnosticCheck(
    [property: JsonPropertyOrder(0)] string Id,
    [property: JsonPropertyOrder(1)] DiagnosticSeverity Severity,
    [property: JsonPropertyOrder(2)] string Summary,
    [property: JsonPropertyOrder(3)] string Detail);

public sealed record DiagnosticReport(
    [property: JsonPropertyOrder(0)] string Product,
    [property: JsonPropertyOrder(1)] string Mode,
    [property: JsonPropertyOrder(2)] bool Success,
    [property: JsonPropertyOrder(3)] BuildDetails Build,
    [property: JsonPropertyOrder(4)] IReadOnlyList<DiagnosticCheck> Checks);

public static class RuntimeDiagnostics
{
    public static DiagnosticReport Run(string mode, string? packagedExecutableName = null)
    {
        List<DiagnosticCheck> checks =
        [
            Success("process.architecture", "Process architecture", RuntimeInformation.ProcessArchitecture.ToString()),
            Success("runtime.dotnet", ".NET runtime", RuntimeInformation.FrameworkDescription),
            Success("runtime.mode", "Repository/runtime mode", mode),
            Success("path.cwd", "Current working directory", Environment.CurrentDirectory),
        ];

        checks.Add(CheckWritable("path.temp", "Writable temporary directory", Path.GetTempPath()));
        string localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectEmergence");
        checks.Add(CheckWritable("path.localAppData", "Writable application-data directory", localData));

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(item => item.GetName().Name?.StartsWith("Emergence.", StringComparison.Ordinal) == true)
                     .OrderBy(item => item.GetName().Name, StringComparer.Ordinal))
        {
            AssemblyName name = assembly.GetName();
            checks.Add(Success(
                $"assembly.{name.Name}",
                "Loaded Project Emergence assembly",
                $"{name.Name} {name.Version}"));
        }

        string layout = packagedExecutableName is not null
            && string.Equals(Path.GetFileName(Environment.ProcessPath), packagedExecutableName, StringComparison.OrdinalIgnoreCase)
            ? "packaged"
            : "development";
        checks.Add(Success("runtime.layout", "Process layout", layout));

        return new DiagnosticReport(
            BuildInfo.ProductName,
            mode,
            checks.All(check => check.Severity != DiagnosticSeverity.Failure),
            BuildInfo.Current,
            checks);
    }

    private static DiagnosticCheck CheckWritable(string id, string summary, string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            string probe = Path.Combine(path, $"emergence-write-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Success(id, summary, path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new DiagnosticCheck(id, DiagnosticSeverity.Failure, summary, exception.Message);
        }
    }

    private static DiagnosticCheck Success(string id, string summary, string detail) =>
        new(id, DiagnosticSeverity.Success, summary, detail);
}
