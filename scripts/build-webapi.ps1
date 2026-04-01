param(
  [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnetHome = Join-Path $repoRoot ".dotnet"
$homeRoot = Join-Path $dotnetHome "home"
$appData = Join-Path $homeRoot "AppData\\Roaming"
$localAppData = Join-Path $homeRoot "AppData\\Local"
$nugetPackages = Join-Path $dotnetHome ".nuget\\packages"
$nugetConfig = Join-Path $appData "NuGet\\NuGet.Config"
$webApiProject = Join-Path $repoRoot "src\\backend\\Atlas.WebApi\\Atlas.WebApi.csproj"

New-Item -ItemType Directory -Force -Path $homeRoot | Out-Null
New-Item -ItemType Directory -Force -Path $appData | Out-Null
New-Item -ItemType Directory -Force -Path $localAppData | Out-Null
New-Item -ItemType Directory -Force -Path $nugetPackages | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $nugetConfig -Parent) | Out-Null

if (-not (Test-Path $nugetConfig)) {
  @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
'@ | Set-Content -Path $nugetConfig -Encoding UTF8
}

$env:SystemDrive = "C:"
$env:ProgramFiles = "C:\Program Files"
[System.Environment]::SetEnvironmentVariable("ProgramFiles(x86)", "C:\Program Files (x86)")
$env:ALLUSERSPROFILE = "C:\ProgramData"
$env:USERPROFILE = $homeRoot
$env:APPDATA = $appData
$env:LOCALAPPDATA = $localAppData
$env:ComSpec = "C:\Windows\System32\cmd.exe"
$env:windir = "C:\Windows"
$env:SystemRoot = "C:\Windows"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_HOME = $dotnetHome
$env:NUGET_PACKAGES = $nugetPackages

$args = @("build", $webApiProject, "-m:1", "/v:m")
if ($NoRestore) {
  $args += "--no-restore"
} else {
  $args += @(
    "--configfile", $nugetConfig,
    "/p:RestoreDisableParallel=true",
    "/p:RestoreUseStaticGraphEvaluation=false",
    "/p:RestorePackagesPath=$nugetPackages",
    "/p:RestoreFallbackFolders=",
    "/p:DisableImplicitNuGetFallbackFolder=true"
  )
}

Write-Host "Running: dotnet $($args -join ' ')" -ForegroundColor Cyan
& dotnet @args
exit $LASTEXITCODE
