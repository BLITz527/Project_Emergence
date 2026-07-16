# Project Emergence

Project Emergence is in **Milestone 0, Phase 0.4**. This phase establishes the first authoritative in-memory world session, deterministic single-threaded phase scheduler, bounded command intake, atomic event commitment, and immutable presentation-snapshot boundary. The fixture session is deliberately nonbiological: it contains no cells, fields, regions, organisms, or fake-life animation.

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

Direct CLI commands are `version`, `doctor`, the preserved `self-test`, `domain-self-test`, `rng-self-test`, and `ruleset validate --directory <path>`, plus the Phase 0.4 `session-self-test`. These commands lock all prior vectors and the scheduler/session/event/state vectors.

```powershell
dotnet run --project .\src\Emergence.Cli -- rng-self-test
dotnet run --project .\src\Emergence.Cli -- ruleset validate --directory .\rulesets
dotnet run --project .\src\Emergence.Cli -- session-self-test
```

With `$env:GODOT4` set to the Godot 4.7 .NET console executable, use `eng/verify-app.ps1`, `eng/package.ps1`, and `eng/verify-package.ps1`. The shell consumes an actual immutable paused-at-tick-zero snapshot and displays `FOUNDATION / M0.4`; it never advances logical time from rendering or frame callbacks.

## Repository map

- `src/Emergence.Foundation` — immutable primitives, canonical hashing, addressed RNG, rulesets, diagnostics, and algorithm catalogs
- `src/Emergence.Model` — immutable session, scheduler-graph, command, event, and receipt contracts
- `src/Emergence.Simulation` — mutable single-owner world-session execution and snapshot production
- `src/Emergence.Presentation.Contracts` — immutable, non-Godot presentation DTOs
- `src/Emergence.Persistence` — bounded untrusted ruleset-directory loading; no session save format
- `src/Emergence.Cli` — headless evidence commands
- `src/Emergence.App` — Godot presentation host only
- `tests` — eight focused test projects, including Model, Simulation, and Presentation contracts
- `tools/Emergence.ReviewPack` — exact review snapshot and independent semantic verifier
- `eng` — repeatable build, test, diagnostics, App, package, and review workflows
- `docs` — active decisions and the immutable imported design baseline
