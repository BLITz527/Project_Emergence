param([Parameter(Mandatory = $true)][string]$ManifestPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
dotnet run --project (Join-Path $root 'tools\Emergence.ReviewPack\Emergence.ReviewPack.csproj') --configuration Release --no-build -- verify $ManifestPath
exit $LASTEXITCODE
