namespace Emergence.Foundation.Versioning;

public readonly record struct AlgorithmId : IComparable<AlgorithmId>
{
    public AlgorithmId(string value)
    {
        DottedName.Validate(value, 96, 32, nameof(value));
        Value = value;
    }
    public string Value { get; }
    public static AlgorithmId Parse(string text) => new(text);
    public static bool TryParse(string? text, out AlgorithmId value) { try { value = new(text!); return true; } catch (ArgumentException) { value = default; return false; } }
    public int CompareTo(AlgorithmId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct AlgorithmReference(AlgorithmId Id, SemanticVersion Version) : IComparable<AlgorithmReference>
{
    public static AlgorithmReference Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int separator = text.IndexOf('@');
        if (separator <= 0 || separator != text.LastIndexOf('@') || separator == text.Length - 1) throw new FormatException("An algorithm reference must use algorithm.id@major.minor.patch form.");
        return new(new AlgorithmId(text[..separator]), SemanticVersion.Parse(text[(separator + 1)..]));
    }
    public static bool TryParse(string? text, out AlgorithmReference value) { try { value = Parse(text!); return true; } catch (Exception exception) when (exception is ArgumentException or FormatException) { value = default; return false; } }
    public int CompareTo(AlgorithmReference other) { int result = Id.CompareTo(other.Id); return result != 0 ? result : Version.CompareTo(other.Version); }
    public override string ToString() => $"{Id}@{Version}";
}

internal static class DottedName
{
    public static void Validate(string? value, int totalMaximum, int segmentMaximum, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > totalMaximum) throw new ArgumentException($"Value length must be 1 through {totalMaximum}.", parameterName);
        foreach (string segment in value.Split('.'))
        {
            if (segment.Length is 0 || segment.Length > segmentMaximum || segment[0] is < 'a' or > 'z'
                || segment.Skip(1).Any(static c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            {
                throw new ArgumentException("Value must contain canonical lowercase ASCII dotted segments.", parameterName);
            }
        }
    }
}
