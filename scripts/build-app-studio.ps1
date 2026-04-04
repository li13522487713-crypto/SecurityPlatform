Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webRoot = Join-Path $repoRoot "src\\frontend\\Atlas.WebApp"

Push-Location $webRoot
try {
  Write-Host "Running: npm run build:app-studio" -ForegroundColor Cyan
  & npm run build:app-studio
  exit $LASTEXITCODE
}
finally {
  Pop-Location
}
