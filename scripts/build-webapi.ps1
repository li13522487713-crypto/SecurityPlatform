param(
  [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "Atlas.SecurityPlatform.slnx"

if (-not (Test-Path $solutionPath)) {
  throw "未找到解决方案文件: $solutionPath"
}

Write-Warning "scripts/build-webapi.ps1 已迁移为构建 AppHost 与整个解决方案。"

$args = @("build", $solutionPath, "-m:1", "/v:m")
if ($NoRestore) {
  $args += "--no-restore"
}

Write-Host "Running: dotnet $($args -join ' ')" -ForegroundColor Cyan
& dotnet @args
exit $LASTEXITCODE
