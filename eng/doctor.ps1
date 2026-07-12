param([string]$ArtifactsRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\cli' }
$output = New-ArtifactDirectory $ArtifactsRoot
$cli = Join-Path $root 'src\Emergence.Cli\Emergence.Cli.csproj'

Invoke-LoggedCommand (Join-Path $output 'version.txt') { dotnet run --project $cli --configuration Release --no-build -- version }
Invoke-LoggedCommand (Join-Path $output 'doctor.log') { dotnet run --project $cli --configuration Release --no-build -- doctor --json (Join-Path $output 'doctor.json') }
Invoke-LoggedCommand (Join-Path $output 'self-test.log') { dotnet run --project $cli --configuration Release --no-build -- self-test --json (Join-Path $output 'self-test.json') }
Invoke-LoggedCommand (Join-Path $output 'domain-self-test.log') { dotnet run --project $cli --configuration Release --no-build -- domain-self-test --json (Join-Path $output 'domain-self-test.json') }
