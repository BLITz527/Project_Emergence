# Project Emergence

Project Emergence is in **Milestone 0, Phase 0.5**. The nonbiological foundation now supports coherent Paused/Faulted session snapshots, strict V2 session definitions, atomic `.emergence-world` save/load packages, deterministic recovery, and exact continuation of logical time, counters, pending commands, fault state, ruleset/algorithm identity, and addressed RNG inputs.

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

All prior CLI evidence remains. Phase 0.5 adds `persistence-self-test` and `world-package fixture|verify|recover`.

```powershell
dotnet run --project .\src\Emergence.Cli -- persistence-self-test
dotnet run --project .\src\Emergence.Cli -- world-package fixture .\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package verify .\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package recover .\foundation-session.emergence-world
```

With `$env:GODOT4` set to the Godot 4.7 .NET console executable, use `eng/verify-app.ps1`, `eng/capture-app-screenshot.ps1`, `eng/package.ps1`, and `eng/verify-package.ps1`. The shell displays `FOUNDATION / M0.5`, saves to `user://saves/foundation-session.emergence-world`, and never advances simulation in a frame callback or while closed.

## Repository map

- `src/Emergence.Foundation` - primitives, strict UTF-8, canonical hashing, addressed RNG, rulesets, diagnostics, and catalogs
- `src/Emergence.Model` - immutable session definitions, snapshots, scheduler, commands, events, and receipts
- `src/Emergence.Simulation` - single-owner session execution, coherent capture, compatibility, and restore
- `src/Emergence.Persistence` - bounded ruleset loading and strict atomic world packages
- `src/Emergence.Presentation.Contracts` - immutable non-Godot presentation DTOs
- `src/Emergence.Cli` - headless evidence and package commands
- `src/Emergence.App` - Godot presentation/save-load host
- `tests` - eight focused test projects
- `tools/Emergence.ReviewPack` - exact review snapshot and independent semantic verifier
- `eng` - repeatable build, test, diagnostics, App, package, and review workflows
- `docs` - active decisions and the immutable imported design baseline
