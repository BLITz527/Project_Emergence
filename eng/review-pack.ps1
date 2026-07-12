param(
    [string]$OutputRoot = 'C:\Dev\ReviewPacks\ProjectEmergence'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
$tool = Join-Path $root 'tools\Emergence.ReviewPack\Emergence.ReviewPack.csproj'
$output = & dotnet run --project $tool --configuration Release --no-build -- create $root $OutputRoot
if ($LASTEXITCODE -ne 0) { throw "Review-pack creation failed with exit code $LASTEXITCODE." }
$reviewDirectory = ($output | Select-Object -Last 1).Trim()
$manifest = Join-Path $reviewDirectory 'MANIFEST.json'
& dotnet run --project $tool --configuration Release --no-build -- verify $manifest
if ($LASTEXITCODE -ne 0) { throw "Review-pack manifest verification failed with exit code $LASTEXITCODE." }
Write-Host "Review pack: $reviewDirectory"
