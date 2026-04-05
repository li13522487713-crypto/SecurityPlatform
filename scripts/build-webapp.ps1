param(
  [ValidateSet("lint", "build", "all")]
  [string]$Task = "all"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$frontendRoot = Join-Path $repoRoot "src\\frontend"

$env:SystemDrive = "C:"
$env:ProgramFiles = "C:\Program Files"
[System.Environment]::SetEnvironmentVariable("ProgramFiles(x86)", "C:\Program Files (x86)")
$env:ALLUSERSPROFILE = "C:\ProgramData"
$env:ComSpec = "C:\Windows\System32\cmd.exe"
$env:windir = "C:\Windows"
$env:SystemRoot = "C:\Windows"

Push-Location $frontendRoot
try {
  Write-Host "Legacy atlas-webapp build script is deprecated. Using platform-web/app-web instead." -ForegroundColor Yellow

  if ($Task -eq "lint" -or $Task -eq "all") {
    Write-Host "Running: pnpm run lint" -ForegroundColor Cyan
    & pnpm run lint
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }

  if ($Task -eq "build" -or $Task -eq "all") {
    Write-Host "Running: pnpm run build:platform-web" -ForegroundColor Cyan
    & pnpm run build:platform-web
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "Running: pnpm run build:app-web" -ForegroundColor Cyan
    & pnpm run build:app-web
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }
}
finally {
  Pop-Location
}
