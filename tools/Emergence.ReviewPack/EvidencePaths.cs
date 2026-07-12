using System.Security.Cryptography;
using System.Text;

namespace Emergence.ReviewPack;

public static class EvidencePaths
{
    public static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public static string DigestTree(string root)
    {
        using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            digest.AppendData(Encoding.UTF8.GetBytes(relative + "\n"));
            digest.AppendData(File.ReadAllBytes(file));
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    public static bool IsSafeNormalizedRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = path.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    public static string ResolveSafePath(string root, string relative)
    {
        if (!IsSafeNormalizedRelativePath(relative))
        {
            throw new InvalidDataException($"Unsafe evidence path: '{relative}'.");
        }

        string fullRoot = Path.GetFullPath(root);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Evidence path escapes its root: '{relative}'.");
        }
        return fullPath;
    }
}
