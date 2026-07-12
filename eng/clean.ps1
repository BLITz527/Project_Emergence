param([switch]$IncludePreflight)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot

$generated = @()
$generated += Get-ChildItem -LiteralPath $root -Directory -Recurse -Force | Where-Object { $_.Name -in @('bin', 'obj', '.godot', 'TestResults') }
$artifactRoot = Join-Path $root 'artifacts'
if (Test-Path -LiteralPath $artifactRoot) {
    $generated += Get-ChildItem -LiteralPath $artifactRoot -Directory | Where-Object { $IncludePreflight -or $_.Name -ne 'preflight' }
}

foreach ($item in $generated | Sort-Object FullName -Descending -Unique) {
    $resolved = [System.IO.Path]::GetFullPath($item.FullName)
    if (-not $resolved.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside repository: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Host 'Removed repository-generated build, test, Godot import, and artifact outputs only.'
