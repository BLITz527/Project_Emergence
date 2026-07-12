param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\tests' }
$output = New-ArtifactDirectory $ArtifactsRoot

$projects = @(
    'tests\Emergence.Foundation.Tests\Emergence.Foundation.Tests.csproj',
    'tests\Emergence.Architecture.Tests\Emergence.Architecture.Tests.csproj',
    'tests\Emergence.Cli.IntegrationTests\Emergence.Cli.IntegrationTests.csproj'
)
foreach ($relative in $projects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($relative)
    $results = New-ArtifactDirectory (Join-Path $output $name)
    Invoke-LoggedCommand (Join-Path $results 'test.log') {
        dotnet test (Join-Path $root $relative) --configuration $Configuration --no-restore --no-build --logger ("trx;LogFileName={0}.trx" -f $name) --results-directory $results --collect 'XPlat Code Coverage'
    }
}
