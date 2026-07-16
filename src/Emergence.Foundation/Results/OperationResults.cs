using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emergence.Foundation.Configuration;

namespace Emergence.Foundation.Results;

[JsonConverter(typeof(IssueSeverityJsonConverter))]
public enum IssueSeverity { Information, Warning, Error, Critical }

internal sealed class IssueSeverityJsonConverter : JsonConverter<IssueSeverity>
{
    public override IssueSeverity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("IssueSeverity must be an exact canonical JSON string.");
        return reader.GetString() switch
        {
            "Information" => IssueSeverity.Information,
            "Warning" => IssueSeverity.Warning,
            "Error" => IssueSeverity.Error,
            "Critical" => IssueSeverity.Critical,
            _ => throw new JsonException("Unknown IssueSeverity."),
        };
    }

    public override void Write(Utf8JsonWriter writer, IssueSeverity value, JsonSerializerOptions options)
    {
        string text = value switch
        {
            IssueSeverity.Information => "Information",
            IssueSeverity.Warning => "Warning",
            IssueSeverity.Error => "Error",
            IssueSeverity.Critical => "Critical",
            _ => throw new JsonException("Undefined IssueSeverity values cannot be written."),
        };
        writer.WriteStringValue(text);
    }
}

public readonly record struct IssueCode
{
    public IssueCode(string value) { _ = new ConfigurationKey(value); Value = value; }
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static IssueCode Parse(string text) => new(text);
    public static bool TryParse(string? text, out IssueCode value) { try { value = new(text!); return true; } catch (ArgumentException) { value = default; return false; } }
    public override string ToString() => Value ?? string.Empty;
}

public sealed record FoundationIssue
{
    public FoundationIssue(IssueCode code, IssueSeverity severity, string summary, string detail)
    {
        if (code.IsEmpty) throw new ArgumentException("Issue code cannot be empty.", nameof(code));
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
        ArgumentNullException.ThrowIfNull(summary); ArgumentNullException.ThrowIfNull(detail);
        Code = code; Severity = severity; Summary = summary; Detail = detail;
    }

    [JsonPropertyOrder(0)] public IssueCode Code { get; }
    [JsonPropertyOrder(1)] public IssueSeverity Severity { get; }
    [JsonPropertyOrder(2)] public string Summary { get; }
    [JsonPropertyOrder(3)] public string Detail { get; }
}

public class OperationResult
{
    private readonly ReadOnlyCollection<FoundationIssue> _issues;
    protected OperationResult(IEnumerable<FoundationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        FoundationIssue[] copy = issues.ToArray();
        if (copy.Any(static issue => issue is null)) throw new ArgumentException("Issues cannot contain null.", nameof(issues));
        if (copy.Any(static issue => !Enum.IsDefined(issue.Severity))) throw new ArgumentException("Issues cannot contain an undefined severity.", nameof(issues));
        _issues = Array.AsReadOnly(copy);
    }
    [JsonPropertyOrder(0)] public bool Success => !_issues.Any(static issue => issue.Severity is IssueSeverity.Error or IssueSeverity.Critical);
    [JsonPropertyOrder(1)] public IReadOnlyList<FoundationIssue> Issues => _issues;
    public static OperationResult FromIssues(IEnumerable<FoundationIssue> issues) => new(issues);
    public static OperationResult Succeeded(params FoundationIssue[] issues) { OperationResult result = new(issues); return result.Success ? result : throw new ArgumentException("A successful result cannot contain Error or Critical issues.", nameof(issues)); }
    public static OperationResult Failed(params FoundationIssue[] issues) { OperationResult result = new(issues); return !result.Success ? result : throw new ArgumentException("A failed result requires an Error or Critical issue.", nameof(issues)); }
}

public sealed class OperationResult<T> : OperationResult
{
    private readonly T? _value;
    private OperationResult(T? value, bool hasValue, IEnumerable<FoundationIssue> issues) : base(issues)
    {
        HasValue = hasValue;
        _value = value;
        if (Success != hasValue) throw new ArgumentException("Result success and value presence must agree.");
        if (hasValue && value is null) throw new ArgumentNullException(nameof(value));
    }
    [JsonPropertyOrder(1)] public bool HasValue { get; }
    [JsonIgnore] public T Value => HasValue ? _value! : throw new InvalidOperationException("A failed result has no value.");
    [JsonPropertyName("value"), JsonPropertyOrder(2), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public object? SerializedValue => HasValue ? _value : null;
    [JsonPropertyOrder(3)] public new IReadOnlyList<FoundationIssue> Issues => base.Issues;
    public bool TryGetValue([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value) { value = _value!; return HasValue; }
    public static OperationResult<T> Succeeded(T value, params FoundationIssue[] issues) => new(value, true, issues);
    public new static OperationResult<T> Failed(params FoundationIssue[] issues) => new(default, false, issues);
}
