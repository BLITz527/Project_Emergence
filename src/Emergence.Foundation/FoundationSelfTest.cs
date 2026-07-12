using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emergence.Foundation;

public sealed record FoundationSelfTestReport(
    [property: JsonPropertyOrder(0)] bool Success,
    [property: JsonPropertyOrder(1)] string Vector,
    [property: JsonPropertyOrder(2)] string Utf8Hex,
    [property: JsonPropertyOrder(3)] string Sha256,
    [property: JsonPropertyOrder(4)] string InvariantNumber,
    [property: JsonPropertyOrder(5)] IReadOnlyList<DiagnosticCheck> Checks);

public static class FoundationSelfTest
{
    public const string TestVector = "Project Emergence Phase 0.1";
    public const string ExpectedSha256 = "f4fd4d01fc3f3e82b74c69622c8fed9a8a87bc02ec6ce2f9f18127aec7544ce1";
    public const string ExpectedInvariantNumber = "12345.6789";

    public static FoundationSelfTestReport Run()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(TestVector);
        string utf8Hex = Convert.ToHexStringLower(utf8);
        string hash = Convert.ToHexStringLower(SHA256.HashData(utf8));
        string invariant = 12345.6789m.ToString(CultureInfo.InvariantCulture);
        List<DiagnosticCheck> checks =
        [
            Check("selftest.utf8", utf8.SequenceEqual(Encoding.UTF8.GetBytes(TestVector)), "Deterministic UTF-8", utf8Hex),
            Check("selftest.sha256", hash == ExpectedSha256, "Known SHA-256 vector", hash),
            Check("selftest.culture", invariant == ExpectedInvariantNumber, "Invariant numeric formatting", invariant),
        ];

        var orderingProbe = new OrderingProbe("first", "second");
        string actualJson = JsonSerializer.Serialize(orderingProbe, JsonDefaults.Compact);
        const string expectedJson = "{\"first\":\"first\",\"second\":\"second\"}";
        checks.Add(Check("selftest.json-order", actualJson == expectedJson, "Stable JSON property ordering", actualJson));

        return new FoundationSelfTestReport(
            checks.All(check => check.Severity == DiagnosticSeverity.Success),
            TestVector,
            utf8Hex,
            hash,
            invariant,
            checks);
    }

    private static DiagnosticCheck Check(string id, bool success, string summary, string detail) =>
        new(id, success ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, summary, detail);

    private sealed record OrderingProbe(
        [property: JsonPropertyName("first"), JsonPropertyOrder(0)] string First,
        [property: JsonPropertyName("second"), JsonPropertyOrder(1)] string Second);
}
