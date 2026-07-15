# Build, test, and package

`eng/build.ps1` restores and builds Debug/Release with the current Git SHA embedded, requires zero warnings/errors, and emits a structured assembly inventory. `eng/test.ps1` runs all five solution test projects once and leaves one TRX plus one normalized `coverage.cobertura.xml` per project. `eng/doctor.ps1` records version, doctor, the preserved Phase 0.1/0.2 self-tests, Phase 0.3 RNG vectors, and repository ruleset validation.

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

`eng/package.ps1` requires matching `4.7.stable.mono` export templates and copies exactly one adjacent `rulesets/foundation-reference.ruleset.json`. `eng/verify-package.ps1` validates the exact package inventory, ruleset location, smoke, doctor, packaged layout, framework, version, and reviewed commit.
