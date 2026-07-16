using System.Runtime.InteropServices;
using Emergence.Foundation;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Persistence.Rulesets;
using Emergence.Presentation.Contracts;
using Emergence.Simulation;
using Godot;

namespace Emergence.App;

public partial class MainShell : Control
{
    private Label? _statusLabel;
    private RichTextLabel? _diagnostics;
    private WorldSession? _session;
    private SessionPresentationSnapshot? _snapshot;

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
            RulesetDirectoryLoadResult rulesets = LoadRulesets();
            bool success = TryCreateSessionSnapshot(rulesets, out _, out SessionPresentationSnapshot? snapshot, out string detail)
                && snapshot is not null
                && snapshot.Tick.Value == UInt128.Zero
                && snapshot.Status == WorldSessionStatus.Paused
                && !snapshot.HasBiologicalState;
            if (success) GD.Print("PROJECT_EMERGENCE_SMOKE_OK: main scene initialized; reference ruleset and paused nonbiological session validated");
            else GD.PushError($"PROJECT_EMERGENCE_SMOKE_FAILED: {detail}");
            GetTree().Quit(success ? 0 : 1);
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

        Label eyebrow = Label("FOUNDATION / M0.4", 14, accent);
        layout.AddChild(eyebrow);
        Label title = Label("Project Emergence", 42, primary);
        layout.AddChild(title);
        Label subtitle = Label("Milestone 0 — World Session and Deterministic Scheduler", 20, muted);
        layout.AddChild(subtitle);

        HSeparator separator = new();
        separator.CustomMinimumSize = new Vector2(0, 22);
        layout.AddChild(separator);

        BuildDetails build = BuildInfo.Current;
        RulesetDirectoryLoadResult rulesets = LoadRulesets();
        bool sessionReady = TryCreateSessionSnapshot(rulesets, out _session, out _snapshot, out string sessionDetail);
        GridContainer facts = new() { Columns = 2 };
        facts.AddThemeConstantOverride("h_separation", 32);
        facts.AddThemeConstantOverride("v_separation", 8);
        AddFact(facts, "BUILD", build.InformationalVersion, muted, primary);
        AddFact(facts, ".NET", RuntimeInformation.FrameworkDescription, muted, primary);
        AddFact(facts, "GODOT", Engine.GetVersionInfo()["string"].AsString(), muted, primary);
        AddFact(facts, "RULESETS", IsExpectedRegistry(rulesets) ? $"1 validated · {rulesets.Registry!.Digest.ToString()[..12]}…" : "validation failed", muted, IsExpectedRegistry(rulesets) ? accent : new Color("e06c75"));
        AddFact(facts, "SESSION", sessionReady ? $"{_snapshot!.Status} · tick {_snapshot.Tick}" : sessionDetail, muted, sessionReady ? accent : new Color("e06c75"));
        AddFact(facts, "STATE", sessionReady ? $"{_snapshot!.StateDigest.ToString()[..12]}… · no biological state" : "unavailable", muted, sessionReady ? accent : new Color("e06c75"));
        AddFact(facts, "STATUS", sessionReady ? "Paused — logical time is not advancing" : "session validation failed", muted, sessionReady ? accent : new Color("e06c75"));
        layout.AddChild(facts);

        _statusLabel = Label("Nonbiological foundation only: no cells, fields, regions, or biological world state exist.", 15, muted);
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
        RulesetDirectoryLoadResult rulesets = LoadRulesets();
        checks.Add(new DiagnosticCheck(
            "ruleset.registry",
            IsExpectedRegistry(rulesets) ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
            "Foundation reference ruleset",
            rulesets.Registry is null ? string.Join("; ", rulesets.Issues.Select(static issue => $"{issue.FileName}: {issue.Reason}")) : $"count={rulesets.Registry.Entries.Count};digest={rulesets.Registry.Digest}"));
        checks.Add(new DiagnosticCheck(
            "rng.algorithm",
            AlgorithmCatalog.Phase03.Digest.ToString() == FoundationRngSelfTest.ExpectedAlgorithmDigest ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
            "Phase 0.3 algorithm catalog",
            AlgorithmCatalog.Phase03.Digest.ToString()));
        checks.Add(new DiagnosticCheck(
            "rng.domains",
            RngDomainCatalog.Phase03.Digest.ToString() == FoundationRngSelfTest.ExpectedDomainDigest ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
            "Phase 0.3 RNG domain catalog",
            RngDomainCatalog.Phase03.Digest.ToString()));
        if (TryCreateSessionSnapshot(rulesets, out WorldSession? session, out SessionPresentationSnapshot? snapshot, out string sessionDetail)
            && session is not null && snapshot is not null)
        {
            string before = session.StateDigest.ToString();
            SessionPresentationSnapshot repeated = new SessionPresentationSnapshotProducer().Create(session);
            string after = session.StateDigest.ToString();
            checks.Add(new DiagnosticCheck(
                "session.definition",
                session.Definition.Digest.ToString() == FoundationSessionFixture.ExpectedDefinitionDigest ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
                "World-session definition",
                session.Definition.Digest.ToString()));
            checks.Add(new DiagnosticCheck(
                "session.scheduler",
                session.Definition.SchedulerGraphDigest.ToString() == FoundationSessionFixture.ExpectedSchedulerGraphDigest ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
                "Deterministic scheduler graph",
                session.Definition.SchedulerGraphDigest.ToString()));
            bool matches = snapshot.WorldId == session.Definition.WorldIdentity.WorldId
                && snapshot.BranchId == session.Definition.BranchIdentity.BranchId
                && snapshot.Tick == session.CurrentTick
                && snapshot.Status == session.Status;
            checks.Add(new DiagnosticCheck(
                "presentation.snapshot",
                matches ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
                "Immutable presentation snapshot",
                $"world={snapshot.WorldId};branch={snapshot.BranchId};tick={snapshot.Tick};status={snapshot.Status};definition={snapshot.SessionDefinitionDigest};state={snapshot.StateDigest}"));
            checks.Add(new DiagnosticCheck(
                "presentation.nonbiological",
                !snapshot.HasBiologicalState ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
                "Biological state declaration",
                $"hasBiologicalState={snapshot.HasBiologicalState.ToString().ToLowerInvariant()}"));
            checks.Add(new DiagnosticCheck(
                "presentation.no-mutation",
                before == after && repeated.StateDigest == snapshot.StateDigest ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
                "Snapshot creation preserves authoritative state",
                $"before={before};after={after}"));
            bool headless = !typeof(WorldSession).Assembly.GetReferencedAssemblies().Any(static item => item.Name?.StartsWith("Godot", StringComparison.Ordinal) == true)
                && !typeof(WorldSessionDefinition).Assembly.GetReferencedAssemblies().Any(static item => item.Name?.StartsWith("Godot", StringComparison.Ordinal) == true);
            checks.Add(new DiagnosticCheck(
                "session.core-headless",
                headless ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
                "Core assemblies are Godot-free",
                headless ? "Model and Simulation have no Godot reference." : "A core assembly references Godot."));
        }
        else
        {
            checks.Add(new DiagnosticCheck("session.fixture", DiagnosticSeverity.Failure, "Paused nonbiological session fixture", sessionDetail));
        }
        return foundation with { Success = checks.All(check => check.Severity != DiagnosticSeverity.Failure), Checks = checks };
    }

    private static RulesetDirectoryLoadResult LoadRulesets() => new RulesetDirectoryLoader().Load(ResolveRulesetDirectory());

    private static string ResolveRulesetDirectory()
    {
        string executable = OS.GetExecutablePath();
        if (string.Equals(Path.GetFileName(executable), "ProjectEmergence.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Path.GetDirectoryName(executable)!, "rulesets");
        return ProjectSettings.GlobalizePath("res://../../rulesets");
    }

    private static bool IsExpectedRegistry(RulesetDirectoryLoadResult result) =>
        result.Success && result.Registry?.Entries.Count == 1
        && result.Registry.Digest.ToString() == "0f04aa596563a6c706ad4177d7b48b19ea44f5ac62c1cd823203531568f33a4d";

    private static bool TryCreateSessionSnapshot(
        RulesetDirectoryLoadResult rulesets,
        out WorldSession? session,
        out SessionPresentationSnapshot? snapshot,
        out string detail)
    {
        session = null;
        snapshot = null;
        if (!IsExpectedRegistry(rulesets) || rulesets.Registry is null)
        {
            detail = "reference ruleset validation failed";
            return false;
        }
        try
        {
            session = FoundationSessionFixture.CreatePausedSession(rulesets.Registry);
            snapshot = new SessionPresentationSnapshotProducer().Create(session);
            detail = "paused nonbiological session ready";
            return snapshot.Status == WorldSessionStatus.Paused && snapshot.Tick.Value == UInt128.Zero && !snapshot.HasBiologicalState;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            detail = $"{exception.GetType().Name}: {exception.Message}";
            session = null;
            snapshot = null;
            return false;
        }
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
