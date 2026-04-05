param(
  [int]$PlatformHostPort = 5001,
  [int]$AppHostPort = 5002,
  [int]$ReadyTimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Wait-Http200 {
  param(
    [Parameter(Mandatory = $true)][string]$Url,
    [Parameter(Mandatory = $true)][int]$TimeoutSeconds
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    try {
      $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
      if ($response.StatusCode -eq 200) {
        return $true
      }
    } catch {
    }

    Start-Sleep -Milliseconds 500
  }

  return $false
}

function Stop-ProcessByNameSafe {
  param([string[]]$Names)

  foreach ($name in $Names) {
    Get-Process -Name $name -ErrorAction SilentlyContinue |
      Stop-Process -Force -ErrorAction SilentlyContinue
  }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$platformHostProject = Join-Path $repoRoot "src\\backend\\Atlas.PlatformHost\\Atlas.PlatformHost.csproj"
$appHostProject = Join-Path $repoRoot "src\\backend\\Atlas.AppHost\\Atlas.AppHost.csproj"

$platformOut = Join-Path $repoRoot "tmp-platformhost-runtime-verify.out.log"
$platformErr = Join-Path $repoRoot "tmp-platformhost-runtime-verify.err.log"
$appOut = Join-Path $repoRoot "tmp-apphost-runtime-verify.out.log"
$appErr = Join-Path $repoRoot "tmp-apphost-runtime-verify.err.log"

$platformRunner = $null
$appRunner = $null

try {
  Stop-ProcessByNameSafe -Names @("Atlas.PlatformHost", "Atlas.AppHost")

  foreach ($logFile in @($platformOut, $platformErr, $appOut, $appErr)) {
    if (Test-Path $logFile) {
      Remove-Item $logFile -Force
    }
  }

  $platformRunner = Start-Process dotnet `
    -ArgumentList @("run", "--project", $platformHostProject, "--no-build", "--no-launch-profile") `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $platformOut `
    -RedirectStandardError $platformErr `
    -PassThru

  $appRunner = Start-Process dotnet `
    -ArgumentList @("run", "--project", $appHostProject, "--no-build", "--no-launch-profile") `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $appOut `
    -RedirectStandardError $appErr `
    -PassThru

  $platformReadyUrl = "http://localhost:$PlatformHostPort/internal/health/ready"
  $appReadyUrl = "http://localhost:$AppHostPort/internal/health/ready"

  if (-not (Wait-Http200 -Url $platformReadyUrl -TimeoutSeconds $ReadyTimeoutSeconds)) {
    throw "PlatformHost 未在超时时间内就绪: $platformReadyUrl"
  }

  if (-not (Wait-Http200 -Url $appReadyUrl -TimeoutSeconds $ReadyTimeoutSeconds)) {
    throw "AppHost 未在超时时间内就绪: $appReadyUrl"
  }

  [pscustomobject]@{
    verifiedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    platformHost = @{ port = $PlatformHostPort; ready = $true }
    appHost = @{ port = $AppHostPort; ready = $true }
  } | ConvertTo-Json -Depth 5
}
catch {
  Write-Host "Runtime verification failed: $($_.Exception.Message)" -ForegroundColor Red

  if (Test-Path $platformOut) {
    Write-Host "`n--- PlatformHost stdout (tail) ---" -ForegroundColor Yellow
    Get-Content $platformOut -Tail 80
  }

  if (Test-Path $platformErr) {
    Write-Host "`n--- PlatformHost stderr (tail) ---" -ForegroundColor Yellow
    Get-Content $platformErr -Tail 120
  }

  if (Test-Path $appOut) {
    Write-Host "`n--- AppHost stdout (tail) ---" -ForegroundColor Yellow
    Get-Content $appOut -Tail 80
  }

  if (Test-Path $appErr) {
    Write-Host "`n--- AppHost stderr (tail) ---" -ForegroundColor Yellow
    Get-Content $appErr -Tail 120
  }

  throw
}
finally {
  Stop-ProcessByNameSafe -Names @("Atlas.PlatformHost", "Atlas.AppHost")

  if ($platformRunner -ne $null) {
    try {
      Stop-Process -Id $platformRunner.Id -Force -ErrorAction SilentlyContinue
    } catch {
    }
  }

  if ($appRunner -ne $null) {
    try {
      Stop-Process -Id $appRunner.Id -Force -ErrorAction SilentlyContinue
    } catch {
    }
  }
}
