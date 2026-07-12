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
