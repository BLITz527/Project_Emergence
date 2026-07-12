param(
    [string]$GodotPath,
    [string]$OutputDirectory,
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\package' }
$evidence = New-ArtifactDirectory $ArtifactsRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $evidence 'windows-x86_64' }
$package = New-ArtifactDirectory $OutputDirectory
$godot = Resolve-GodotExecutable $GodotPath
if (-not $godot) {
    'BLOCKED: Godot 4.7 .NET executable was not found.' | Out-File -FilePath (Join-Path $evidence 'package-status.txt') -Encoding utf8
    Write-Error 'Packaging blocked: Godot 4.7 .NET executable was not found.'
}
$version = (& $godot --version 2>&1 | Out-String).Trim()
if ($version -notmatch '^4\.7(\.0)?\.stable' -or $version -notmatch 'mono') {
    throw "Packaging requires Godot 4.7 stable .NET; selected version is '$version'."
}
$templateVersion = ($version -split '\.mono')[0]
$templateRoot = Join-Path $env:APPDATA ("Godot\export_templates\{0}" -f $templateVersion)
if (-not (Test-Path -LiteralPath $templateRoot -PathType Container)) {
    "BLOCKED: matching templates missing at $templateRoot" | Out-File -FilePath (Join-Path $evidence 'package-status.txt') -Encoding utf8
    throw "Matching Godot export templates are unavailable at $templateRoot"
}
$executable = Join-Path $package 'ProjectEmergence.exe'
Invoke-LoggedCommand (Join-Path $evidence 'export.log') { & $godot --headless --path (Join-Path $root 'src\Emergence.App') --export-release 'Windows Desktop x86_64' $executable }
& (Join-Path $PSScriptRoot 'verify-package.ps1') -PackageDirectory $package -ArtifactsRoot $evidence
