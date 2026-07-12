# Toolchain Baseline Verification Note

Archive date: 2026-07-12

The Phase 0.1 prompt was prepared against the official current stable baseline available on the archive date:

- Godot 4.7 stable, including the Windows .NET edition, released 2026-06-18.
- .NET 10 LTS; SDK 10.0.301 was the current official SDK release listed on 2026-06-09.
- Godot's stable C# documentation requires the .NET-enabled editor and a compatible .NET SDK; Godot 4.7 documentation lists .NET SDK 8.0+ for .NET builds.

Official references:
- https://godotengine.org/download/windows/
- https://godotengine.org/download/archive/4.7-stable/
- https://dotnet.microsoft.com/en-us/download
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html
- https://docs.godotengine.org/en/stable/engine_details/development/compiling/compiling_with_dotnet.html

The implementation prompt requires a preflight and an explicit ADR rather than assuming target-framework compatibility.
