# Build, test, and package

`eng/build.ps1` restores and builds Debug/Release with the current Git SHA embedded, requires zero warnings/errors, and emits an assembly inventory. `eng/test.ps1` runs all eight test projects once and leaves one TRX plus one normalized `coverage.cobertura.xml` per project. `eng/doctor.ps1` records all prior vectors plus `persistence-self-test`, deterministic stale-lock/active-contention/reacquisition checks, a deterministic fixture package, package verification, and recovery.

```powershell
.\eng\preflight.ps1
.\eng\build.ps1
.\eng\test.ps1
.\eng\doctor.ps1
```

Godot source checks use the 4.7 stable .NET console executable. Screenshot capture launches the normal shell with a real save/verify/load QA pass before capture.

```powershell
& $env:GODOT4 --headless --path .\src\Emergence.App --editor --quit
& $env:GODOT4 --headless --path .\src\Emergence.App -- --smoke-exit
& $env:GODOT4 --headless --path .\src\Emergence.App -- --doctor-json .\artifacts\app\doctor.json
.\eng\capture-app-screenshot.ps1 -GodotPath $env:GODOT4
```

`eng/package.ps1` requires matching `4.7.stable.mono` Windows x86_64 .NET export templates and copies exactly one adjacent reference ruleset. `eng/verify-package.ps1` validates the exact package inventory, source/package ruleset equivalence, required Model/Simulation/Persistence/Presentation assemblies, M0.5R identity, version/framework/commit metadata, smoke, doctor, writable save/load round trip, addressed RNG continuation, an isolated stale-lock Save/Recover probe, and absence of save sidecars after success.

The supported CLI package workflow is:

```powershell
dotnet run --project .\src\Emergence.Cli -- persistence-self-test --json .\artifacts\cli\persistence-self-test.json
dotnet run --project .\src\Emergence.Cli -- world-package fixture .\artifacts\cli\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package verify .\artifacts\cli\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package recover .\artifacts\cli\foundation-session.emergence-world
```
