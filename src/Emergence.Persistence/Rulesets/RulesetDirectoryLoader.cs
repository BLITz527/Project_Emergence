using System.Text;
using System.Text.Json;
using Emergence.Foundation;
using Emergence.Foundation.Rulesets;

namespace Emergence.Persistence.Rulesets;

public sealed record RulesetLoadIssue(string Code, string FileName, string Reason);

public sealed record RulesetDirectoryLoadResult(
    bool Success,
    IReadOnlyList<string> DiscoveredFiles,
    RulesetRegistry? Registry,
    IReadOnlyList<RulesetLoadIssue> Issues);

public sealed class RulesetDirectoryLoader
{
    public const int MaximumFileCount = 256;
    public const long MaximumFileBytes = 1024 * 1024;
    public const long MaximumTotalBytes = 8 * 1024 * 1024;
    public const int MaximumJsonDepth = 32;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = MaximumJsonDepth };

    public RulesetDirectoryLoadResult Load(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return Failure([], "ruleset.directory.invalid", string.Empty, "A ruleset directory path is required.");
        string root;
        try { root = Path.GetFullPath(directoryPath); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return Failure([], "ruleset.directory.invalid", string.Empty, "The ruleset directory path is invalid."); }
        if (!Directory.Exists(root)) return Failure([], "ruleset.directory.missing", string.Empty, "The ruleset directory does not exist.");
        try
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return Failure([], "ruleset.directory.reparse", string.Empty, "The ruleset directory cannot be a reparse point.");
            string[] files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Where(static path => Path.GetFileName(path).EndsWith(".ruleset.json", StringComparison.Ordinal))
                .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();
            string[] names = files.Select(Path.GetFileName).ToArray()!;
            if (files.Length == 0) return Failure(names, "ruleset.directory.empty", string.Empty, "No top-level *.ruleset.json files were found.");
            if (files.Length > MaximumFileCount) return Failure(names, "ruleset.limit.file-count", string.Empty, $"The directory contains more than {MaximumFileCount} matching files.");

            long total = 0;
            foreach (string file in files)
            {
                string name = Path.GetFileName(file); string full = Path.GetFullPath(file);
                if (!IsDirectChild(root, full)) return Failure(names, "ruleset.path.escape", name, "A discovered path escapes the requested directory.");
                FileAttributes attributes = File.GetAttributes(full);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return Failure(names, "ruleset.file.reparse", name, "A ruleset input cannot be a directory or reparse point.");
                long length = new FileInfo(full).Length;
                if (length > MaximumFileBytes) return Failure(names, "ruleset.limit.file-size", name, $"The file exceeds {MaximumFileBytes} bytes.");
                total = checked(total + length);
                if (total > MaximumTotalBytes) return Failure(names, "ruleset.limit.total-size", name, $"The matching files exceed {MaximumTotalBytes} total bytes.");
            }

            List<RulesetDescriptor> descriptors = [];
            foreach (string file in files)
            {
                string name = Path.GetFileName(file); string full = Path.GetFullPath(file); long length = new FileInfo(full).Length;
                byte[] bytes = ReadExact(full, length);
                if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf) return Failure(names, "ruleset.utf8.bom", name, "UTF-8 BOM is not permitted.");
                string json;
                try { json = StrictUtf8.GetString(bytes); }
                catch (DecoderFallbackException) { return Failure(names, "ruleset.utf8.invalid", name, "The file is not valid UTF-8."); }
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json, DocumentOptions);
                    RulesetDescriptor descriptor = JsonSerializer.Deserialize<RulesetDescriptor>(document.RootElement, JsonDefaults.Compact) ?? throw new JsonException("A ruleset descriptor cannot be null.");
                    descriptors.Add(descriptor);
                }
                catch (JsonException exception) { return Failure(names, "ruleset.json.invalid", name, Normalize(exception.Message)); }
                catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException or InvalidOperationException) { return Failure(names, "ruleset.value.invalid", name, Normalize(exception.Message)); }
            }
            try { RulesetRegistry registry = new(descriptors); return new(true, Array.AsReadOnly(names), registry, Array.Empty<RulesetLoadIssue>()); }
            catch (ArgumentException exception) { return Failure(names, "ruleset.registry.invalid", string.Empty, Normalize(exception.Message)); }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PathTooLongException or OverflowException)
        {
            return Failure([], "ruleset.io.failure", string.Empty, Normalize(exception.Message));
        }
    }

    private static byte[] ReadExact(string path, long length)
    {
        byte[] bytes = GC.AllocateUninitializedArray<byte>(checked((int)length));
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new IOException("The ruleset file changed while it was being read.");
        return bytes;
    }
    private static bool IsDirectChild(string root, string path) => string.Equals(Path.GetDirectoryName(path), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    private static RulesetDirectoryLoadResult Failure(IReadOnlyList<string> files, string code, string file, string reason) => new(false, Array.AsReadOnly(files.ToArray()), null, Array.AsReadOnly([new RulesetLoadIssue(code, file, reason)]));
    private static string Normalize(string message) { string line = message.Split(['\r', '\n'], 2)[0]; return line.Length <= 500 ? line : line[..500]; }
}
