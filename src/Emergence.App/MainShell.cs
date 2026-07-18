using System.Runtime.InteropServices;
using Emergence.Foundation;
using Emergence.Foundation.Randomness;
using Emergence.Foundation.Results;
using Emergence.Foundation.Versioning;
using Emergence.Model;
using Emergence.Persistence.Rulesets;
using Emergence.Persistence.WorldPackages;
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
    private Label? _packageStatus;
    private string _savePath = string.Empty;

    public override void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        if (TryRunCommandLineMode(arguments))
        {
            return;
        }

        BuildShell();
        if (arguments.Contains("--save-load-qa", StringComparer.Ordinal))
        {
            SaveSession();
            VerifySave();
            LoadSession();
        }
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
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        VBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 9);
        margin.AddChild(layout);

        Label eyebrow = Label("FOUNDATION / M0.5", 14, accent);
        layout.AddChild(eyebrow);
        Label title = Label("Project Emergence", 42, primary);
        layout.AddChild(title);
        Label subtitle = Label("Milestone 0 — Coherent Snapshots and Save/Load", 20, muted);
        layout.AddChild(subtitle);

        HSeparator separator = new();
        separator.CustomMinimumSize = new Vector2(0, 14);
        layout.AddChild(separator);

        BuildDetails build = BuildInfo.Current;
        RulesetDirectoryLoadResult rulesets = LoadRulesets();
        bool sessionReady = TryCreateSessionSnapshot(rulesets, out _session, out _snapshot, out string sessionDetail);
        _savePath = ProjectSettings.GlobalizePath("user://saves/foundation-session.emergence-world");
        Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);
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
        AddFact(facts, "SAVE", "user://saves/foundation-session.emergence-world", muted, primary);
        layout.AddChild(facts);

        _statusLabel = Label("Nonbiological foundation only: no cells, fields, regions, or biological world state exist.", 15, muted);
        _statusLabel.CustomMinimumSize = new Vector2(0, 32);
        layout.AddChild(_statusLabel);

        _packageStatus = Label("SAVE/LOAD  Ready — no background simulation while closed.", 14, accent);
        _packageStatus.CustomMinimumSize = new Vector2(0, 24);
        layout.AddChild(_packageStatus);

        _diagnostics = new RichTextLabel
        {
            FitContent = true,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(0, 70),
            BbcodeEnabled = true,
            Text = "[color=#8aa3ae]Diagnostics have not been run.[/color]",
        };
        layout.AddChild(_diagnostics);

        HBoxContainer actions = new();
        actions.AddThemeConstantOverride("separation", 12);
        Button diagnosticsButton = new() { Text = "Run Diagnostics" };
        diagnosticsButton.Pressed += ShowDiagnostics;
        actions.AddChild(diagnosticsButton);
        Button saveButton = new() { Text = "Save Session", Disabled = !sessionReady };
        saveButton.Pressed += SaveSession;
        actions.AddChild(saveButton);
        Button loadButton = new() { Text = "Load Session" };
        loadButton.Pressed += LoadSession;
        actions.AddChild(loadButton);
        Button verifyButton = new() { Text = "Verify Save" };
        verifyButton.Pressed += VerifySave;
        actions.AddChild(verifyButton);
        Button quitButton = new() { Text = "Quit" };
        quitButton.Pressed += () => GetTree().Quit();
        actions.AddChild(quitButton);
        layout.AddChild(actions);
    }

    private void SaveSession()
    {
        if (_session is null || _session.Status is not (WorldSessionStatus.Paused or WorldSessionStatus.Faulted))
        {
            SetPackageStatus("Save unavailable: session must be Paused or Faulted.", false);
            return;
        }
        OperationResult<WorldSessionSnapshot> capture = _session.CaptureSnapshot();
        if (!capture.Success)
        {
            SetPackageStatus("Save failed during coherent snapshot capture.", false, capture.Issues.Select(static issue => issue.Detail));
            return;
        }
        WorldPackageSaveResult save = new WorldPackageWriter().Save(_savePath, capture.Value);
        if (!save.Success)
        {
            SetPackageStatus("Save failed; the prior package remains authoritative or recoverable.", false, save.Issues.Select(static issue => issue.Detail));
            return;
        }
        SetPackageStatus($"Save verified · state {capture.Value.StateDigest.ToString()[..12]}… · package {save.PackageIdentityDigest[..12]}…", true);
    }

    private void LoadSession()
    {
        WorldPackageLoadResult load = new WorldPackageReader().Load(_savePath);
        if (!load.Success || load.Document is null)
        {
            SetPackageStatus("Load failed; the current session is unchanged.", false, load.Issues.Select(static issue => issue.Detail));
            return;
        }
        CommandProcessorRegistry processors = FoundationSessionFixture.CreateCommandProcessorRegistry();
        IReadOnlyList<ISimulationSystem> systems = FoundationSessionFixture.CreateSystems();
        OperationResult compatibility = SessionCompatibilityValidator.Validate(load.Document.Snapshot, systems, processors);
        if (!compatibility.Success)
        {
            SetPackageStatus("Load is incompatible; the current session is unchanged.", false, compatibility.Issues.Select(static issue => issue.Detail));
            return;
        }
        OperationResult<WorldSession> restoration = WorldSession.Restore(load.Document.Snapshot, systems, processors);
        if (!restoration.Success)
        {
            SetPackageStatus("Restore failed; the current session is unchanged.", false, restoration.Issues.Select(static issue => issue.Detail));
            return;
        }

        WorldSession candidate = restoration.Value;
        SessionPresentationSnapshot candidateSnapshot = new SessionPresentationSnapshotProducer().Create(candidate);
        _session = candidate;
        _snapshot = candidateSnapshot;
        SetPackageStatus($"Loaded {candidate.Status} state · tick {candidate.CurrentTick} · {candidate.StateDigest.ToString()[..12]}…", true);
    }

    private void VerifySave()
    {
        WorldPackageLoadResult load = new WorldPackageReader().Load(_savePath);
        if (!load.Success || load.Document is null)
        {
            SetPackageStatus("Save verification failed.", false, load.Issues.Select(static issue => issue.Detail));
            return;
        }
        SetPackageStatus($"Save verified · state {load.Document.Snapshot.StateDigest.ToString()[..12]}… · no biological state", true);
    }

    private void SetPackageStatus(string message, bool success, IEnumerable<string>? details = null)
    {
        if (_packageStatus is not null)
        {
            _packageStatus.Text = "SAVE/LOAD  " + message;
            _packageStatus.AddThemeColorOverride("font_color", success ? new Color("54d6b0") : new Color("e06c75"));
        }
        if (_statusLabel is not null) _statusLabel.Text = message;
        if (_diagnostics is not null && details is not null)
            _diagnostics.Text = string.Join("\n", details.Select(static detail => $"[color=#e06c75]Detail[/color]  {detail}"));
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
            "phase.identity",
            DiagnosticSeverity.Success,
            "Persistence evidence phase",
            "M0 Phase 0.5"));
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
                session.Definition.Digest.ToString() == FoundationSessionFixture.ExpectedPhase05DefinitionDigest ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure,
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
            AddPersistenceDiagnostics(checks, session);
        }
        else
        {
            checks.Add(new DiagnosticCheck("session.fixture", DiagnosticSeverity.Failure, "Paused nonbiological session fixture", sessionDetail));
        }
        return foundation with { Success = checks.All(check => check.Severity != DiagnosticSeverity.Failure), Checks = checks };
    }

    private static void AddPersistenceDiagnostics(List<DiagnosticCheck> checks, WorldSession session)
    {
        string directory = ProjectSettings.GlobalizePath("user://diagnostics");
        string packagePath = Path.Combine(directory, "app-doctor.emergence-world");
        Directory.CreateDirectory(directory);
        CleanupPackageArtifacts(packagePath);
        bool success = false;
        bool rngMatch = false;
        bool sidecarsClean;
        string detail;
        try
        {
            OperationResult<WorldSessionSnapshot> capture = session.CaptureSnapshot();
            if (!capture.Success) throw new InvalidOperationException("App doctor snapshot capture failed.");
            RngSampleAddress address = new(new("foundation.self-test"), RngScopeKey.Parse(FoundationRngSelfTest.Scope), 42);
            string before = new DeterministicAddressedRng(session.Definition.RootSeed, session.Definition.SelectedRuleset.RngDomains).GenerateBlock(address).ToString();
            WorldPackageSaveResult save = new WorldPackageWriter().Save(packagePath, capture.Value);
            if (!save.Success) throw new InvalidOperationException(save.Issues[0].Detail);
            WorldPackageLoadResult load = new WorldPackageReader().Load(packagePath);
            if (!load.Success || load.Document is null) throw new InvalidOperationException(load.Issues[0].Detail);
            CommandProcessorRegistry processors = FoundationSessionFixture.CreateCommandProcessorRegistry();
            OperationResult<WorldSession> restored = WorldSession.Restore(load.Document.Snapshot, FoundationSessionFixture.CreateSystems(), processors);
            if (!restored.Success) throw new InvalidOperationException(restored.Issues[0].Detail);
            string after = new DeterministicAddressedRng(restored.Value.Definition.RootSeed, restored.Value.Definition.SelectedRuleset.RngDomains).GenerateBlock(address).ToString();
            rngMatch = before == after;
            success = restored.Value.StateDigest == session.StateDigest && rngMatch && !load.Document.Snapshot.FaultIssues.Any();
            detail = $"state={restored.Value.StateDigest};package={load.Document.Manifest.PackageIdentityDigest}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            detail = $"{exception.GetType().Name}: {exception.Message}";
        }
        finally
        {
            CleanupPackageArtifacts(packagePath);
            sidecarsClean = PackageArtifactPaths(packagePath).All(path => !File.Exists(path));
        }
        checks.Add(new DiagnosticCheck("persistence.round-trip", success ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, "App save/load round trip", detail));
        checks.Add(new DiagnosticCheck("persistence.rng-continuation", rngMatch ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, "App addressed RNG continuation", rngMatch.ToString().ToLowerInvariant()));
        checks.Add(new DiagnosticCheck("persistence.sidecars", sidecarsClean ? DiagnosticSeverity.Success : DiagnosticSeverity.Failure, "App temporary sidecar cleanup", sidecarsClean ? "none" : "sidecar remains"));
    }

    private static void CleanupPackageArtifacts(string target)
    {
        foreach (string path in PackageArtifactPaths(target))
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static IEnumerable<string> PackageArtifactPaths(string target) =>
        [target, target + ".writing", target + ".previous", target + ".lock", target + ".corrupt"];

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
            session = FoundationSessionFixture.CreatePhase05PausedSession(rulesets.Registry);
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
