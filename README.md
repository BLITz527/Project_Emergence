# Project Emergence

Project Emergence is in **Milestone 0, Phase 0.2**. This phase establishes deterministic, strongly typed foundation values and their evidence pipeline. It does **not** contain a world state, cells, organisms, biological simulation, biological RNG, or fake-life animation.

## Requirements

- Windows x86_64
- .NET SDK 10.0.201 selected by `global.json`
- Godot 4.7 stable .NET and matching Windows x86_64 .NET export templates
- Git and PowerShell 5.1+

The immutable authoritative design archive is under `docs/design/v1.0/` with SHA-256 `915f013f26955e1c614bb851a39b83c6966951ee94b73ac13a06167b2ff5fb6c`.

## Build, test, and diagnose

```powershell
.\eng\preflight.ps1
.\eng\build.ps1
.\eng\test.ps1
.\eng\doctor.ps1
```

Direct CLI commands are `version`, `doctor`, the preserved Phase 0.1 `self-test`, and `domain-self-test`. The latter locks canonical encoding V1, algorithm-catalog, and configuration digests.

```powershell
dotnet run --project .\src\Emergence.Cli -- domain-self-test
```

With `$env:GODOT4` set to the Godot 4.7 .NET console executable, use `eng/verify-app.ps1`, `eng/package.ps1`, and `eng/verify-package.ps1`. The shell remains a nonbiological presentation host and displays `FOUNDATION / M0.2`.

## Repository map

- `src/Emergence.Foundation` — IDs, logical time, exact quantities, hashing, versions, immutable configuration, results, diagnostics, and self-tests
- `src/Emergence.Cli` — headless evidence commands
- `src/Emergence.App` — Godot presentation host only
- other `src/Emergence.*` projects — preserved dependency boundaries; Model remains marker-only
- `tests` — Foundation, architecture, CLI, and review-evidence tests
- `tools/Emergence.ReviewPack` — exact review snapshot and semantic verifier
- `eng` — repeatable build, test, diagnostics, App, package, and review workflows
- `docs` — active decisions and the immutable imported design baseline
