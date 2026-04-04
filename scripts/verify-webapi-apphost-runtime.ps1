param(
  [string]$TenantId = "00000000-0000-0000-0000-000000000001",
  [string]$Username = "admin",
  [string]$Password = "P@ssw0rd!",
  [int]$WebApiPort = 5000,
  [long]$InstanceId = 0,
  [int]$WebApiReadyTimeoutSeconds = 60,
  [int]$HealthReadyTimeoutSeconds = 30,
  [string]$EncryptionKey = "Atlas@DbEncrypt!SimulatedProd#2025`$Key"
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
      $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
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
$webApiProjectDir = Join-Path $repoRoot "src\\backend\\Atlas.WebApi"
$webApiOut = Join-Path $repoRoot "tmp-webapi-runtime-verify.out.log"
$webApiErr = Join-Path $repoRoot "tmp-webapi-runtime-verify.err.log"

$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldEncryptionKey = $env:Database__Encryption__Key
$webApiRunner = $null

try {
  Stop-ProcessByNameSafe -Names @("Atlas.WebApi", "Atlas.AppHost")

  foreach ($logFile in @($webApiOut, $webApiErr)) {
    if (Test-Path $logFile) {
      Remove-Item $logFile -Force
    }
  }

  $env:ASPNETCORE_ENVIRONMENT = "Development"
  $env:Database__Encryption__Key = $EncryptionKey

  $webApiRunner = Start-Process dotnet `
    -ArgumentList @("run", "--no-build", "--no-launch-profile") `
    -WorkingDirectory $webApiProjectDir `
    -RedirectStandardOutput $webApiOut `
    -RedirectStandardError $webApiErr `
    -PassThru

  $swaggerUrl = "http://localhost:$WebApiPort/swagger/v1/swagger.json"
  if (-not (Wait-Http200 -Url $swaggerUrl -TimeoutSeconds $WebApiReadyTimeoutSeconds)) {
    throw "WebApi did not become ready at $swaggerUrl within $WebApiReadyTimeoutSeconds seconds."
  }

  $loginBody = @{
    username = $Username
    password = $Password
  } | ConvertTo-Json

  $loginResponse = Invoke-WebRequest `
    -Uri "http://localhost:$WebApiPort/api/v1/auth/token" `
    -Method Post `
    -Headers @{ "X-Tenant-Id" = $TenantId } `
    -ContentType "application/json" `
    -Body $loginBody `
    -UseBasicParsing `
    -TimeoutSec 20

  $loginJson = $loginResponse.Content | ConvertFrom-Json
  $accessToken = [string]$loginJson.data.accessToken
  if ([string]::IsNullOrWhiteSpace($accessToken)) {
    throw "Login succeeded but accessToken is empty."
  }

  $csrfToken = [string]$loginResponse.Headers["X-CSRF-TOKEN"]
  $readHeaders = @{
    "Authorization" = "Bearer $accessToken"
    "X-Tenant-Id"   = $TenantId
  }

  if ($InstanceId -le 0) {
    $listResponse = Invoke-WebRequest `
      -Uri "http://localhost:$WebApiPort/api/v2/tenant-app-instances?pageIndex=1&pageSize=1" `
      -Headers $readHeaders `
      -UseBasicParsing `
      -TimeoutSec 20
    $listJson = $listResponse.Content | ConvertFrom-Json
    $InstanceId = [long]$listJson.data.items[0].id
  }

  if ($InstanceId -le 0) {
    throw "No tenant app instance is available for verification."
  }

  $writeHeaders = @{
    "Authorization"  = "Bearer $accessToken"
    "X-Tenant-Id"    = $TenantId
    "X-CSRF-TOKEN"   = $csrfToken
    "Idempotency-Key" = [guid]::NewGuid().ToString()
  }

  $startResponse = Invoke-WebRequest `
    -Uri "http://localhost:$WebApiPort/api/v2/tenant-app-instances/$InstanceId/start" `
    -Method Post `
    -Headers $writeHeaders `
    -UseBasicParsing `
    -TimeoutSec 120
  $startJson = $startResponse.Content | ConvertFrom-Json

  $assignedPort = [int]$startJson.data.assignedPort
  if ($assignedPort -le 0) {
    throw "Start API did not return a valid assignedPort."
  }

  $healthReady = $false
  $healthJson = $null
  $healthDeadline = (Get-Date).AddSeconds($HealthReadyTimeoutSeconds)
  while ((Get-Date) -lt $healthDeadline) {
    try {
      $healthResponse = Invoke-WebRequest `
        -Uri "http://localhost:$WebApiPort/api/v2/tenant-app-instances/$InstanceId/health" `
        -Headers $readHeaders `
        -UseBasicParsing `
        -TimeoutSec 10
      $healthJson = $healthResponse.Content | ConvertFrom-Json
      if ($healthJson.data.ready -eq $true) {
        $healthReady = $true
        break
      }
    } catch {
    }

    Start-Sleep -Milliseconds 500
  }

  if (-not $healthReady) {
    throw "Health did not become ready within $HealthReadyTimeoutSeconds seconds."
  }

  $runtimeResponse = Invoke-WebRequest `
    -Uri "http://localhost:$WebApiPort/api/v2/tenant-app-instances/$InstanceId/runtime-info" `
    -Headers $readHeaders `
    -UseBasicParsing `
    -TimeoutSec 20
  $runtimeJson = $runtimeResponse.Content | ConvertFrom-Json

  $directReady = Invoke-WebRequest `
    -Uri "http://127.0.0.1:$assignedPort/internal/health/ready" `
    -UseBasicParsing `
    -TimeoutSec 10

  $writeHeaders["Idempotency-Key"] = [guid]::NewGuid().ToString()
  $stopResponse = Invoke-WebRequest `
    -Uri "http://localhost:$WebApiPort/api/v2/tenant-app-instances/$InstanceId/stop" `
    -Method Post `
    -Headers $writeHeaders `
    -UseBasicParsing `
    -TimeoutSec 60
  $stopJson = $stopResponse.Content | ConvertFrom-Json

  [pscustomobject]@{
    verifiedAtUtc               = (Get-Date).ToUniversalTime().ToString("O")
    instanceId                  = $InstanceId
    startRuntimeStatus          = $startJson.data.runtimeStatus
    startHealthStatus           = $startJson.data.healthStatus
    assignedPort                = $assignedPort
    startPid                    = $startJson.data.currentPid
    healthReady                 = $healthReady
    healthStatus                = $healthJson.data.healthStatus
    runtimeInfoStatus           = $runtimeJson.data.runtimeStatus
    runtimeInfoPid              = $runtimeJson.data.currentPid
    appHostDirectReadyHttpCode  = $directReady.StatusCode
    stopRuntimeStatus           = $stopJson.data.runtimeStatus
  } | ConvertTo-Json -Depth 5
}
catch {
  Write-Host "Verification failed: $($_.Exception.Message)" -ForegroundColor Red
  if (Test-Path $webApiOut) {
    Write-Host "`n--- WebApi stdout (tail) ---" -ForegroundColor Yellow
    Get-Content $webApiOut -Tail 80
  }

  if (Test-Path $webApiErr) {
    Write-Host "`n--- WebApi stderr (tail) ---" -ForegroundColor Yellow
    Get-Content $webApiErr -Tail 120
  }

  throw
}
finally {
  Stop-ProcessByNameSafe -Names @("Atlas.AppHost")
  if ($webApiRunner -ne $null) {
    try {
      Stop-Process -Id $webApiRunner.Id -Force -ErrorAction SilentlyContinue
    } catch {
    }
  }

  if ($null -eq $oldEnvironment) {
    Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
  } else {
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
  }

  if ($null -eq $oldEncryptionKey) {
    Remove-Item Env:Database__Encryption__Key -ErrorAction SilentlyContinue
  } else {
    $env:Database__Encryption__Key = $oldEncryptionKey
  }
}
