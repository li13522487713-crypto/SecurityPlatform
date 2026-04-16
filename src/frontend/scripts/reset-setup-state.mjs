import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(frontendRoot, "..", "..");

const cleanupTargets = [
  "src/backend/Atlas.PlatformHost/setup-state.json",
  "src/backend/Atlas.PlatformHost/appsettings.runtime.json",
  "src/backend/Atlas.PlatformHost/atlas.e2e.db",
  "src/backend/Atlas.PlatformHost/atlas.e2e.db-shm",
  "src/backend/Atlas.PlatformHost/atlas.e2e.db-wal",
  "src/backend/Atlas.PlatformHost/atlas.app.e2e.db",
  "src/backend/Atlas.PlatformHost/atlas.app.e2e.db-shm",
  "src/backend/Atlas.PlatformHost/atlas.app.e2e.db-wal",
  "src/backend/Atlas.PlatformHost/hangfire-platformhost.db",
  "src/backend/Atlas.PlatformHost/hangfire-platformhost.db-shm",
  "src/backend/Atlas.PlatformHost/hangfire-platformhost.db-wal",
  "src/backend/Atlas.AppHost/hangfire-apphost.db",
  "src/backend/Atlas.AppHost/hangfire-apphost.db-shm",
  "src/backend/Atlas.AppHost/hangfire-apphost.db-wal",
  "src/backend/Atlas.AppHost/app-setup-state.json"
];

const cleanupDirectories = [
  "runtime/instances",
  "runtime/artifacts",
  "src/frontend/test-results",
  "src/frontend/playwright-report"
];

const runtimeConfigPath = "src/backend/Atlas.PlatformHost/appsettings.runtime.json";
const appSetupStatePath = "src/backend/Atlas.AppHost/app-setup-state.json";

function stopRelevantProcesses() {
  if (process.platform !== "win32") {
    return;
  }

  const command = `
$ErrorActionPreference = 'SilentlyContinue'
$namedProcesses = Get-CimInstance Win32_Process | Where-Object {
  (($_.Name -eq 'dotnet.exe' -or $_.Name -eq 'dotnet') -and $_.CommandLine -and ($_.CommandLine -match 'Atlas\\.PlatformHost|Atlas\\.AppHost')) -or
  (($_.Name -eq 'node.exe' -or $_.Name -eq 'node') -and $_.CommandLine -and ($_.CommandLine -match 'dev:app-web|apps\\\\app-web|vite'))
}
$targetPorts = @(5001, 5002, 5181, 5182)
$portPids = @()
foreach ($port in $targetPorts) {
  $listeners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
  if ($listeners) {
    $portPids += ($listeners | Select-Object -ExpandProperty OwningProcess)
  }
}

$targetProcessIds = @()
if ($namedProcesses) {
  $targetProcessIds += ($namedProcesses | Select-Object -ExpandProperty ProcessId)
}
if ($portPids) {
  $targetProcessIds += $portPids
}

$targetProcessIds = $targetProcessIds | Where-Object { $_ -and $_ -ne $PID } | Select-Object -Unique
if ($targetProcessIds) {
  foreach ($targetProcessId in $targetProcessIds) {
    Stop-Process -Id $targetProcessId -Force -ErrorAction SilentlyContinue
  }
  Write-Host ('[reset-setup-state] stopped process count: ' + $targetProcessIds.Count)
}
`;
  const shells = ["C:\\Program Files\\PowerShell\\7\\pwsh.exe", "pwsh", "powershell"];
  for (const shell of shells) {
    try {
      execFileSync(shell, ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command], {
        stdio: "inherit"
      });
      return;
    } catch {
      // try next shell
    }
  }

  console.warn("[reset-setup-state] process shutdown probe failed, continuing with file cleanup.");
}

function removeFile(relativePath) {
  const absolutePath = path.resolve(repoRoot, relativePath);
  if (!absolutePath.startsWith(repoRoot)) {
    throw new Error(`Refusing to delete path outside repo: ${absolutePath}`);
  }

  if (!fs.existsSync(absolutePath)) {
    return;
  }

  try {
    fs.rmSync(absolutePath, { force: true });
    console.log(`[reset-setup-state] removed file: ${relativePath}`);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const code = typeof error === "object" && error !== null && "code" in error ? String(error.code) : "";
    if (code === "EPERM" || code === "EBUSY") {
      console.warn(`[reset-setup-state] skip locked file: ${relativePath} (${code})`);
      return;
    }

    throw new Error(`Failed to remove ${relativePath}: ${message}`);
  }
}

function clearDirectoryContents(relativePath) {
  const absolutePath = path.resolve(repoRoot, relativePath);
  if (!absolutePath.startsWith(repoRoot)) {
    throw new Error(`Refusing to clear path outside repo: ${absolutePath}`);
  }

  if (!fs.existsSync(absolutePath)) {
    return;
  }

  for (const entry of fs.readdirSync(absolutePath)) {
    fs.rmSync(path.join(absolutePath, entry), { recursive: true, force: true });
  }

  console.log(`[reset-setup-state] cleared directory: ${relativePath}`);
}

function writeRuntimeConfigPlaceholder() {
  const absolutePath = path.resolve(repoRoot, runtimeConfigPath);
  if (!absolutePath.startsWith(repoRoot)) {
    throw new Error(`Refusing to write path outside repo: ${absolutePath}`);
  }

  fs.writeFileSync(absolutePath, "{}\n", "utf8");
  console.log(`[reset-setup-state] reset file: ${runtimeConfigPath}`);
}

function writeAppSetupStatePlaceholder() {
  const absolutePath = path.resolve(repoRoot, appSetupStatePath);
  if (!absolutePath.startsWith(repoRoot)) {
    throw new Error(`Refusing to write path outside repo: ${absolutePath}`);
  }

  fs.writeFileSync(absolutePath, "{}\n", "utf8");
  console.log(`[reset-setup-state] reset file: ${appSetupStatePath}`);
}

stopRelevantProcesses();

for (const target of cleanupTargets) {
  removeFile(target);
}

for (const directory of cleanupDirectories) {
  clearDirectoryContents(directory);
}

writeRuntimeConfigPlaceholder();
writeAppSetupStatePlaceholder();

console.log("[reset-setup-state] setup test environment reset complete.");
