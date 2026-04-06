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
  "runtime/artifacts"
];

function stopRelevantProcesses() {
  if (process.platform !== "win32") {
    return;
  }

  const command = `
$ErrorActionPreference = 'SilentlyContinue'
$processes = Get-CimInstance Win32_Process | Where-Object {
  (($_.Name -eq 'dotnet.exe' -or $_.Name -eq 'dotnet') -and $_.CommandLine -and ($_.CommandLine -match 'Atlas\\.PlatformHost|Atlas\\.AppHost')) -or
  (($_.Name -eq 'node.exe' -or $_.Name -eq 'node') -and $_.CommandLine -and ($_.CommandLine -match 'dev:platform-web|dev:app-web|apps\\\\platform-web|apps\\\\app-web|vite'))
}
if ($processes) {
  $processes | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Write-Host ('[reset-setup-state] stopped process count: ' + $processes.Count)
}
`;
  try {
    execFileSync("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command], {
      stdio: "inherit"
    });
  } catch {
    console.warn("[reset-setup-state] process shutdown probe failed, continuing with file cleanup.");
  }
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

stopRelevantProcesses();

for (const target of cleanupTargets) {
  removeFile(target);
}

for (const directory of cleanupDirectories) {
  clearDirectoryContents(directory);
}

console.log("[reset-setup-state] setup test environment reset complete.");
