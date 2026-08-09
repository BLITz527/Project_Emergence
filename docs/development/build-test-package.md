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

`eng/package.ps1` requires matching `4.7.stable.mono` Windows x86_64 .NET export templates and copies exactly one adjacent reference ruleset. `eng/verify-package.ps1` validates the exact package inventory, source/package ruleset equivalence, required Model/Simulation/Persistence/Presentation assemblies, Phase 1.1 identity, version/framework/commit metadata, smoke, doctor, writable V2 save/load, all field chunks, an isolated stale-lock Save/Recover probe, and absence of save sidecars after success.

Phase 1.1 CLI evidence includes `environment-self-test`, `environment-performance`, `environment-package fixture|verify`, and `environment-probe`. Run the independent reference implementation with Python 3:

```powershell
python .\tools\reference\phase11_environment_vectors.py
python .\tools\reference\phase11_environment_vectors.py --package .\artifacts\cli\environment-session.emergence-world
```

App QA captures two fresh images: normal smooth view and `--raw-grid` debug view. Neither mode advances logical time.

The supported CLI package workflow is:

```powershell
dotnet run --project .\src\Emergence.Cli -- persistence-self-test --json .\artifacts\cli\persistence-self-test.json
dotnet run --project .\src\Emergence.Cli -- world-package fixture .\artifacts\cli\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package verify .\artifacts\cli\foundation-session.emergence-world
dotnet run --project .\src\Emergence.Cli -- world-package recover .\artifacts\cli\foundation-session.emergence-world
```
