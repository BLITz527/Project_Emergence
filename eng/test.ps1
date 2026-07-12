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
    'tests\Emergence.Cli.IntegrationTests\Emergence.Cli.IntegrationTests.csproj',
    'tests\Emergence.ReviewPack.Tests\Emergence.ReviewPack.Tests.csproj'
)
foreach ($relative in $projects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($relative)
    $resultsPath = [System.IO.Path]::GetFullPath((Join-Path $output $name))
    if (-not $resultsPath.StartsWith($output + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear test evidence outside the configured artifact root: $resultsPath"
    }
    if (Test-Path -LiteralPath $resultsPath) {
        Remove-Item -LiteralPath $resultsPath -Recurse -Force
    }
    $results = New-ArtifactDirectory $resultsPath
    $project = Join-Path $root $relative
    $trx = Join-Path $results ("{0}.trx" -f $name)
    $coverage = Join-Path $results 'coverage.cobertura.xml'
    $commandText = "dotnet test `"$project`" --configuration $Configuration --no-restore --no-build --logger `"trx;LogFileName=$name.trx`" --results-directory `"$results`" --collect `"XPlat Code Coverage`""
    $commandText | Out-File -FilePath (Join-Path $results 'command.txt') -Encoding utf8
    $Configuration | Out-File -FilePath (Join-Path $results 'configuration.txt') -Encoding ascii
    Invoke-LoggedCommand (Join-Path $results 'test.log') {
        dotnet test $project --configuration $Configuration --no-restore --no-build --logger ("trx;LogFileName={0}.trx" -f $name) --results-directory $results --collect 'XPlat Code Coverage'
    }
    if (-not (Test-Path -LiteralPath $trx -PathType Leaf)) {
        throw "Test command completed without the required TRX result: $trx"
    }
    $coverageCandidates = @(Get-ChildItem -LiteralPath $results -Filter 'coverage.cobertura.xml' -File -Recurse |
        Where-Object { $_.FullName -ne $coverage })
    if ($coverageCandidates.Count -lt 1) {
        throw "Expected generated coverage.cobertura.xml for $name; found none."
    }
    $coverageHashes = @($coverageCandidates | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Select-Object -Unique)
    if ($coverageHashes.Count -ne 1) {
        throw "Coverage collector produced $($coverageCandidates.Count) contradictory coverage files for $name."
    }
    Copy-Item -LiteralPath $coverageCandidates[0].FullName -Destination $coverage
    foreach ($directory in @(Get-ChildItem -LiteralPath $results -Directory)) {
        $resolvedDirectory = [System.IO.Path]::GetFullPath($directory.FullName)
        if (-not $resolvedDirectory.StartsWith($results + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected coverage directory: $resolvedDirectory"
        }
        Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force
    }
    if (-not (Test-Path -LiteralPath $coverage -PathType Leaf)) {
        throw "Normalized coverage evidence is missing: $coverage"
    }
}
