param(
    [string]$GodotPath,
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\app' }
$output = New-ArtifactDirectory $ArtifactsRoot
$godot = Resolve-GodotExecutable $GodotPath
if (-not $godot) { throw 'Godot 4.7 stable .NET executable was not found.' }

$gui = $godot -replace '_console\.exe$', '.exe'
if (-not (Test-Path -LiteralPath $gui -PathType Leaf)) { $gui = $godot }
$project = Join-Path $root 'src\Emergence.App'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EmergenceWindowCapture {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
}
'@

function Capture-FieldMode {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][bool]$RawGrid
    )
    $ready = Join-Path $output ("{0}-ready.txt" -f $Name)
    if (Test-Path -LiteralPath $ready) { Remove-Item -LiteralPath $ready -Force }
    $arguments = @('--path', $project, '--', '--save-load-qa', '--qa-ready-file', $ready)
    if ($RawGrid) { $arguments += '--raw-grid' }
    $process = Start-Process -FilePath $gui -ArgumentList $arguments -WorkingDirectory $project -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 100
            $process.Refresh()
        } while (($process.MainWindowHandle -eq [IntPtr]::Zero -or -not (Test-Path -LiteralPath $ready -PathType Leaf)) -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)
        if ($process.HasExited) { throw "Godot $Name shell exited early with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw "Godot $Name shell did not expose a window within 30 seconds." }
        if (-not (Test-Path -LiteralPath $ready -PathType Leaf) -or (Get-Content -LiteralPath $ready -Raw).Trim() -ne 'PROJECT_EMERGENCE_SAVE_LOAD_QA_READY') {
            throw "Godot $Name shell did not complete the save/load QA handshake within 30 seconds."
        }
        Start-Sleep -Milliseconds 500
        $rectangle = New-Object EmergenceWindowCapture+Rect
        if (-not [EmergenceWindowCapture]::GetWindowRect($process.MainWindowHandle, [ref]$rectangle)) { throw 'Could not read the Godot shell window bounds.' }
        $width = $rectangle.Right - $rectangle.Left
        $height = $rectangle.Bottom - $rectangle.Top
        if ($width -le 0 -or $height -le 0) { throw "Invalid Godot window bounds: ${width}x${height}." }
        $bitmap = New-Object System.Drawing.Bitmap $width, $height
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try { $graphics.CopyFromScreen($rectangle.Left, $rectangle.Top, 0, 0, $bitmap.Size) }
            finally { $graphics.Dispose() }
            $bitmap.Save((Join-Path $output ("field-{0}.png" -f $Name)), [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
    } finally {
        if (-not $process.HasExited) {
            $null = $process.CloseMainWindow()
            if (-not $process.WaitForExit(10000)) { throw "Godot $Name shell did not close cleanly within 10 seconds." }
        }
        $process.Dispose()
    }
}

Capture-FieldMode -Name 'normal' -RawGrid $false
Capture-FieldMode -Name 'raw-grid' -RawGrid $true
'PASSED: normal and raw-grid HABITAT / M1.1 shells launched, saved and verified a coherent Paused static-environment session, loaded exact V2 fields with no biological state, rendered, were captured fresh, and closed cleanly.' |
    Out-File -FilePath (Join-Path $output 'manual-launch-status.txt') -Encoding utf8
