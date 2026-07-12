param(
    [ValidateSet('Debug', 'Release', 'Both')][string]$Configuration = 'Both',
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\build' }
$logs = New-ArtifactDirectory $ArtifactsRoot
$solution = Join-Path $root 'ProjectEmergence.slnx'
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

Invoke-LoggedCommand (Join-Path $logs 'restore.log') { dotnet restore $solution }
$configurations = if ($Configuration -eq 'Both') { @('Debug', 'Release') } else { @($Configuration) }
foreach ($item in $configurations) {
    Invoke-LoggedCommand (Join-Path $logs ("build-{0}.log" -f $item.ToLowerInvariant())) { dotnet build $solution --configuration $item --no-restore ("-p:GitCommit={0}" -f $gitCommit) }
}

Get-ChildItem -LiteralPath $root -Filter 'Emergence.*.dll' -Recurse -File |
    Where-Object { $_.FullName -match '\\bin\\' } |
    Select-Object FullName, Length, @{ Name = 'Sha256'; Expression = { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } } |
    ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $logs 'assemblies.json') -Encoding utf8
