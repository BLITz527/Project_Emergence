using System.Globalization;

namespace Emergence.Foundation.Versioning;

public readonly record struct SemanticVersion(uint Major, uint Minor, uint Patch) : IComparable<SemanticVersion>
{
    public static SemanticVersion Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out SemanticVersion value) ? value : throw new FormatException("A semantic version must use canonical major.minor.patch form.");
    }

    public static bool TryParse(string? text, out SemanticVersion value)
    {
        value = default;
        if (string.IsNullOrEmpty(text)) return false;
        string[] parts = text.Split('.');
        if (parts.Length != 3 || parts.Any(static part => part.Length == 0 || (part.Length > 1 && part[0] == '0') || part.Any(static c => c is < '0' or > '9'))) return false;
        if (!uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint major)
            || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint minor)
            || !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint patch)) return false;
        value = new(major, minor, patch);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int result = Major.CompareTo(other.Major); if (result != 0) return result;
        result = Minor.CompareTo(other.Minor); return result != 0 ? result : Patch.CompareTo(other.Patch);
    }
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
}
