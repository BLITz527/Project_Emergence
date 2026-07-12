# Windows setup

1. Install a stable .NET 10 SDK compatible with `global.json`.
2. Install Git and PowerShell 5.1 or newer.
3. For the desktop host, install Godot 4.7 stable .NET x86_64 and matching Windows export templates.
4. Point `GODOT4` at the .NET-enabled executable or pass `-GodotPath` to scripts.
5. Run `eng/preflight.ps1` and inspect `artifacts/preflight/preflight.json`.

Scripts do not change global Git, PowerShell, .NET, or Godot settings. Missing Godot/templates block only engine runtime and package acceptance, not headless implementation.
