param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$package = [System.IO.Path]::GetFullPath($PackageDirectory)
if (-not $ArtifactsRoot) { $ArtifactsRoot = $package }
$evidence = New-ArtifactDirectory $ArtifactsRoot
$executable = Join-Path $package 'ProjectEmergence.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Expected executable is missing: $executable" }

Invoke-LoggedCommand (Join-Path $evidence 'packaged-smoke.log') { & $executable -- --smoke-exit }
Invoke-LoggedCommand (Join-Path $evidence 'packaged-doctor.log') { & $executable -- --doctor-json (Join-Path $evidence 'packaged-doctor.json') }
Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName |
    Select-Object @{Name='Path';Expression={$_.FullName.Substring($package.Length + 1).Replace('\','/')}}, Length, @{Name='Sha256';Expression={(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}} |
    ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $evidence 'package-manifest.json') -Encoding utf8
'PASSED: packaged smoke and diagnostics exited 0.' | Out-File -FilePath (Join-Path $evidence 'package-status.txt') -Encoding utf8
