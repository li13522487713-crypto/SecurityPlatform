param(
  [Parameter(Mandatory = $true)][string]$AppKey,
  [Parameter(Mandatory = $true)][string]$ReleaseId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outRoot = Join-Path $repoRoot "artifacts\\app-packages"
$artifactId = "$AppKey-r$ReleaseId-$(Get-Date -Format 'yyyyMMddHHmmss')"
$artifactDir = Join-Path $outRoot $artifactId
$zipPath = Join-Path $outRoot "$artifactId.zip"
$tempDir = Join-Path $repoRoot "tmp\\app-package-build\\$artifactId"
$appHostProject = Join-Path $repoRoot "src\\backend\\Atlas.AppHost\\Atlas.AppHost.csproj"
$frontendRoot = Join-Path $repoRoot "src\\frontend"
$appWebDist = Join-Path $frontendRoot "apps\\app-web\\dist"

New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $artifactDir "frontend\\runtime") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $artifactDir "frontend\\login") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $artifactDir "backend\\Atlas.AppHost") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $artifactDir "metadata") | Out-Null

Write-Host "Building AppHost publish output..." -ForegroundColor Cyan
dotnet publish $appHostProject -c Release -o (Join-Path $tempDir "publish") | Out-Host

Write-Host "Building runtime/login frontend entries..." -ForegroundColor Cyan
Push-Location $frontendRoot
try {
  pnpm run build:app-web | Out-Host
}
finally {
  Pop-Location
}

if (-not (Test-Path $appWebDist)) {
  throw "AppWeb dist not found: $appWebDist"
}

Copy-Item -Path (Join-Path $appWebDist "*") -Destination (Join-Path $artifactDir "frontend\\runtime") -Recurse -Force
Copy-Item -Path (Join-Path $appWebDist "*") -Destination (Join-Path $artifactDir "frontend\\login") -Recurse -Force

Copy-Item -Path (Join-Path $tempDir "publish\\*") -Destination (Join-Path $artifactDir "backend\\Atlas.AppHost") -Recurse -Force

@{
  packageType = "atlas-app-package"
  appKey = $AppKey
  releaseId = $ReleaseId
  artifactId = $artifactId
  builtAtUtc = (Get-Date).ToUniversalTime().ToString("O")
} | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $artifactDir "manifest.json") -Encoding UTF8

if (Test-Path $zipPath) {
  Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $artifactDir "*") -DestinationPath $zipPath -Force
Write-Host "AppPackage generated: $zipPath" -ForegroundColor Green
