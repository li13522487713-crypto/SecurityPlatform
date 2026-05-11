param(
  [string]$BaseUrl = "http://127.0.0.1:5002",
  [string]$Project = "src/backend/Atlas.AppHost/Atlas.AppHost.csproj",
  [int]$SessionCount = 200,
  [int]$DurationSeconds = 600,
  [int]$StartupTimeoutSeconds = 60,
  [int]$SampleIntervalSeconds = 5
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

function Receive-InitialMessages {
  param([System.Net.WebSockets.ClientWebSocket]$Socket, [int]$TimeoutMs = 2000)
  $cts = [System.Threading.CancellationTokenSource]::new()
  $cts.CancelAfter($TimeoutMs)
  $buf = New-Object byte[] 65536
  try {
    for ($i = 0; $i -lt 2; $i++) {
      $seg = [ArraySegment[byte]]::new($buf)
      $res = $Socket.ReceiveAsync($seg, $cts.Token).GetAwaiter().GetResult()
      if ($res.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
        break
      }
    }
  }
  catch {
  }
  finally {
    $cts.Dispose()
  }
}

$logDir = Join-Path $env:TEMP "securityplatform-service-logs"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$outLog = Join-Path $logDir "apphost.ws-loadtest.out.log"
$errLog = Join-Path $logDir "apphost.ws-loadtest.err.log"

$appHost = Start-Process -FilePath "dotnet" -ArgumentList @("run", "-c", "Debug", "--project", $Project) -WorkingDirectory (Get-Location) -RedirectStandardOutput $outLog -RedirectStandardError $errLog -WindowStyle Hidden -PassThru

$sockets = New-Object System.Collections.Generic.List[System.Net.WebSockets.ClientWebSocket]
$failed = 0
$connected = 0
$peakWorkingSetMb = 0.0
$peakPrivateMb = 0.0

try {
  if (-not (Wait-AppHostReady -TimeoutSeconds $StartupTimeoutSeconds)) {
    throw "AppHost did not become ready on port 5002 within $StartupTimeoutSeconds seconds."
  }

  $wsBase = Convert-ToWsUrl -Url $BaseUrl
  $connectStartedAt = Get-Date

  for ($i = 1; $i -le $SessionCount; $i++) {
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $sessionId = "loadtest-session-$i"
    $uri = [Uri]("$wsBase/api/debug/microflow/mf-loadtest?sessionId=$sessionId")
    $cts = [System.Threading.CancellationTokenSource]::new()
    $cts.CancelAfter([TimeSpan]::FromSeconds(8))
    try {
      $null = $socket.ConnectAsync($uri, $cts.Token).GetAwaiter().GetResult()
      $sockets.Add($socket) | Out-Null
      $connected += 1
      Receive-InitialMessages -Socket $socket
    }
    catch {
      $failed += 1
      try { $socket.Dispose() } catch { }
    }
    finally {
      $cts.Dispose()
    }
  }

  $connectLatencyMs = [int](((Get-Date) - $connectStartedAt).TotalMilliseconds)
  $startedAt = Get-Date
  while (((Get-Date) - $startedAt).TotalSeconds -lt $DurationSeconds) {
    $proc = Get-Process -Id $appHost.Id -ErrorAction SilentlyContinue
    if (-not $proc) {
      throw "AppHost process exited during load test."
    }
    $workingSetMb = [Math]::Round($proc.WorkingSet64 / 1MB, 2)
    $privateMb = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 2)
    if ($workingSetMb -gt $peakWorkingSetMb) { $peakWorkingSetMb = $workingSetMb }
    if ($privateMb -gt $peakPrivateMb) { $peakPrivateMb = $privateMb }

    foreach ($socket in $sockets) {
      if ($socket.State -ne [System.Net.WebSockets.WebSocketState]::Open) {
        continue
      }
      try {
        $payload = '{"type":"ping","data":{"sequence":1}}'
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $null = $socket.SendAsync([ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
      }
      catch {
      }
    }

    Start-Sleep -Seconds $SampleIntervalSeconds
  }

  $summary = [pscustomobject]@{
    SessionCount = $SessionCount
    Connected = $connected
    Failed = $failed
    DurationSeconds = $DurationSeconds
    ConnectLatencyMs = $connectLatencyMs
    PeakWorkingSetMb = $peakWorkingSetMb
    PeakPrivateMemoryMb = $peakPrivateMb
    Passed_StableConnections = ($connected -eq $SessionCount)
    Passed_MemoryUnder500Mb = ($peakPrivateMb -lt 500)
  }

  $summary | Format-List

  if (-not $summary.Passed_StableConnections) {
    throw "Not all sessions connected successfully."
  }
}
finally {
  foreach ($socket in $sockets) {
    try {
      if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $null = $socket.CloseOutputAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
      }
    }
    catch {
    }
    finally {
      try { $socket.Dispose() } catch { }
    }
  }
  Stop-Process -Id $appHost.Id -Force -ErrorAction SilentlyContinue
}
