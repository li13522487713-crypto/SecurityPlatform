Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sqlRoot = Join-Path $repoRoot "sql"
$platformRoot = Join-Path $sqlRoot "platform"
$appRoot = Join-Path $sqlRoot "app"

if (!(Test-Path $platformRoot)) {
  throw "Missing sql/platform directory."
}

if (!(Test-Path $appRoot)) {
  throw "Missing sql/app directory."
}

$platformScripts = @(Get-ChildItem -Path $platformRoot -Filter *.sql -File | Sort-Object Name)
$appScripts = @(Get-ChildItem -Path $appRoot -Filter *.sql -File | Sort-Object Name)

if ($platformScripts.Count -eq 0) {
  throw "sql/platform has no migration scripts."
}

if ($appScripts.Count -eq 0) {
  throw "sql/app has no migration scripts."
}

foreach ($script in $platformScripts + $appScripts) {
  if ($script.Length -le 0) {
    throw "Script is empty: $($script.FullName)"
  }

  if ($script.Name -notmatch '^\d{8}_\d{3}_.+\.sql$') {
    throw "Invalid script naming: $($script.Name). Expected yyyymmdd_###_name.sql."
  }
}

Write-Host "Database migration scripts verified." -ForegroundColor Green
Write-Host "platform scripts: $($platformScripts.Count), app scripts: $($appScripts.Count)"
