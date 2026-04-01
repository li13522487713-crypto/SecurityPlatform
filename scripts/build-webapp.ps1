param(
  [ValidateSet("lint", "build", "all")]
  [string]$Task = "all"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webRoot = Join-Path $repoRoot "src\\frontend\\Atlas.WebApp"

$env:SystemDrive = "C:"
$env:ProgramFiles = "C:\Program Files"
[System.Environment]::SetEnvironmentVariable("ProgramFiles(x86)", "C:\Program Files (x86)")
$env:ALLUSERSPROFILE = "C:\ProgramData"
$env:ComSpec = "C:\Windows\System32\cmd.exe"
$env:windir = "C:\Windows"
$env:SystemRoot = "C:\Windows"

Push-Location $webRoot
try {
  if ($Task -eq "lint" -or $Task -eq "all") {
    Write-Host "Running: npm run lint" -ForegroundColor Cyan
    & npm run lint
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }

  if ($Task -eq "build" -or $Task -eq "all") {
    Write-Host "Running: npm run build" -ForegroundColor Cyan
    & npm run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }
}
finally {
  Pop-Location
}

