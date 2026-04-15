[CmdletBinding()]
param(
    [string]$UpstreamRepo = "D:\Code\coze-studio-main",
    [int]$Port = 5182
)

$ErrorActionPreference = "Stop"

$appFolder = Join-Path $UpstreamRepo "frontend\apps\coze-studio"
$rsbuild = Join-Path $appFolder "node_modules\.bin\rsbuild.CMD"
$overrideConfig = Join-Path $appFolder "rsbuild.atlas.override.ts"

if (-not (Test-Path $rsbuild)) {
    throw "找不到 rsbuild 可执行文件: $rsbuild"
}

if (-not (Test-Path $overrideConfig)) {
    throw "找不到上游 Host 覆盖配置: $overrideConfig"
}

$env:IS_OPEN_SOURCE = "true"
Push-Location $appFolder
try {
    & $rsbuild preview --port $Port -c $overrideConfig
}
finally {
    Pop-Location
}
