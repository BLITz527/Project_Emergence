param([string]$ArtifactsRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Get-RepositoryRoot
if (-not $ArtifactsRoot) { $ArtifactsRoot = Join-Path $root 'artifacts\cli' }
$output = New-ArtifactDirectory $ArtifactsRoot
$cli = Join-Path $root 'src\Emergence.Cli\Emergence.Cli.csproj'

foreach ($name in @('version.txt', 'doctor.json', 'doctor.log', 'self-test.json', 'self-test.log', 'domain-self-test.json', 'domain-self-test.log', 'rng-self-test.json', 'rng-self-test.log', 'ruleset-validation.json', 'ruleset-validation.log', 'session-self-test.json', 'session-self-test.log', 'persistence-self-test.json', 'persistence-self-test.log', 'foundation-session.emergence-world', 'world-package-fixture.json', 'world-package-fixture.log', 'world-package-verify.json', 'world-package-verify.log', 'world-package-recover.json', 'world-package-recover.log')) {
    $stale = Join-Path $output $name
    if (Test-Path -LiteralPath $stale -PathType Leaf) { Remove-Item -LiteralPath $stale -Force }
}

Invoke-LoggedCommand (Join-Path $output 'version.txt') { dotnet run --project $cli --configuration Release --no-build -- version }
Invoke-LoggedCommand (Join-Path $output 'doctor.log') { dotnet run --project $cli --configuration Release --no-build -- doctor --json (Join-Path $output 'doctor.json') }
Invoke-LoggedCommand (Join-Path $output 'self-test.log') { dotnet run --project $cli --configuration Release --no-build -- self-test --json (Join-Path $output 'self-test.json') }
Invoke-LoggedCommand (Join-Path $output 'domain-self-test.log') { dotnet run --project $cli --configuration Release --no-build -- domain-self-test --json (Join-Path $output 'domain-self-test.json') }
Invoke-LoggedCommand (Join-Path $output 'rng-self-test.log') { dotnet run --project $cli --configuration Release --no-build -- rng-self-test --json (Join-Path $output 'rng-self-test.json') }
Invoke-LoggedCommand (Join-Path $output 'ruleset-validation.log') { dotnet run --project $cli --configuration Release --no-build -- ruleset validate --directory (Join-Path $root 'rulesets') --json (Join-Path $output 'ruleset-validation.json') }
Invoke-LoggedCommand (Join-Path $output 'session-self-test.log') { dotnet run --project $cli --configuration Release --no-build -- session-self-test --json (Join-Path $output 'session-self-test.json') }
Invoke-LoggedCommand (Join-Path $output 'persistence-self-test.log') { dotnet run --project $cli --configuration Release --no-build -- persistence-self-test --json (Join-Path $output 'persistence-self-test.json') }
$fixture = Join-Path $output 'foundation-session.emergence-world'
Invoke-LoggedCommand (Join-Path $output 'world-package-fixture.log') { dotnet run --project $cli --configuration Release --no-build -- world-package fixture $fixture --json (Join-Path $output 'world-package-fixture.json') }
Invoke-LoggedCommand (Join-Path $output 'world-package-verify.log') { dotnet run --project $cli --configuration Release --no-build -- world-package verify $fixture --json (Join-Path $output 'world-package-verify.json') }
Invoke-LoggedCommand (Join-Path $output 'world-package-recover.log') { dotnet run --project $cli --configuration Release --no-build -- world-package recover $fixture --json (Join-Path $output 'world-package-recover.json') }
