param(
  [string]$BaseUrl = "http://127.0.0.1:5002",
  [string]$Project = "src/backend/Atlas.AppHost/Atlas.AppHost.csproj",
  [int]$StartupTimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Wait-AppHostReady {
  param([int]$TimeoutSeconds)
  $startedAt = Get-Date
  while (((Get-Date) - $startedAt).TotalSeconds -lt $TimeoutSeconds) {
    $conn = Get-NetTCPConnection -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue
    if ($conn) {
      return $true
    }
    Start-Sleep -Milliseconds 500
  }
  return $false
}

function Invoke-RestStatusCode {
  param(
    [string]$Method,
    [string]$Uri,
    [string]$Body
  )
  try {
    if ($Method -eq "GET") {
      $resp = Invoke-WebRequest -Uri $Uri -Method GET -TimeoutSec 10
    }
    else {
      $resp = Invoke-WebRequest -Uri $Uri -Method $Method -ContentType "application/json" -Body $Body -TimeoutSec 10
    }
    return [int]$resp.StatusCode
  }
  catch {
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
      return [int]$_.Exception.Response.StatusCode
    }
    return -1
  }
}

function Convert-ToWsUrl {
  param([string]$Url)
  if ($Url.StartsWith("https://")) {
    return "wss://" + $Url.Substring("https://".Length)
  }
  if ($Url.StartsWith("http://")) {
    return "ws://" + $Url.Substring("http://".Length)
  }
  return $Url
}

$logDir = Join-Path $env:TEMP "securityplatform-service-logs"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$outLog = Join-Path $logDir "apphost.ws-cutover.verify.out.log"
$errLog = Join-Path $logDir "apphost.ws-cutover.verify.err.log"

$appHost = Start-Process -FilePath "dotnet" -ArgumentList @("run", "-c", "Debug", "--project", $Project) -WorkingDirectory (Get-Location) -RedirectStandardOutput $outLog -RedirectStandardError $errLog -WindowStyle Hidden -PassThru

try {
  if (-not (Wait-AppHostReady -TimeoutSeconds $StartupTimeoutSeconds)) {
    throw "AppHost did not become ready on port 5002 within $StartupTimeoutSeconds seconds."
  }

  $restCases = @(
    @{ Method = "GET"; Path = "/api/debug/demo/state"; Expected = 404; Body = $null },
    @{ Method = "GET"; Path = "/api/debug/demo/variables"; Expected = 404; Body = $null },
    @{ Method = "POST"; Path = "/api/debug/demo/step"; Expected = 404; Body = "{}" },
    @{ Method = "POST"; Path = "/api/debug/demo/breakpoints"; Expected = 404; Body = "{}" },
    @{ Method = "POST"; Path = "/api/debug/demo/start"; Expected = 404; Body = "{}" },
    @{ Method = "POST"; Path = "/api/debug/demo/stop"; Expected = 404; Body = "{}" }
  )

  $restResults = foreach ($case in $restCases) {
    $uri = "$BaseUrl$($case.Path)"
    $statusCode = Invoke-RestStatusCode -Method $case.Method -Uri $uri -Body $case.Body
    [pscustomobject]@{
      Check = "REST-Removed"
      Method = $case.Method
      Path = $case.Path
      Expected = $case.Expected
      Actual = $statusCode
      Passed = ($statusCode -eq $case.Expected)
    }
  }

  $ws = [System.Net.WebSockets.ClientWebSocket]::new()
  $wsUri = [Uri]((Convert-ToWsUrl -Url $BaseUrl) + "/api/debug/microflow/mf-verify?sessionId=session-verify-1")
  $cts = [System.Threading.CancellationTokenSource]::new()
  $cts.CancelAfter([TimeSpan]::FromSeconds(15))

  $start = Get-Date
  $null = $ws.ConnectAsync($wsUri, $cts.Token).GetAwaiter().GetResult()
  $connectedInMs = [int](((Get-Date) - $start).TotalMilliseconds)

  $buf = New-Object byte[] 65536
  $initialTypes = @()
  for ($i = 0; $i -lt 4; $i++) {
    $seg = [ArraySegment[byte]]::new($buf)
    $recv = $ws.ReceiveAsync($seg, $cts.Token).GetAwaiter().GetResult()
    if ($recv.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
      break
    }
    $txt = [System.Text.Encoding]::UTF8.GetString($buf, 0, $recv.Count)
    try {
      $obj = $txt | ConvertFrom-Json
      if ($obj.type) { $initialTypes += [string]$obj.type }
    }
    catch {
    }
    if ($initialTypes.Count -ge 2) {
      break
    }
  }

  $pingPayload = '{"type":"ping","data":{"sequence":12345}}'
  $pingBytes = [System.Text.Encoding]::UTF8.GetBytes($pingPayload)
  $null = $ws.SendAsync([ArraySegment[byte]]::new($pingBytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult()

  $pongReceived = $false
  for ($i = 0; $i -lt 4; $i++) {
    $seg = [ArraySegment[byte]]::new($buf)
    $recv = $ws.ReceiveAsync($seg, $cts.Token).GetAwaiter().GetResult()
    if ($recv.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
      break
    }
    $txt = [System.Text.Encoding]::UTF8.GetString($buf, 0, $recv.Count)
    if ($txt -match '"type"\s*:\s*"pong"') {
      $pongReceived = $true
      break
    }
  }

  $serverHeartbeatPingReceived = $false
  $heartbeatCts = [System.Threading.CancellationTokenSource]::new()
  $heartbeatCts.CancelAfter([TimeSpan]::FromSeconds(35))
  try {
    while (-not $heartbeatCts.IsCancellationRequested) {
      $seg = [ArraySegment[byte]]::new($buf)
      $recv = $ws.ReceiveAsync($seg, $heartbeatCts.Token).GetAwaiter().GetResult()
      if ($recv.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
        break
      }
      $txt = [System.Text.Encoding]::UTF8.GetString($buf, 0, $recv.Count)
      if ($txt -match '"type"\s*:\s*"ping"') {
        $serverHeartbeatPingReceived = $true
        break
      }
    }
  }
  catch {
  }
  finally {
    $heartbeatCts.Dispose()
  }

  try {
    $null = $ws.CloseOutputAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", $cts.Token).GetAwaiter().GetResult()
  }
  catch {
  }
  $ws.Dispose()

  $wsResults = @(
    [pscustomobject]@{
      Check = "WS-Connect-Latency"
      Method = "WS"
      Path = "/api/debug/microflow/{id}"
      Expected = "<2000ms"
      Actual = "$connectedInMs ms"
      Passed = ($connectedInMs -lt 2000)
    },
    [pscustomobject]@{
      Check = "WS-Initial-Frames"
      Method = "WS"
      Path = "/api/debug/microflow/{id}"
      Expected = "session-status + state-sync"
      Actual = ($initialTypes -join ",")
      Passed = ($initialTypes -contains "session-status" -and $initialTypes -contains "state-sync")
    },
    [pscustomobject]@{
      Check = "WS-PingPong"
      Method = "WS"
      Path = "/api/debug/microflow/{id}"
      Expected = "pong"
      Actual = $(if ($pongReceived) { "pong" } else { "missing" })
      Passed = $pongReceived
    },
    [pscustomobject]@{
      Check = "WS-Heartbeat-30s"
      Method = "WS"
      Path = "/api/debug/microflow/{id}"
      Expected = "server ping within 35s"
      Actual = $(if ($serverHeartbeatPingReceived) { "ping-received" } else { "not-received" })
      Passed = $serverHeartbeatPingReceived
    }
  )

  $all = @($restResults + $wsResults)
  $all | Format-Table -AutoSize

  if ($all.Where({ -not $_.Passed }).Count -gt 0) {
    throw "One or more WS cutover checks failed."
  }
}
finally {
  Stop-Process -Id $appHost.Id -Force -ErrorAction SilentlyContinue
}
