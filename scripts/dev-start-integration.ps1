# Atlas 双前端 + 双宿主联调启动脚本（平台集成模式）
# 启动组件：
# - PlatformHost (:5001)
# - AppHost (:5002)
# - PlatformWeb (:5180)
# - AppWeb (:5181, platform 模式)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$frontendRoot = Join-Path $repoRoot "src/frontend"

Write-Host "=== Atlas 平台集成联调启动 ===" -ForegroundColor Cyan
Write-Host "仓库根目录: $repoRoot" -ForegroundColor DarkGray

Write-Host "启动 PlatformHost (:5001)..." -ForegroundColor Green
Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/backend/Atlas.PlatformHost") -WorkingDirectory $repoRoot

Write-Host "启动 AppHost (:5002)..." -ForegroundColor Green
Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/backend/Atlas.AppHost") -WorkingDirectory $repoRoot

Start-Sleep -Seconds 3

Write-Host "启动 PlatformWeb (:5180)..." -ForegroundColor Green
Start-Process -FilePath "powershell" -ArgumentList @(
  "-NoExit",
  "-Command",
  "Set-Location '$frontendRoot'; pnpm run dev:platform-web"
) -WorkingDirectory $repoRoot

Write-Host "启动 AppWeb (:5181, platform)..." -ForegroundColor Green
Start-Process -FilePath "powershell" -ArgumentList @(
  "-NoExit",
  "-Command",
  "Set-Location '$frontendRoot'; `$env:VITE_APP_RUNTIME_MODE='platform'; pnpm run dev:app-web"
) -WorkingDirectory $repoRoot

Write-Host ""
Write-Host "已发起全部进程。建议访问：" -ForegroundColor Yellow
Write-Host "  PlatformWeb: http://localhost:5180"
Write-Host "  AppWeb:      http://localhost:5181/apps/dev-app/login"
