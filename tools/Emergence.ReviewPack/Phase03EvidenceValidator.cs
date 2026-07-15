using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Emergence.ReviewPack;

public static class Phase03EvidenceValidator
{
    public const string Seed = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    public const string Domain = "foundation.self-test";
    public const string Scope = "0123456789abcdeffedcba9876543210";
    public const string SampleIndex = "42";
    public const string Encoded = "50452d43414e4f4e4943414c2f3100011c0000000000000050726f6a656374456d657267656e63652e526e67426c6f636b2e7631022000000000000000000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f011400000000000000666f756e646174696f6e2e73656c662d7465737403efcdab8967452301031032547698badcfe042a000000000000000000000000000000030000000000000000";
    public const string Block = "8c39412c47d92f7367ae49de9f122d232aa011d2442e393572265bdc231a34e7";
    public const ulong Lane0 = 8300091537975490956UL;
    public const ulong Bounded10 = 6;
    public const string AlgorithmDigest = "77ebbb568d4c72fcb1cdc7ace7dbc29b3d9e38f5e65e4a44f4a7d8eb9e050b20";
    public const string DomainDigest = "03d1b76efaa64416b934e5a6e37b194ea1ca11199fe1932989bd493db7f545c7";
    public const string ConfigurationDigest = "d538f97c802dc0a5338bfd696ac40c115e106c21727e69e11a6fe26a9e0e58d2";
    public const string DescriptorDigest = "365db3c8a32ee157ad94b2e3051a8ed4eda28c0863999234b3e9acc1dd846086";
    public const string RegistryDigest = "0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d";
    public const string RulesetKey = "00000000000000000000000000000001@1.0.0";

    public static (RngEvidence Rng, RulesetEvidence Rulesets) Evaluate(string reviewRoot)
    {
        RngEvidence rng = EvaluateRng(reviewRoot);
        RulesetEvidence rulesets = EvaluateRulesets(reviewRoot);
        return (rng, rulesets);
    }

    private static RngEvidence EvaluateRng(string root)
    {
        const string dataRelative = "cli/rng-self-test.json"; const string logRelative = "cli/rng-self-test.log";
        List<string> errors = []; string seed = "", domain = "", scope = "", index = "", encoded = "", block = "", domainDigest = "", algorithmDigest = ""; ulong lane = 0, bounded = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Resolve(root, dataRelative))); JsonElement x = document.RootElement;
            Exact(x, "success", "seed", "domain", "scope", "sampleIndex", "canonicalEncodingHex", "block", "lane0", "bounded10", "domainCatalogDigest", "algorithmCatalogDigest", "checks");
            if (x.GetProperty("success").ValueKind != JsonValueKind.True) errors.Add("RNG evidence does not report success=true.");
            seed = String(x, "seed"); domain = String(x, "domain"); scope = String(x, "scope"); index = String(x, "sampleIndex"); encoded = String(x, "canonicalEncodingHex"); block = String(x, "block"); lane = x.GetProperty("lane0").GetUInt64(); bounded = x.GetProperty("bounded10").GetUInt64(); domainDigest = String(x, "domainCatalogDigest"); algorithmDigest = String(x, "algorithmCatalogDigest");
            byte[] independentlyEncoded = EncodeRng(); string independentHex = Convert.ToHexStringLower(independentlyEncoded); string independentBlock = Hash(independentlyEncoded); ulong independentLane = BinaryPrimitives.ReadUInt64LittleEndian(Convert.FromHexString(independentBlock));
            Require(seed == Seed, "RNG seed fixture mismatch.", errors); Require(domain == Domain, "RNG domain mismatch.", errors); Require(scope == Scope, "RNG scope mismatch.", errors); Require(index == SampleIndex, "RNG sample index mismatch.", errors);
            Require(encoded == Encoded && encoded == independentHex, "RNG encoded bytes mismatch.", errors); Require(block == Block && block == independentBlock, "RNG primary block mismatch.", errors); Require(lane == Lane0 && lane == independentLane, "RNG lane zero mismatch.", errors); Require(bounded == Bounded10 && bounded == independentLane % 10, "RNG bounded result mismatch.", errors);
            Require(domainDigest == DomainDigest, "RNG domain-catalog digest mismatch.", errors); Require(algorithmDigest == AlgorithmDigest, "RNG algorithm-catalog digest mismatch.", errors);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or FormatException or OverflowException) { errors.Add($"RNG evidence is invalid: {exception.Message}"); }
        if (!File.Exists(Resolve(root, logRelative))) errors.Add("RNG self-test log is missing.");
        return new("emergence rng-self-test --json", errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed, seed, domain, scope, index, encoded, block, lane, bounded, domainDigest, algorithmDigest, [dataRelative, logRelative], errors.Count == 0 ? "RNG evidence and independently reconstructed SHA-256 vector passed." : string.Join(" ", errors));
    }

    private static RulesetEvidence EvaluateRulesets(string root)
    {
        const string dataRelative = "cli/ruleset-validation.json"; const string logRelative = "cli/ruleset-validation.log";
        const string sourceRelative = "source/rulesets/foundation-reference.ruleset.json"; const string packageRelative = "package/windows-x86_64/rulesets/foundation-reference.ruleset.json";
        List<string> errors = []; int discovered = 0, loaded = 0; string algorithm = "", domains = "", config = "", descriptor = "", registry = ""; string[] keys = [];
        try
        {
            using JsonDocument reportDocument = JsonDocument.Parse(File.ReadAllText(Resolve(root, dataRelative))); JsonElement report = reportDocument.RootElement;
            Exact(report, "success", "directory", "discoveredFiles", "loadedRulesets", "rulesetKeys", "algorithmCatalogDigest", "domainCatalogDigest", "configurationDigest", "descriptorDigest", "registryDigest", "issues", "checks");
            if (report.GetProperty("success").ValueKind != JsonValueKind.True) errors.Add("Ruleset evidence does not report success=true.");
            if (report.GetProperty("issues").GetArrayLength() != 0) errors.Add("Ruleset evidence contains validation issues.");
            if (report.GetProperty("checks").EnumerateArray().Any(x => x.TryGetProperty("severity", out JsonElement severity) && severity.GetString() == "Failure")) errors.Add("Ruleset evidence contains a failed check.");
            discovered = report.GetProperty("discoveredFiles").GetArrayLength(); loaded = report.GetProperty("loadedRulesets").GetInt32(); keys = report.GetProperty("rulesetKeys").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            algorithm = String(report, "algorithmCatalogDigest"); domains = String(report, "domainCatalogDigest"); config = String(report, "configurationDigest"); descriptor = String(report, "descriptorDigest"); registry = String(report, "registryDigest");
            Require(discovered == 1 && loaded == 1 && keys.SequenceEqual([RulesetKey], StringComparer.Ordinal), "Ruleset counts or keys mismatch.", errors);
            byte[] sourceBytes = File.ReadAllBytes(Resolve(root, sourceRelative)); byte[] packageBytes = File.ReadAllBytes(Resolve(root, packageRelative)); Require(sourceBytes.SequenceEqual(packageBytes), "Source/package ruleset mismatch.", errors);
            using JsonDocument sourceDocument = JsonDocument.Parse(sourceBytes, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 32 }); JsonElement ruleset = sourceDocument.RootElement; ValidateRulesetShape(ruleset);
            string independentAlgorithm = CatalogDigest(ruleset.GetProperty("algorithms")); string independentDomains = DomainCatalogDigest(ruleset.GetProperty("rngDomains")); string independentConfig = ConfigurationDigestOf(ruleset.GetProperty("configuration")); string independentDescriptor = DescriptorDigestOf(ruleset, independentAlgorithm, independentDomains, independentConfig); string independentRegistry = RegistryDigestOf(ruleset, independentDescriptor);
            Require(String(ruleset.GetProperty("algorithms"), "digest") == independentAlgorithm, "Embedded algorithm-catalog digest mismatch.", errors); Require(String(ruleset.GetProperty("rngDomains"), "digest") == independentDomains, "Embedded domain-catalog digest mismatch.", errors); Require(String(ruleset.GetProperty("configuration"), "digest") == independentConfig, "Embedded configuration digest mismatch.", errors); Require(String(ruleset, "digest") == independentDescriptor, "Embedded descriptor digest mismatch.", errors);
            Require(algorithm == AlgorithmDigest && independentAlgorithm == AlgorithmDigest, "Ruleset algorithm-catalog digest mismatch.", errors); Require(domains == DomainDigest && independentDomains == DomainDigest, "Ruleset domain-catalog digest mismatch.", errors); Require(config == ConfigurationDigest && independentConfig == ConfigurationDigest, "Ruleset configuration digest mismatch.", errors); Require(descriptor == DescriptorDigest && independentDescriptor == DescriptorDigest, "Ruleset descriptor digest mismatch.", errors); Require(registry == RegistryDigest && independentRegistry == RegistryDigest, "Ruleset registry digest mismatch.", errors);
            ValidateDoctorRegistry(Resolve(root, "app/doctor.json"), errors); ValidateDoctorRegistry(Resolve(root, "package/packaged-doctor.json"), errors);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or FormatException or OverflowException or ArgumentException) { errors.Add($"Ruleset evidence is invalid: {exception.Message}"); }
        if (!File.Exists(Resolve(root, logRelative))) errors.Add("Ruleset-validation log is missing.");
        return new("emergence ruleset validate --directory <repository-rulesets> --json", errors.Count == 0 ? EvidenceStatus.Passed : EvidenceStatus.Failed, "repository-rulesets", discovered, loaded, keys, algorithm, domains, config, descriptor, registry, [dataRelative, logRelative, sourceRelative, packageRelative, "app/doctor.json", "package/packaged-doctor.json"], errors.Count == 0 ? "Ruleset evidence, nested canonical digests, source/package bytes, and App reports passed independent validation." : string.Join(" ", errors));
    }

    private static byte[] EncodeRng()
    {
        Canonical writer = new(); writer.WriteString("ProjectEmergence.RngBlock.v1"); writer.WriteBytes(Convert.FromHexString(Seed)); writer.WriteString(Domain); writer.WriteUInt64(0x0123456789abcdef); writer.WriteUInt64(0xfedcba9876543210); writer.WriteUInt128(42); writer.WriteUInt64(0); return writer.Bytes;
    }
    private static string CatalogDigest(JsonElement catalog) { Exact(catalog, "entries", "digest"); Canonical w = new(); w.WriteString("ProjectEmergence.AlgorithmCatalog.v1"); JsonElement.ArrayEnumerator entries = catalog.GetProperty("entries").EnumerateArray(); JsonElement[] array = entries.ToArray(); w.WriteUInt64((ulong)array.Length); foreach (JsonElement item in array) { string reference = item.GetString()!; int at = reference.IndexOf('@'); w.WriteString(reference[..at]); w.WriteString(reference[(at + 1)..]); } return Hash(w.Bytes); }
    private static string DomainCatalogDigest(JsonElement catalog) { Exact(catalog, "entries", "digest"); JsonElement[] entries = catalog.GetProperty("entries").EnumerateArray().ToArray(); Canonical w = new(); w.WriteString("ProjectEmergence.RngDomainCatalog.v1"); w.WriteUInt64((ulong)entries.Length); foreach (JsonElement item in entries) w.WriteString(item.GetString()!); return Hash(w.Bytes); }
    private static string ConfigurationDigestOf(JsonElement config)
    {
        Exact(config, "schemaId", "schemaVersion", "entries", "digest"); JsonElement[] entries = config.GetProperty("entries").EnumerateArray().ToArray(); Canonical w = new(); w.WriteString("ProjectEmergence.Configuration.v1"); w.WriteString(String(config, "schemaId")); w.WriteString(String(config, "schemaVersion")); w.WriteUInt64((ulong)entries.Length);
        foreach (JsonElement entry in entries) { Exact(entry, "key", "value"); JsonElement value = entry.GetProperty("value"); Exact(value, "kind", "value"); string kind = String(value, "kind"); w.WriteString(String(entry, "key")); w.WriteString(kind); if (kind == "Boolean") w.WriteBoolean(value.GetProperty("value").GetBoolean()); else if (kind == "UInt64") w.WriteUInt64(ulong.Parse(String(value, "value"), System.Globalization.CultureInfo.InvariantCulture)); else if (kind == "Digest") w.WriteDigest(Convert.FromHexString(String(value, "value"))); else w.WriteString(value.GetProperty("value").GetString()!); }
        return Hash(w.Bytes);
    }
    private static string DescriptorDigestOf(JsonElement ruleset, string algorithms, string domains, string config) { string key = String(ruleset, "key"); int at = key.IndexOf('@'); Canonical w = new(); w.WriteString("ProjectEmergence.RulesetManifest.v1"); w.WriteString(String(ruleset, "formatVersion")); w.WriteString(key[..at]); w.WriteString(key[(at + 1)..]); w.WriteString(String(ruleset, "displayName")); w.WriteDigest(Convert.FromHexString(algorithms)); w.WriteDigest(Convert.FromHexString(domains)); w.WriteDigest(Convert.FromHexString(config)); return Hash(w.Bytes); }
    private static string RegistryDigestOf(JsonElement ruleset, string descriptor) { string key = String(ruleset, "key"); int at = key.IndexOf('@'); Canonical w = new(); w.WriteString("ProjectEmergence.RulesetRegistry.v1"); w.WriteUInt64(1); w.WriteString(key[..at]); w.WriteString(key[(at + 1)..]); w.WriteDigest(Convert.FromHexString(descriptor)); return Hash(w.Bytes); }
    private static void ValidateRulesetShape(JsonElement root) { Exact(root, "formatVersion", "key", "displayName", "algorithms", "rngDomains", "configuration", "digest"); Exact(root.GetProperty("algorithms"), "entries", "digest"); Exact(root.GetProperty("rngDomains"), "entries", "digest"); Exact(root.GetProperty("configuration"), "schemaId", "schemaVersion", "entries", "digest"); }
    private static void ValidateDoctorRegistry(string path, List<string> errors) { using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)); JsonElement[] checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray(); bool match = checks.Any(x => x.GetProperty("id").GetString() == "ruleset.registry" && x.GetProperty("severity").GetString() == "Success" && (x.GetProperty("detail").GetString() ?? "").Contains(RegistryDigest, StringComparison.Ordinal)); Require(match, $"Doctor ruleset registry mismatch: {path}.", errors); }
    private static void Exact(JsonElement element, params string[] names) { if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected object."); HashSet<string> expected = new(names, StringComparer.Ordinal), seen = new(StringComparer.Ordinal); foreach (JsonProperty p in element.EnumerateObject()) { if (!expected.Contains(p.Name)) throw new JsonException($"Unknown property '{p.Name}'."); if (!seen.Add(p.Name)) throw new JsonException($"Duplicate property '{p.Name}'."); } if (seen.Count != expected.Count) throw new JsonException("Missing required property."); }
    private static string String(JsonElement x, string property) => x.GetProperty(property).GetString() ?? throw new JsonException($"{property} must be a string.");
    private static string Resolve(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static void Require(bool condition, string message, List<string> errors) { if (!condition) errors.Add(message); }

    private sealed class Canonical
    {
        private readonly List<byte> _bytes = [.. Encoding.ASCII.GetBytes("PE-CANONICAL/1\0")];
        public byte[] Bytes => [.. _bytes];
        public void WriteString(string value) { byte[] bytes = Encoding.UTF8.GetBytes(value); Prefix(0x01, bytes.Length); _bytes.AddRange(bytes); }
        public void WriteBytes(ReadOnlySpan<byte> value) { Prefix(0x02, value.Length); _bytes.AddRange(value.ToArray()); }
        public void WriteUInt64(ulong value) { _bytes.Add(0x03); Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); _bytes.AddRange(bytes.ToArray()); }
        public void WriteUInt128(UInt128 value) { _bytes.Add(0x04); Span<byte> bytes = stackalloc byte[16]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, (ulong)value); BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], (ulong)(value >> 64)); _bytes.AddRange(bytes.ToArray()); }
        public void WriteBoolean(bool value) { _bytes.Add(0x05); _bytes.Add(value ? (byte)1 : (byte)0); }
        public void WriteDigest(ReadOnlySpan<byte> value) { if (value.Length != 32) throw new FormatException("Digest length mismatch."); _bytes.Add(0x06); _bytes.AddRange(value.ToArray()); }
        private void Prefix(byte tag, int length) { _bytes.Add(tag); Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, (ulong)length); _bytes.AddRange(bytes.ToArray()); }
    }
}
