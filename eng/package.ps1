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
foreach ($name in @('export.log', 'package-status.txt', 'package-manifest.json', 'packaged-doctor.json', 'packaged-smoke.log', 'packaged-doctor.log')) {
    $staleEvidence = Join-Path $evidence $name
    if (Test-Path -LiteralPath $staleEvidence) { Remove-Item -LiteralPath $staleEvidence -Force }
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $evidence 'windows-x86_64' }
$fullOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $fullOutput) {
    if (-not $fullOutput.StartsWith($evidence + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear package output outside the package evidence directory: $fullOutput"
    }
    Remove-Item -LiteralPath $fullOutput -Recurse -Force
}
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
$templateRoot = Resolve-GodotExportTemplateDirectory $version
if (-not $templateRoot) {
    $templateBase = Join-Path $env:APPDATA 'Godot\export_templates'
    "BLOCKED: no matching 4.7 stable .NET Windows x86_64 templates found under $templateBase" | Out-File -FilePath (Join-Path $evidence 'package-status.txt') -Encoding utf8
    throw "Matching Godot 4.7 stable .NET export templates are unavailable under $templateBase"
}
$executable = Join-Path $package 'ProjectEmergence.exe'
$previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
$previousGitCommit = $env:GitCommit
$gitCommit = 'unknown'
$headFile = Join-Path $root '.git\HEAD'
if (Test-Path -LiteralPath $headFile) {
    $headValue = (Get-Content -LiteralPath $headFile -Raw).Trim()
    if ($headValue.StartsWith('ref: ', [StringComparison]::Ordinal)) {
        $refFile = Join-Path $root ('.git\' + $headValue.Substring(5).Replace('/', '\'))
        if (Test-Path -LiteralPath $refFile) { $gitCommit = (Get-Content -LiteralPath $refFile -Raw).Trim() }
    } elseif ($headValue -match '^[0-9a-fA-F]{40}$') {
        $gitCommit = $headValue
    }
}
try {
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:GitCommit = $gitCommit
    Invoke-LoggedCommand (Join-Path $evidence 'export.log') { & $godot --headless --path (Join-Path $root 'src\Emergence.App') --export-release 'Windows Desktop x86_64' $executable --quit }
} finally {
    if ($null -eq $previousNodeReuse) { Remove-Item Env:MSBUILDDISABLENODEREUSE -ErrorAction SilentlyContinue }
    else { $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse }
    if ($null -eq $previousGitCommit) { Remove-Item Env:GitCommit -ErrorAction SilentlyContinue }
    else { $env:GitCommit = $previousGitCommit }
}
$rulesetOutput = Join-Path $package 'rulesets'
New-Item -ItemType Directory -Path $rulesetOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'rulesets\foundation-reference.ruleset.json') -Destination (Join-Path $rulesetOutput 'foundation-reference.ruleset.json')
& (Join-Path $PSScriptRoot 'verify-package.ps1') -PackageDirectory $package -ArtifactsRoot $evidence
