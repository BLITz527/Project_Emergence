# Build, test, and package

Use `eng/build.ps1` for restore plus Debug/Release builds and assembly hashes. Use `eng/test.ps1` for all three test projects, TRX results, and XPlat coverage. Use `eng/doctor.ps1` for CLI evidence.

With Godot available, load the App headlessly and invoke user arguments after `--`:

```powershell
& $env:GODOT4 --headless --path .\src\Emergence.App --editor --quit
& $env:GODOT4 --headless --path .\src\Emergence.App -- --smoke-exit
& $env:GODOT4 --headless --path .\src\Emergence.App -- --doctor-json .\artifacts\app\doctor.json
```

`eng/package.ps1` refuses non-4.7/non-.NET editors or missing matching templates. `eng/verify-package.ps1` executes the packaged smoke and doctor modes before recording file hashes.
