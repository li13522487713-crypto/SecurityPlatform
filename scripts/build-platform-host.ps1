param(
  [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\\backend\\Atlas.PlatformHost\\Atlas.PlatformHost.csproj"
$args = @("build", $project, "-m:1", "/v:m")
if ($NoRestore) {
  $args += "--no-restore"
}

Write-Host "Running: dotnet $($args -join ' ')" -ForegroundColor Cyan
& dotnet @args
exit $LASTEXITCODE
