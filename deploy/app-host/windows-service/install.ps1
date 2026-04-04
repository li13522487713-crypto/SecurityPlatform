#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Install Atlas AppHost as a Windows Service.

.PARAMETER AppKey
    Unique application key for this instance.

.PARAMETER Port
    HTTP port the AppHost listens on.

.PARAMETER InstallPath
    Path to the published AppHost directory.

.PARAMETER InstanceHome
    Root directory for instance data (config, logs, data).

.PARAMETER ServiceName
    Windows service name. Defaults to atlas-apphost-<AppKey>.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AppKey,

    [Parameter(Mandatory)]
    [int]$Port,

    [Parameter(Mandatory)]
    [string]$InstallPath,

    [Parameter()]
    [string]$InstanceHome = "",

    [Parameter()]
    [string]$ServiceName = ""
)

$ErrorActionPreference = 'Stop'

if (-not $ServiceName) {
    $ServiceName = "atlas-apphost-$AppKey"
}

if (-not $InstanceHome) {
    $InstanceHome = Join-Path $InstallPath "instance-data"
}

$exePath = Join-Path $InstallPath "Atlas.AppHost.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "AppHost executable not found: $exePath"
    exit 1
}

$configPath = Join-Path $InstanceHome "config" "appinstance.json"
if (-not (Test-Path (Split-Path $configPath -Parent))) {
    New-Item -ItemType Directory -Path (Split-Path $configPath -Parent) -Force | Out-Null
}

$displayName = "Atlas AppHost ($AppKey)"
$description = "Atlas AppHost instance for application '$AppKey' on port $Port."

$binPath = "`"$exePath`""
$envVars = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ASPNETCORE_URLS=http://+:$Port",
    "AppInstance__ConfigPath=$configPath",
    "APP_INSTANCE_HOME=$InstanceHome"
)

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service '$ServiceName' already exists. Stopping and removing..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating Windows Service: $ServiceName"
New-Service `
    -Name $ServiceName `
    -BinaryPathName $binPath `
    -DisplayName $displayName `
    -Description $description `
    -StartupType Automatic

foreach ($envVar in $envVars) {
    $parts = $envVar -split '=', 2
    [System.Environment]::SetEnvironmentVariable($parts[0], $parts[1], [System.EnvironmentVariableTarget]::Machine)
}

Write-Host "Setting recovery options (restart on failure)..."
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Host "Starting service '$ServiceName'..."
Start-Service -Name $ServiceName

$svc = Get-Service -Name $ServiceName
Write-Host "Service '$ServiceName' status: $($svc.Status)"
Write-Host ""
Write-Host "Installation complete."
Write-Host "  Service Name : $ServiceName"
Write-Host "  Executable   : $exePath"
Write-Host "  Port         : $Port"
Write-Host "  Instance Home: $InstanceHome"
Write-Host "  Config Path  : $configPath"
