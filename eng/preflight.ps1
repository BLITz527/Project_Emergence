param(
    [string]$GodotPath,
    [string]$ArtifactsRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\preflight' }
$output = New-ArtifactDirectory $ArtifactsRoot

$windows = $null
try {
    $windows = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop | Select-Object Caption, Version, OSArchitecture, BuildNumber
} catch {
    $windows = [pscustomobject]@{
        Caption = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        Version = [System.Environment]::OSVersion.Version.ToString()
        OSArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        BuildNumber = [System.Environment]::OSVersion.Version.Build
    }
}
$windows | Format-List | Out-File -FilePath (Join-Path $output 'windows-info.txt') -Encoding utf8
dotnet --info 2>&1 | Tee-Object -FilePath (Join-Path $output 'dotnet-info.txt')
$dotnetExit = $LASTEXITCODE
git --version 2>&1 | Tee-Object -FilePath (Join-Path $output 'git-version.txt')
$gitExit = $LASTEXITCODE
$PSVersionTable | Format-List | Out-File -FilePath (Join-Path $output 'powershell-version.txt') -Encoding utf8

$minimal = Join-Path $output 'minimal-sdk-compile'
if (Test-Path -LiteralPath $minimal) {
    $resolvedMinimal = [System.IO.Path]::GetFullPath($minimal)
    $resolvedOutput = [System.IO.Path]::GetFullPath($output)
    if (-not $resolvedMinimal.StartsWith($resolvedOutput + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear unexpected probe path: $resolvedMinimal"
    }
    Remove-Item -LiteralPath $resolvedMinimal -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $minimal | Out-Null
dotnet new classlib --force --no-restore --framework net10.0 --name MinimalSdkCompile --output $minimal 2>&1 | Tee-Object -FilePath (Join-Path $output 'minimal-create.log')
$minimalCreateExit = $LASTEXITCODE
dotnet build $minimal --configuration Release 2>&1 | Tee-Object -FilePath (Join-Path $output 'minimal-build.log')
$minimalBuildExit = $LASTEXITCODE

$resolvedGodot = Resolve-GodotExecutable $GodotPath
$godotVersion = ''
$godotExit = -1
$godotDotnet = $false
$templates = $false
if ($resolvedGodot) {
    $godotVersion = (& $resolvedGodot --version 2>&1 | Out-String).Trim()
    $godotExit = $LASTEXITCODE
    $godotVersion | Out-File -FilePath (Join-Path $output 'godot-version.txt') -Encoding utf8
    $godotDotnet = ($godotVersion -match 'mono|\.net') -or ((Split-Path -Leaf $resolvedGodot) -match 'mono|\.net')
    $templateRoot = Resolve-GodotExportTemplateDirectory $godotVersion
    $templates = [bool]$templateRoot
} else {
    'UNAVAILABLE: no Godot executable was found.' | Out-File -FilePath (Join-Path $output 'godot-version.txt') -Encoding utf8
}

$result = [ordered]@{
    createdUtc = [DateTime]::UtcNow.ToString('o')
    windows = $windows
    processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    powershellVersion = $PSVersionTable.PSVersion.ToString()
    dotnetSdk = (& dotnet --version).Trim()
    dotnetInfoExitCode = $dotnetExit
    gitVersionExitCode = $gitExit
    minimalCompile = [ordered]@{ createExitCode = $minimalCreateExit; buildExitCode = $minimalBuildExit; success = ($minimalBuildExit -eq 0) }
    targetFramework = 'net10.0'
    godotExecutable = if ($resolvedGodot) { $resolvedGodot } else { '' }
    godotVersion = $godotVersion
    godotExitCode = $godotExit
    godotDotnetEdition = $godotDotnet
    windowsExportTemplatesAvailable = $templates
    limitations = @(
        if (-not $resolvedGodot) { 'Godot 4.7 .NET executable unavailable.' }
        if ($resolvedGodot -and -not $godotDotnet) { 'Selected Godot executable is not verifiably .NET-enabled.' }
        if (-not $templates) { 'Matching Windows export templates unavailable.' }
    )
}
$result | ConvertTo-Json -Depth 8 | Out-File -FilePath (Join-Path $output 'preflight.json') -Encoding utf8
Write-Host "Preflight evidence: $output"
if ($dotnetExit -ne 0 -or $gitExit -ne 0 -or $minimalBuildExit -ne 0) { exit 1 }
exit 0
