# Project Emergence

Project Emergence is in **Milestone 1, Phase 1.1**. The runtime now owns one static, nonbiological 16×12 environmental region with three exact conserved-material fields, real zero-volume solid boundaries, deterministic probes and conservation audits, smooth Godot visualization, and complete V2 environment packages. All accepted Milestone 0 session, RNG, V1-package, recovery, and crash-recoverable lock vectors remain regression contracts.

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

All prior CLI evidence remains. Phase 1.1 adds deterministic environment vectors, an independently reproducible field-chunk format, authoritative probes, V2 fixtures, and informational dense-storage/performance evidence.

```powershell
dotnet run --project .\src\Emergence.Cli -- persistence-self-test
dotnet run --project .\src\Emergence.Cli -- world-package fixture .\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package verify .\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package recover .\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- environment-self-test
dotnet run --project .\src\Emergence.Cli -- environment-package fixture .\environment-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- environment-package verify .\environment-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- environment-probe .\environment-session.emergence-world 00000000000000000000000000000064 8 6 matter.energy-substrate
dotnet run --project .\src\Emergence.Cli -- environment-performance
```

With `$env:GODOT4` set to the Godot 4.7 stable .NET console executable, use `eng/verify-app.ps1`, `eng/capture-app-screenshot.ps1`, `eng/package.ps1`, and `eng/verify-package.ps1`. Normal view presents a smooth interpolated surface with no grid; the optional `DEBUG / AUTHORITATIVE SAMPLES` overlay reveals exact cell boundaries. Click probes are exact, channel selection is presentation-only, saves use `user://saves/environment-session.emergence-world`, and no frame callback advances simulation.

The `.lock` path is only a rendezvous. Live exclusive OS-handle ownership is authoritative; an ordinary stale lock file is immediately reacquirable without age, timestamp, PID, or metadata decisions. Active contention fails closed with no package/sidecar mutation. Lease cleanup is ownership-safe, and a fully promoted and validated package remains committed if cleanup reports a nonfatal warning.

## Repository map

- `src/Emergence.Foundation` - primitives, strict UTF-8, canonical hashing, addressed RNG, rulesets, diagnostics, and catalogs
- `src/Emergence.Model` - immutable session/environment definitions and coherent snapshots
- `src/Emergence.Simulation` - single-owner session execution and dense authoritative field arrays
- `src/Emergence.Persistence` - bounded ruleset loading, exact field chunks, and strict V1/V2 atomic world packages
- `src/Emergence.Presentation.Contracts` - immutable non-Godot session and field-surface DTOs
- `src/Emergence.Cli` - headless evidence and package commands
- `src/Emergence.App` - Godot presentation/save-load host
- `tests` - eight focused test projects
- `tools/Emergence.ReviewPack` - exact review snapshot and independent semantic verifier
- `eng` - repeatable build, test, diagnostics, App, package, and review workflows
- `docs` - active decisions and the immutable imported design baseline
