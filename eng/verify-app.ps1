param(
    [string]$GodotPath,
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\app' }
$output = New-ArtifactDirectory $ArtifactsRoot
$statusPath = Join-Path $output 'app-status.txt'
if (Test-Path -LiteralPath $statusPath) { Remove-Item -LiteralPath $statusPath -Force }
$godot = Resolve-GodotExecutable $GodotPath
if (-not $godot) {
    'SKIPPED/BLOCKED: Godot 4.7 stable .NET executable was not found. Source compilation is covered by the solution build; load, smoke, doctor, normal launch, and screenshot are not accepted.' |
        Out-File -FilePath $statusPath -Encoding utf8
    throw 'Godot App runtime verification is blocked: Godot 4.7 stable .NET executable was not found.'
}

$version = (& $godot --version 2>&1 | Out-String).Trim()
$version | Out-File -FilePath (Join-Path $output 'godot-version.txt') -Encoding utf8
if ($version -notmatch '^4\.7(\.0)?\.stable' -or $version -notmatch 'mono') {
    throw "Godot App verification requires Godot 4.7 stable .NET; selected '$version'."
}
$project = Join-Path $root 'src\Emergence.App'
Invoke-LoggedCommand (Join-Path $output 'load.log') { & $godot --headless --editor --path $project --quit }
Invoke-LoggedCommand (Join-Path $output 'smoke.log') { & $godot --headless --path $project -- --smoke-exit }
Invoke-LoggedCommand (Join-Path $output 'doctor.log') { & $godot --headless --path $project -- --doctor-json (Join-Path $output 'doctor.json') }
'PASSED: Phase 1.1 headless project load, main-scene smoke, environment field diagnostics, V2 save/load, chunks, and static-tick checks exited 0. Normal/raw-grid screenshots are recorded separately.' |
    Out-File -FilePath $statusPath -Encoding utf8
