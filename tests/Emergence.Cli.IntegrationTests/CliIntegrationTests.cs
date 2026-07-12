using System.Globalization;
using System.Text.Json;
using Emergence.Cli;

namespace Emergence.Cli.IntegrationTests;

public sealed class CliIntegrationTests
{
    [Fact]
    public async Task VersionReturnsRequiredFields()
    {
        Invocation result = await Invoke("version");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Product: Project Emergence", result.Output, StringComparison.Ordinal);
        Assert.Contains("Version:", result.Output, StringComparison.Ordinal);
        Assert.Contains("Target framework:", result.Output, StringComparison.Ordinal);
        Assert.Contains("Runtime:", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoctorJsonIsParseable()
    {
        string path = TemporaryJsonPath();
        try
        {
            Invocation result = await Invoke("doctor", "--json", path);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));

            Assert.Equal(0, result.ExitCode);
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("cli", document.RootElement.GetProperty("mode").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SelfTestJsonIsParseableAndSuccessful()
    {
        string path = TemporaryJsonPath();
        try
        {
            Invocation result = await Invoke("self-test", "--json", path);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));

            Assert.Equal(0, result.ExitCode);
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InvalidCommandReturnsUsefulHelp()
    {
        Invocation result = await Invoke("not-a-command");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown command", result.Error, StringComparison.Ordinal);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CultureDoesNotAlterMachineReadableOutput()
    {
        CultureInfo prior = CultureInfo.CurrentCulture;
        string first = TemporaryJsonPath();
        string second = TemporaryJsonPath();
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(0, (await Invoke("self-test", "--json", first)).ExitCode);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(0, (await Invoke("self-test", "--json", second)).ExitCode);
            Assert.Equal(await File.ReadAllTextAsync(first), await File.ReadAllTextAsync(second));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
            File.Delete(first);
            File.Delete(second);
        }
    }

    private static async Task<Invocation> Invoke(params string[] arguments)
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        int exitCode = await CliApplication.RunAsync(arguments, output, error);
        return new Invocation(exitCode, output.ToString(), error.ToString());
    }

    private static string TemporaryJsonPath() => Path.Combine(Path.GetTempPath(), $"emergence-cli-test-{Guid.NewGuid():N}.json");

    private sealed record Invocation(int ExitCode, string Output, string Error);
}
