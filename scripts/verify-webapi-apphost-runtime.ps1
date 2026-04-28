param(
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
$appHostProject = Join-Path $repoRoot "src\\backend\\Atlas.AppHost\\Atlas.AppHost.csproj"

$appOut = Join-Path $repoRoot "tmp-apphost-runtime-verify.out.log"
$appErr = Join-Path $repoRoot "tmp-apphost-runtime-verify.err.log"

$appRunner = $null

try {
  Stop-ProcessByNameSafe -Names @("Atlas.AppHost")

  foreach ($logFile in @($appOut, $appErr)) {
    if (Test-Path $logFile) {
      Remove-Item $logFile -Force
    }
  }

  $appRunner = Start-Process dotnet `
    -ArgumentList @("run", "--project", $appHostProject, "--no-build", "--no-launch-profile") `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $appOut `
    -RedirectStandardError $appErr `
    -PassThru

  $appReadyUrl = "http://localhost:$AppHostPort/internal/health/ready"

  if (-not (Wait-Http200 -Url $appReadyUrl -TimeoutSeconds $ReadyTimeoutSeconds)) {
    throw "AppHost 未在超时时间内就绪: $appReadyUrl"
  }

  [pscustomobject]@{
    verifiedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    appHost = @{ port = $AppHostPort; ready = $true }
  } | ConvertTo-Json -Depth 5
}
catch {
  Write-Host "Runtime verification failed: $($_.Exception.Message)" -ForegroundColor Red

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
  Stop-ProcessByNameSafe -Names @("Atlas.AppHost")

  if ($appRunner -ne $null) {
    try {
      Stop-Process -Id $appRunner.Id -Force -ErrorAction SilentlyContinue
    } catch {
    }
  }
}
