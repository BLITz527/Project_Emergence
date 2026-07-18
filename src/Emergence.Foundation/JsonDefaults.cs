using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Text;

namespace Emergence.Foundation;

public static class JsonDefaults
{
    public static JsonSerializerOptions Compact { get; } = Create(false);
    public static JsonSerializerOptions Indented { get; } = Create(true);

    public static string Serialize<T>(T value, bool indented = true) =>
        JsonSerializer.Serialize(value, indented ? Indented : Compact);

    public static void WriteFile<T>(string path, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, StrictUtf8.GetBytes(Serialize(value) + Environment.NewLine));
    }

    private static JsonSerializerOptions Create(bool indented)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            Encoder = JavaScriptEncoder.Default,
        };
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticSeverity>());
        FoundationJsonConverters.AddTo(options);
        return options;
    }
}
