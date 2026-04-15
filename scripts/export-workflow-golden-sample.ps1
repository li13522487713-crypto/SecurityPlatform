param(
    [Parameter(Mandatory = $true)]
    [string]$SampleName,

    [Parameter(Mandatory = $true)]
    [string]$WorkflowId,

    [string]$ExecutionId,

    [ValidateSet("draft", "published")]
    [string]$Source = "draft",

    [string]$BaseUrl = "http://localhost:5002",
    [string]$TenantId = "00000000-0000-0000-0000-000000000001",
    [string]$Username = "admin",
    [string]$Password = "P@ssw0rd!",
    [string]$OutputRoot = "D:\Code\Web_SaaS_Backend\SecurityPlatform\artifacts\workflow-golden"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function New-ApiHeaders {
    param(
        [string]$AccessToken,
        [string]$CsrfToken,
        [string]$IdempotencyKey
    )

    $headers = @{
        "X-Tenant-Id" = $TenantId
    }

    if ($AccessToken) {
        $headers["Authorization"] = "Bearer $AccessToken"
    }

    if ($CsrfToken) {
        $headers["X-CSRF-TOKEN"] = $CsrfToken
    }

    if ($IdempotencyKey) {
        $headers["Idempotency-Key"] = $IdempotencyKey
    }

    return $headers
}

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers,
        [object]$Body
    )

    $invokeParams = @{
        Method      = $Method
        Uri         = $Url
        Headers     = $Headers
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $invokeParams["Body"] = ($Body | ConvertTo-Json -Depth 20)
    }

    return Invoke-RestMethod @invokeParams
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $json = $Value | ConvertTo-Json -Depth 50
    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
}

Write-Host "登录并获取鉴权令牌..." -ForegroundColor Cyan
$tokenResponse = Invoke-JsonApi -Method "POST" -Url "$BaseUrl/api/v1/auth/token" -Headers @{ "X-Tenant-Id" = $TenantId } -Body @{
    username = $Username
    password = $Password
}
$accessToken = $tokenResponse.data.accessToken
if (-not $accessToken) {
    throw "未获取到 accessToken。"
}

$csrfResponse = Invoke-JsonApi -Method "GET" -Url "$BaseUrl/api/v1/secure/antiforgery" -Headers (New-ApiHeaders -AccessToken $accessToken -CsrfToken "" -IdempotencyKey "") -Body $null
$csrfToken = $csrfResponse.data.token
if (-not $csrfToken) {
    throw "未获取到 CSRF token。"
}

$sampleRoot = Join-Path $OutputRoot $SampleName
$atlasRoot = Join-Path $sampleRoot "atlas"
if (-not (Test-Path -LiteralPath $atlasRoot)) {
    New-Item -ItemType Directory -Path $atlasRoot -Force | Out-Null
}

Write-Host "导出工作流详情..." -ForegroundColor Cyan
$detail = Invoke-JsonApi -Method "GET" -Url "$BaseUrl/api/v2/workflows/$WorkflowId?source=$Source" -Headers (New-ApiHeaders -AccessToken $accessToken -CsrfToken "" -IdempotencyKey "") -Body $null
Write-JsonFile -Path (Join-Path $atlasRoot "detail.json") -Value $detail

$canvasJson = $detail.data.canvasJson
if ($canvasJson) {
    try {
        $schema = $canvasJson | ConvertFrom-Json -Depth 50
        Write-JsonFile -Path (Join-Path $atlasRoot "schema.json") -Value $schema
    } catch {
        Set-Content -LiteralPath (Join-Path $atlasRoot "schema.raw.json") -Value $canvasJson -Encoding UTF8
    }
}

if (-not $ExecutionId) {
    Write-Host "未提供 ExecutionId，尝试触发一次运行..." -ForegroundColor Yellow
    $runResponse = Invoke-JsonApi -Method "POST" -Url "$BaseUrl/api/v2/workflows/$WorkflowId/run" -Headers (New-ApiHeaders -AccessToken $accessToken -CsrfToken $csrfToken -IdempotencyKey ([guid]::NewGuid().ToString())) -Body @{
        source    = $Source
        inputsJson = "{}"
    }
    $ExecutionId = $runResponse.data.executionId
}

if ($ExecutionId) {
    Write-Host "导出执行过程与 Trace: $ExecutionId" -ForegroundColor Cyan
    $process = Invoke-JsonApi -Method "GET" -Url "$BaseUrl/api/v2/workflows/executions/$ExecutionId/process" -Headers (New-ApiHeaders -AccessToken $accessToken -CsrfToken "" -IdempotencyKey "") -Body $null
    $trace = Invoke-JsonApi -Method "GET" -Url "$BaseUrl/api/v2/workflows/executions/$ExecutionId/trace" -Headers (New-ApiHeaders -AccessToken $accessToken -CsrfToken "" -IdempotencyKey "") -Body $null
    $debugView = Invoke-JsonApi -Method "GET" -Url "$BaseUrl/api/v2/workflows/executions/$ExecutionId/debug-view" -Headers (New-ApiHeaders -AccessToken $accessToken -CsrfToken "" -IdempotencyKey "") -Body $null

    Write-JsonFile -Path (Join-Path $atlasRoot "process.json") -Value $process
    Write-JsonFile -Path (Join-Path $atlasRoot "trace.json") -Value $trace
    Write-JsonFile -Path (Join-Path $atlasRoot "debug-view.json") -Value $debugView

    $nodeExecutions = @($process.data.nodeExecutions)
    foreach ($node in $nodeExecutions) {
        if (-not $node.nodeKey) {
            continue
        }

        $nodeDetail = Invoke-JsonApi -Method "GET" -Url "$BaseUrl/api/v2/workflows/executions/$ExecutionId/nodes/$($node.nodeKey)" -Headers (New-ApiHeaders -AccessToken $accessToken -CsrfToken "" -IdempotencyKey "") -Body $null
        Write-JsonFile -Path (Join-Path $atlasRoot ("node-{0}.json" -f $node.nodeKey)) -Value $nodeDetail
    }
}

$notesPath = Join-Path $atlasRoot "notes.md"
@"
# $SampleName

- workflowId: $WorkflowId
- executionId: $ExecutionId
- source: $Source
- exportedAt: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
- baseUrl: $BaseUrl

"@ | Set-Content -LiteralPath $notesPath -Encoding UTF8

Write-Host "导出完成: $atlasRoot" -ForegroundColor Green
