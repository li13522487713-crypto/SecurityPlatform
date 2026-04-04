# Atlas 多宿主开发一键启动脚本
# 同时启动 PlatformHost (:5001) + AppHost (:5002) + 前端 platform-console (:5173)

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Host "=== Atlas 开发环境启动 ===" -ForegroundColor Cyan

Write-Host "启动 PlatformHost (:5001)..." -ForegroundColor Green
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/backend/Atlas.PlatformHost" -WorkingDirectory $root

Write-Host "启动 AppHost (:5002)..." -ForegroundColor Green
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src/backend/Atlas.AppHost" -WorkingDirectory $root

Write-Host "等待 3 秒后启动前端..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

Write-Host "启动前端 platform-console (:5173)..." -ForegroundColor Green
Set-Location "$root/src/frontend/Atlas.WebApp"
npm run dev:platform-console
