param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
$package = [System.IO.Path]::GetFullPath($PackageDirectory)
if (-not $ArtifactsRoot) { $ArtifactsRoot = $package }
$evidence = New-ArtifactDirectory $ArtifactsRoot
$executable = Join-Path $package 'ProjectEmergence.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Expected executable is missing: $executable" }
$rulesetFiles = @(Get-ChildItem -LiteralPath $package -File -Recurse | Where-Object { $_.Name.EndsWith('.ruleset.json', [StringComparison]::Ordinal) })
if ($rulesetFiles.Count -ne 1 -or $rulesetFiles[0].FullName -ne (Join-Path $package 'rulesets\foundation-reference.ruleset.json')) {
    throw 'Package must contain exactly one rulesets/foundation-reference.ruleset.json.'
}
$sourceRuleset = Join-Path $root 'rulesets\foundation-reference.ruleset.json'
if ((Get-FileHash -LiteralPath $sourceRuleset -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $rulesetFiles[0].FullName -Algorithm SHA256).Hash) {
    throw 'Packaged reference ruleset is not byte-equivalent to the tracked source ruleset.'
}
$managedRoot = Join-Path $package 'data_Emergence.App_windows_x86_64'
foreach ($assembly in @('Emergence.Model.dll', 'Emergence.Simulation.dll', 'Emergence.Presentation.Contracts.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $managedRoot $assembly) -PathType Leaf)) { throw "Required packaged assembly is missing: $assembly" }
}

function Invoke-PackagedMode {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )
    $stdout = Join-Path $evidence ("{0}.stdout.log" -f $Name)
    $stderr = Join-Path $evidence ("{0}.stderr.log" -f $Name)
    $combined = Join-Path $evidence ("{0}.log" -f $Name)
    foreach ($path in @($stdout, $stderr, $combined)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    $process = Start-Process -FilePath $executable -ArgumentList $Arguments -WorkingDirectory $package -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $output = @()
    if (Test-Path -LiteralPath $stdout) { $output += Get-Content -LiteralPath $stdout }
    if (Test-Path -LiteralPath $stderr) { $output += Get-Content -LiteralPath $stderr }
    $output | Out-File -FilePath $combined -Encoding utf8
    if ($process.ExitCode -ne 0) { throw "$Name failed with exit code $($process.ExitCode). See $combined" }
}

$doctorJson = Join-Path $evidence 'packaged-doctor.json'
if (Test-Path -LiteralPath $doctorJson) { Remove-Item -LiteralPath $doctorJson -Force }
Invoke-PackagedMode -Name 'packaged-smoke' -Arguments @('--headless', '--', '--smoke-exit')
Invoke-PackagedMode -Name 'packaged-doctor' -Arguments @('--headless', '--', '--doctor-json', $doctorJson)
if (-not (Test-Path -LiteralPath $doctorJson -PathType Leaf)) { throw 'Packaged doctor did not produce JSON evidence.' }
$doctorResult = Get-Content -LiteralPath $doctorJson -Raw | ConvertFrom-Json
if (-not $doctorResult.success) { throw 'Packaged doctor JSON reports failure.' }
$requiredSessionChecks = @('session.definition', 'session.scheduler', 'presentation.snapshot', 'presentation.nonbiological', 'presentation.no-mutation', 'session.core-headless')
foreach ($checkId in $requiredSessionChecks) {
    $check = @($doctorResult.checks | Where-Object { $_.id -eq $checkId })
    if ($check.Count -ne 1 -or $check[0].severity -ne 'Success') { throw "Packaged doctor is missing successful Phase 0.4 check: $checkId" }
}
Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName |
    Select-Object @{Name='Path';Expression={$_.FullName.Substring($package.Length + 1).Replace('\','/')}}, Length, @{Name='Sha256';Expression={(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}} |
    ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $evidence 'package-manifest.json') -Encoding utf8
'PASSED: packaged smoke and diagnostics exited 0.' | Out-File -FilePath (Join-Path $evidence 'package-status.txt') -Encoding utf8
