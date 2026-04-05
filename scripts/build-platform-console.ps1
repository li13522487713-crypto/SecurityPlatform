Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$frontendRoot = Join-Path $repoRoot "src\\frontend"

Push-Location $frontendRoot
try {
  Write-Host "Running: pnpm run build:platform-web" -ForegroundColor Cyan
  & pnpm run build:platform-web
  exit $LASTEXITCODE
}
finally {
  Pop-Location
}
