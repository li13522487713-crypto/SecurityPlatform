import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(frontendRoot, "..", "..");
const platformHostRoot = path.resolve(repoRoot, "src", "backend", "Atlas.PlatformHost");
const appHostRoot = path.resolve(repoRoot, "src", "backend", "Atlas.AppHost");
const platformHostProject = path.resolve(platformHostRoot, "Atlas.PlatformHost.csproj");
const appHostProject = path.resolve(appHostRoot, "Atlas.AppHost.csproj");
const e2eBuildRoot = path.resolve(repoRoot, "artifacts", "e2e");
const platformHostBuildDir = path.resolve(e2eBuildRoot, "Atlas.PlatformHost");
const appHostBuildDir = path.resolve(e2eBuildRoot, "Atlas.AppHost");
const platformHostDll = path.resolve(platformHostBuildDir, "Atlas.PlatformHost.dll");
const appHostDll = path.resolve(appHostBuildDir, "Atlas.AppHost.dll");
const isWindows = process.platform === "win32";
const playwrightArgs = ["test", "-c", "playwright.app.config.ts", ...process.argv.slice(2)];
const appWebMode = process.env.PLAYWRIGHT_APP_WEB_MODE === "direct" ? "direct" : "platform";
const appWebPort = appWebMode === "direct" ? 5182 : 5181;
const appWebScript = appWebMode === "direct" ? "dev:app-web:direct" : "dev:app-web";
const platformApiBase = "http://127.0.0.1:5001";
const appApiBase = "http://127.0.0.1:5002";
const platformDatabasePath = "Data Source=atlas.app.e2e.db";
const appDatabasePath = `Data Source=${path.resolve(frontendRoot, "../backend/Atlas.PlatformHost/atlas.app.e2e.db")}`;
const e2eConnectionString = `Data Source=${path.resolve(platformHostRoot, "atlas.app.e2e.db")}`;
const defaultTenantId = "00000000-0000-0000-0000-000000000001";
const defaultUsername = "admin";
const defaultPassword = "P@ssw0rd!";
const defaultAppName = "App E2E Regression";

/** @type {import("node:child_process").ChildProcess[]} */
const managedProcesses = [];
/** @type {Map<string, import("node:child_process").ChildProcess>} */
const serviceProcesses = new Map();

const services = [
  {
    name: "PlatformHost",
    command: "dotnet",
    args: [platformHostDll],
    cwd: platformHostRoot,
    url: "http://127.0.0.1:5001/internal/health/live",
    env: {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5001",
      Database__ConnectionString: e2eConnectionString,
      Database__DbType: "SQLite"
    }
  },
  {
    name: "AppHost",
    command: "dotnet",
    args: [appHostDll],
    cwd: appHostRoot,
    url: "http://127.0.0.1:5002/internal/health/live",
    env: {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5002",
      ATLAS_PLATFORM_CONFIG_ROOT: platformHostRoot,
      AppSetup__StateFilePath: path.resolve(appHostRoot, "app-setup-state.json"),
      Database__ConnectionString: e2eConnectionString,
      Database__DbType: "SQLite"
    }
  },
  {
    name: "PlatformWeb",
    command: isWindows ? "cmd.exe" : "pnpm",
    args: isWindows ? ["/d", "/s", "/c", "pnpm run dev:platform-web"] : ["run", "dev:platform-web"],
    cwd: frontendRoot,
    url: "http://127.0.0.1:5180",
    env: {
      PLAYWRIGHT_E2E: "1"
    }
  },
  {
    name: "AppWeb",
    command: isWindows ? "cmd.exe" : "pnpm",
    args: isWindows ? ["/d", "/s", "/c", `pnpm run ${appWebScript}`] : ["run", appWebScript],
    cwd: frontendRoot,
    url: `http://127.0.0.1:${appWebPort}`,
    env: {
      PLAYWRIGHT_E2E: "1",
      PLAYWRIGHT_APP_WEB_MODE: appWebMode,
      PLAYWRIGHT_APP_WEB_PORT: String(appWebPort)
    }
  }
];
const platformHostService = services[0];
const appHostService = services[1];
const platformWebService = services[2];
const appWebService = services[3];

function log(message) {
  process.stdout.write(`[run-app-e2e] ${message}\n`);
}

function ensureBuildDirectories() {
  fs.mkdirSync(platformHostBuildDir, { recursive: true });
  fs.mkdirSync(appHostBuildDir, { recursive: true });
}

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function waitForUrl(url, timeoutMs, serviceName) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < timeoutMs) {
    try {
      const response = await fetch(url, { method: "GET" });
      if (response.status < 500) {
        log(`${serviceName} 已就绪: ${url} (status ${response.status})`);
        return;
      }
    } catch {
      // ignore probe errors until timeout
    }

    await sleep(1000);
  }

  throw new Error(`${serviceName} 在 ${timeoutMs}ms 内未就绪: ${url}`);
}

async function waitForUrlUnavailable(url, timeoutMs, serviceName) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < timeoutMs) {
    try {
      const response = await fetch(url, { method: "GET" });
      if (!response.ok) {
        log(`${serviceName} 已停止响应: ${url} (status ${response.status})`);
        return;
      }
    } catch {
      log(`${serviceName} 已停止: ${url}`);
      return;
    }

    await sleep(1000);
  }

  throw new Error(`${serviceName} 在 ${timeoutMs}ms 内未停止: ${url}`);
}

async function readJsonSafe(response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function spawnService(service) {
  log(`启动 ${service.name}`);
  const child = spawn(service.command, service.args, {
    cwd: service.cwd,
    env: {
      ...process.env,
      ...service.env
    },
    stdio: "inherit"
  });

  managedProcesses.push(child);
  serviceProcesses.set(service.name, child);

  child.on("exit", (code, signal) => {
    if (code !== 0 && signal !== "SIGTERM") {
      log(`${service.name} 提前退出，code=${code ?? "null"} signal=${signal ?? "null"}`);
    }
    if (serviceProcesses.get(service.name) === child) {
      serviceProcesses.delete(service.name);
    }
  });

  return child;
}

async function runCommand(command, args, cwd, extraEnv, displayName) {
  await new Promise((resolve, reject) => {
    log(`执行 ${displayName}`);
    const child = spawn(command, args, {
      cwd,
      env: {
        ...process.env,
        ...extraEnv
      },
      stdio: "inherit"
    });

    child.on("exit", (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`${displayName} 失败，退出码 ${code ?? "null"}`));
    });

    child.on("error", reject);
  });
}

async function ensurePlatformSetupState() {
  const stateResp = await fetch(`${platformApiBase}/api/v1/setup/state`, { method: "GET" });
  const statePayload = await readJsonSafe(stateResp);
  if (statePayload?.success && statePayload?.data?.status === "Ready") {
    return;
  }

  const initializeResp = await fetch(`${platformApiBase}/api/v1/setup/initialize`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      database: {
        driverCode: "SQLite",
        mode: "raw",
        connectionString: platformDatabasePath
      },
      admin: {
        tenantId: defaultTenantId,
        username: defaultUsername,
        password: defaultPassword
      },
      roles: {
        selectedRoleCodes: ["SecurityAdmin", "AssetAdmin"]
      },
      organization: {
        departments: [{ name: "总部", code: "HQ", parentCode: null, sortOrder: 0 }],
        positions: [{ name: "系统管理员", code: "SYS_ADMIN", description: "系统配置与运维管理", sortOrder: 10 }]
      }
    })
  });
  const initializePayload = await readJsonSafe(initializeResp);
  const initialized =
    (initializeResp.ok && initializePayload?.success === true) ||
    initializePayload?.code === "ALREADY_CONFIGURED";

  if (!initialized) {
    throw new Error(`平台初始化失败: ${JSON.stringify(initializePayload)}`);
  }

  const startedAt = Date.now();
  while (Date.now() - startedAt < 45_000) {
    const response = await fetch(`${platformApiBase}/api/v1/setup/state`, { method: "GET" });
    const payload = await readJsonSafe(response);
    if (payload?.success && payload?.data?.status === "Ready") {
      return;
    }

    await sleep(1000);
  }

  throw new Error("平台初始化等待超时（45s）");
}

async function ensureAppSetupState() {
  const stateResp = await fetch(`${appApiBase}/api/v1/setup/state`, { method: "GET" });
  const statePayload = await readJsonSafe(stateResp);
  if (statePayload?.success && statePayload?.data?.appSetupCompleted === true) {
    return;
  }

  const initializeResp = await fetch(`${appApiBase}/api/v1/setup/initialize`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      database: {
        driverCode: "SQLite",
        mode: "raw",
        connectionString: appDatabasePath
      },
      admin: {
        appName: defaultAppName,
        adminUsername: defaultUsername
      },
      roles: {
        selectedRoleCodes: ["SecurityAdmin"]
      },
      organization: {
        departments: [{ name: "总部", code: "HQ", parentCode: null, sortOrder: 0 }],
        positions: [{ name: "系统管理员", code: "SYS_ADMIN", description: "系统配置与运维管理", sortOrder: 10 }]
      }
    })
  });
  const initializePayload = await readJsonSafe(initializeResp);
  const initialized =
    (initializeResp.ok && initializePayload?.success === true) ||
    initializePayload?.code === "ALREADY_CONFIGURED";

  if (!initialized) {
    throw new Error(`应用初始化失败: ${JSON.stringify(initializePayload)}`);
  }

  const startedAt = Date.now();
  while (Date.now() - startedAt < 45_000) {
    const response = await fetch(`${appApiBase}/api/v1/setup/state`, { method: "GET" });
    const payload = await readJsonSafe(response);
    if (payload?.success && payload?.data?.appSetupCompleted === true) {
      return;
    }

    await sleep(1000);
  }

  throw new Error("应用初始化等待超时（45s）");
}

function terminateProcessTree(child) {
  if (!child.pid) {
    return;
  }

  if (isWindows) {
    spawn("taskkill", ["/pid", String(child.pid), "/t", "/f"], {
      stdio: "ignore"
    });
    return;
  }

  child.kill("SIGTERM");
}

async function restartService(service) {
  log(`重启 ${service.name}`);
  const current = serviceProcesses.get(service.name);
  let shouldSpawnAfterStop = true;
  if (current) {
    terminateProcessTree(current);
    try {
      await waitForUrlUnavailable(service.url, 30_000, service.name);
    } catch (error) {
      // 端口仍存活通常意味着外部已有同服务进程在监听（例如 IDE 启动），
      // 这时复用该进程继续执行 E2E，避免启动流程直接失败。
      shouldSpawnAfterStop = false;
      const message = error instanceof Error ? error.message : String(error);
      log(`${service.name} 未完全停止，复用现有监听服务: ${message}`);
    }
  }

  if (shouldSpawnAfterStop) {
    spawnService(service);
  }
  await waitForUrl(service.url, 180_000, service.name);
}

async function cleanup() {
  for (const child of managedProcesses.splice(0).reverse()) {
    terminateProcessTree(child);
  }

  await sleep(2000);
}

let cleanedUp = false;
async function cleanupOnce() {
  if (cleanedUp) {
    return;
  }

  cleanedUp = true;
  await cleanup();
}

async function startServices() {
  spawnService(platformHostService);
  await waitForUrl(platformHostService.url, 180_000, platformHostService.name);
  await ensurePlatformSetupState();
  await restartService(platformHostService);

  spawnService(appHostService);
  await waitForUrl(appHostService.url, 180_000, appHostService.name);
  await ensureAppSetupState();
  await restartService(appHostService);

  spawnService(platformWebService);
  spawnService(appWebService);
  await waitForUrl(platformWebService.url, 180_000, platformWebService.name);
  await waitForUrl(appWebService.url, 180_000, appWebService.name);
}

async function runPlaywright() {
  const exitCode = await new Promise((resolve, reject) => {
    const child = spawn(
      isWindows ? "cmd.exe" : "pnpm",
      isWindows
        ? ["/d", "/s", "/c", [".\\node_modules\\.bin\\playwright.cmd", ...playwrightArgs].join(" ")]
        : ["exec", "playwright", ...playwrightArgs],
      {
        cwd: frontendRoot,
        env: {
          ...process.env,
          PLAYWRIGHT_MANAGED_WEBSERVERS: "0",
          PLAYWRIGHT_APP_WEB_MODE: appWebMode,
          PLAYWRIGHT_APP_WEB_PORT: String(appWebPort)
        },
        stdio: "inherit"
      }
    );

    child.on("exit", (code) => resolve(code ?? 1));
    child.on("error", reject);
  });

  if (exitCode !== 0) {
    throw new Error(`应用级 E2E 失败，退出码 ${exitCode}`);
  }
}

async function main() {
  log(`应用前端模式: ${appWebMode} (http://127.0.0.1:${appWebPort})`);
  ensureBuildDirectories();

  await runCommand(
    "dotnet",
    [
      "build",
      platformHostProject,
      "--no-restore",
      "-m:1",
      "-nr:false",
      "/p:UseAppHost=false",
      `/p:OutDir=${platformHostBuildDir}${path.sep}`
    ],
    frontendRoot,
    {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5001"
    },
    "PlatformHost 构建"
  );

  await runCommand(
    "dotnet",
    [
      "build",
      appHostProject,
      "--no-restore",
      "-m:1",
      "-nr:false",
      "/p:UseAppHost=false",
      `/p:OutDir=${appHostBuildDir}${path.sep}`
    ],
    frontendRoot,
    {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5002"
    },
    "AppHost 构建"
  );

  await startServices();
  await runPlaywright();
  await cleanupOnce();
  process.exit(0);
}

process.on("SIGINT", async () => {
  await cleanupOnce();
  process.exit(130);
});

process.on("SIGTERM", async () => {
  await cleanupOnce();
  process.exit(143);
});

main().catch(async (error) => {
  console.error(error);
  await cleanupOnce();
  process.exit(1);
});
