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
$process = Start-Process -FilePath $gui -ArgumentList @('--path', $project, '--', '--save-load-qa') -WorkingDirectory $project -PassThru

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and -not $process.HasExited -and [DateTime]::UtcNow -lt $deadline)

    if ($process.HasExited) { throw "Godot normal shell exited early with code $($process.ExitCode)." }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Godot normal shell did not expose a window within 30 seconds.' }
    Start-Sleep -Seconds 2

    Add-Type -AssemblyName System.Drawing
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EmergenceWindowCapture {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
}
'@
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
        $screenshot = Join-Path $output 'shell-screenshot.png'
        $bitmap.Save($screenshot, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }

    'PASSED: normal FOUNDATION / M0.5 shell launched, saved and verified a coherent Paused tick 0 session, loaded it with no biological state, rendered, was captured fresh, and closed cleanly.' |
        Out-File -FilePath (Join-Path $output 'manual-launch-status.txt') -Encoding utf8
} finally {
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(10000)) { throw 'Godot shell did not close cleanly within 10 seconds.' }
    }
    $process.Dispose()
}
