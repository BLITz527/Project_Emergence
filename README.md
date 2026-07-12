# Project Emergence

Project Emergence is in **Milestone 0, Phase 0.1**: repository, toolchain, diagnostics, and application-shell foundations. This repository does **not** yet contain cells, worlds, biological simulation, persistence of biological state, or fake-life animation.

## Requirements

- Windows x86_64
- .NET SDK 10.0.201 or a compatible 10.0 patch selected by `global.json`
- Git
- Godot 4.7 stable .NET editor and matching Windows export templates for App runtime and packaging checks
- Windows PowerShell 5.1 or PowerShell 7+

The authoritative design baseline is imported under `docs/design/v1.0/`. Its raw external source remains ignored at `design-input/Project_Emergence_Design_v1.0.zip`; see `docs/design/README.md` for the verified SHA-256.

## Build and test

```powershell
.\eng\preflight.ps1
dotnet restore .\ProjectEmergence.slnx
.\eng\build.ps1
.\eng\test.ps1
```

Run the headless diagnostics:

```powershell
dotnet run --project .\src\Emergence.Cli -- version
dotnet run --project .\src\Emergence.Cli -- doctor --json .\artifacts\cli\doctor.json
dotnet run --project .\src\Emergence.Cli -- self-test --json .\artifacts\cli\self-test.json
```

Launch the source shell with a matching Godot .NET editor:

```powershell
& $env:GODOT4 --path .\src\Emergence.App --editor
& $env:GODOT4 --path .\src\Emergence.App
```

Package and review:

```powershell
.\eng\package.ps1 -GodotPath $env:GODOT4
.\eng\review-pack.ps1
```

## Repository map

- `src/Emergence.Foundation` — build metadata, structured diagnostics, and runtime self-test
- `src/Emergence.Cli` — headless `version`, `doctor`, and `self-test` entry point
- `src/Emergence.App` — Godot .NET presentation host only
- other `src/Emergence.*` libraries — intentionally minimal future dependency boundaries
- `tests` — Foundation, architecture, and CLI integration tests
- `tools/Emergence.ReviewPack` — review snapshot and manifest generator/verifier
- `eng` — repeatable PowerShell engineering entry points
- `docs` — decisions, setup, workflow, and scope
- `rulesets` — reserved nonbiological configuration boundary; no simulation rules exist yet
