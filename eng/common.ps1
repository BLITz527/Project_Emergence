Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

function New-ArtifactDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    return [System.IO.Path]::GetFullPath($Path)
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
    & $Command 2>&1 | Tee-Object -FilePath $LogPath
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE. See $LogPath"
    }
}

function Resolve-GodotExecutable {
    param([string]$GodotPath)
    if ($GodotPath -and (Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $GodotPath).Path
    }
    if ($env:GODOT4 -and (Test-Path -LiteralPath $env:GODOT4 -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $env:GODOT4).Path
    }
    foreach ($name in @('godot4-mono', 'godot-mono', 'godot4', 'godot')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }
    return $null
}

function Resolve-GodotExportTemplateDirectory {
    param([Parameter(Mandatory = $true)][string]$GodotVersion)

    $templateBase = Join-Path $env:APPDATA 'Godot\export_templates'
    if (-not (Test-Path -LiteralPath $templateBase -PathType Container)) { return $null }
    if ($GodotVersion -notmatch '^(\d+\.\d+(?:\.\d+)?\.stable(?:\.mono)?)') { return $null }

    $identifiers = @($Matches[1])
    if ($Matches[1].EndsWith('.mono', [StringComparison]::Ordinal)) {
        $identifiers += $Matches[1].Substring(0, $Matches[1].Length - 5)
    }
    foreach ($identifier in $identifiers) {
        $candidate = Join-Path $templateBase $identifier
        if (Test-Path -LiteralPath (Join-Path $candidate 'windows_release_x86_64.exe') -PathType Leaf) {
            return $candidate
        }
    }
    return $null
}
