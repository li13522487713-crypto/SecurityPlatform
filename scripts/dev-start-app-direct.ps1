# Atlas 应用直连联调启动脚本（应用级独立模式）
# 启动组件：
# - AppHost (:5002)
# - AppWeb (:5181, direct 模式)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$frontendRoot = Join-Path $repoRoot "src/frontend"

Write-Host "=== Atlas 应用直连联调启动 ===" -ForegroundColor Cyan
Write-Host "仓库根目录: $repoRoot" -ForegroundColor DarkGray

Write-Host "启动 AppHost (:5002)..." -ForegroundColor Green
Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/backend/Atlas.AppHost") -WorkingDirectory $repoRoot

Start-Sleep -Seconds 2

Write-Host "启动 AppWeb (:5181, direct)..." -ForegroundColor Green
Set-Location $frontendRoot
$env:VITE_APP_RUNTIME_MODE = "direct"
pnpm run dev:app-web
