using System.Runtime.InteropServices;
using Emergence.Foundation;
using Godot;

namespace Emergence.App;

public partial class MainShell : Control
{
    private Label? _statusLabel;
    private RichTextLabel? _diagnostics;

    public override void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        if (TryRunCommandLineMode(arguments))
        {
            return;
        }

        BuildShell();
    }

    private bool TryRunCommandLineMode(string[] arguments)
    {
        if (arguments.Contains("--smoke-exit", StringComparer.Ordinal))
        {
            GD.Print("PROJECT_EMERGENCE_SMOKE_OK: main scene initialized");
            GetTree().Quit(0);
            return true;
        }

        int doctorIndex = Array.IndexOf(arguments, "--doctor-json");
        if (doctorIndex >= 0)
        {
            if (doctorIndex + 1 >= arguments.Length)
            {
                GD.PushError("--doctor-json requires a path");
                GetTree().Quit(2);
                return true;
            }

            DiagnosticReport report = RunAppDiagnostics();
            JsonDefaults.WriteFile(arguments[doctorIndex + 1], report);
            GD.Print($"PROJECT_EMERGENCE_DOCTOR_JSON: {Path.GetFullPath(arguments[doctorIndex + 1])}");
            GetTree().Quit(report.Success ? 0 : 1);
            return true;
        }

        return false;
    }

    private void BuildShell()
    {
        Color primary = new("d8e8ef");
        Color muted = new("8aa3ae");
        Color accent = new("54d6b0");

        MarginContainer margin = new();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 56);
        margin.AddThemeConstantOverride("margin_right", 56);
        margin.AddThemeConstantOverride("margin_top", 48);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(margin);

        VBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 14);
        margin.AddChild(layout);

        Label eyebrow = Label("FOUNDATION / M0.2", 14, accent);
        layout.AddChild(eyebrow);
        Label title = Label("Project Emergence", 42, primary);
        layout.AddChild(title);
        Label subtitle = Label("Milestone 0 — Foundational Domain Types", 20, muted);
        layout.AddChild(subtitle);

        HSeparator separator = new();
        separator.CustomMinimumSize = new Vector2(0, 22);
        layout.AddChild(separator);

        BuildDetails build = BuildInfo.Current;
        GridContainer facts = new() { Columns = 2 };
        facts.AddThemeConstantOverride("h_separation", 32);
        facts.AddThemeConstantOverride("v_separation", 8);
        AddFact(facts, "BUILD", build.InformationalVersion, muted, primary);
        AddFact(facts, ".NET", RuntimeInformation.FrameworkDescription, muted, primary);
        AddFact(facts, "GODOT", Engine.GetVersionInfo()["string"].AsString(), muted, primary);
        AddFact(facts, "STATUS", "Ready — foundation services nominal", muted, accent);
        layout.AddChild(facts);

        _statusLabel = Label("No biological simulation is implemented in this phase.", 15, muted);
        _statusLabel.CustomMinimumSize = new Vector2(0, 44);
        layout.AddChild(_statusLabel);

        _diagnostics = new RichTextLabel
        {
            FitContent = true,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(0, 170),
            BbcodeEnabled = true,
            Text = "[color=#8aa3ae]Diagnostics have not been run.[/color]",
        };
        layout.AddChild(_diagnostics);

        HBoxContainer actions = new();
        actions.AddThemeConstantOverride("separation", 12);
        Button diagnosticsButton = new() { Text = "Run Diagnostics" };
        diagnosticsButton.Pressed += ShowDiagnostics;
        actions.AddChild(diagnosticsButton);
        Button quitButton = new() { Text = "Quit" };
        quitButton.Pressed += () => GetTree().Quit();
        actions.AddChild(quitButton);
        layout.AddChild(actions);
    }

    private void ShowDiagnostics()
    {
        DiagnosticReport report = RunAppDiagnostics();
        _statusLabel!.Text = report.Success ? "Diagnostics passed." : "Diagnostics found a required failure.";
        _diagnostics!.Text = string.Join(
            "\n",
            report.Checks.Select(check => $"[color=#{SeverityColor(check.Severity)}]{check.Severity,-7}[/color]  {check.Summary}: {check.Detail}"));
    }

    private static DiagnosticReport RunAppDiagnostics()
    {
        DiagnosticReport foundation = RuntimeDiagnostics.Run("godot-app", "ProjectEmergence.exe");
        List<DiagnosticCheck> checks = foundation.Checks.ToList();
        checks.Add(new DiagnosticCheck(
            "runtime.godot",
            DiagnosticSeverity.Success,
            "Godot runtime",
            Engine.GetVersionInfo()["string"].AsString()));
        return foundation with { Success = checks.All(check => check.Severity != DiagnosticSeverity.Failure), Checks = checks };
    }

    private static Label Label(string text, int size, Color color)
    {
        Label label = new() { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static void AddFact(GridContainer grid, string name, string value, Color nameColor, Color valueColor)
    {
        grid.AddChild(Label(name, 13, nameColor));
        grid.AddChild(Label(value, 15, valueColor));
    }

    private static string SeverityColor(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Success => "54d6b0",
        DiagnosticSeverity.Warning => "e5c07b",
        DiagnosticSeverity.Failure => "e06c75",
        _ => "d8e8ef",
    };
}
