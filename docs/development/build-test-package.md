# Build, test, and package

`eng/build.ps1` restores and builds Debug/Release with the current Git SHA embedded, requires zero warnings/errors, and emits a structured assembly inventory. `eng/test.ps1` runs every solution test project once and leaves one TRX plus one normalized `coverage.cobertura.xml` per project. `eng/doctor.ps1` records version, doctor, Phase 0.1 self-test, and Phase 0.2 domain-self-test evidence.

```powershell
.\eng\preflight.ps1
.\eng\build.ps1
.\eng\test.ps1
.\eng\doctor.ps1
```

Godot source checks use the 4.7 stable .NET console executable:

```powershell
& $env:GODOT4 --headless --path .\src\Emergence.App --editor --quit
& $env:GODOT4 --headless --path .\src\Emergence.App -- --smoke-exit
& $env:GODOT4 --headless --path .\src\Emergence.App -- --doctor-json .\artifacts\app\doctor.json
.\eng\capture-app-screenshot.ps1 -GodotPath $env:GODOT4
```

`eng/package.ps1` requires matching `4.7.stable.mono` export templates. `eng/verify-package.ps1` validates exact package inventory, smoke, doctor, packaged layout, framework, version, and reviewed commit.
