using System.Text.Json;

namespace Emergence.Model;

internal static class StrictModelJson
{
    public static void Exact(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a JSON object.");
        HashSet<string> allowed = new(expected, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
                throw new JsonException($"Unexpected or duplicate property '{property.Name}'.");
        }
        if (!allowed.SetEquals(seen)) throw new JsonException("JSON object is missing required properties.");
    }
}
