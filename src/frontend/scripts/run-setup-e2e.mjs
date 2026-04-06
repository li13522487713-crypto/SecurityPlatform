import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(frontendRoot, "..", "..");
const platformHostRoot = path.resolve(repoRoot, "src", "backend", "Atlas.PlatformHost");
const appHostRoot = path.resolve(repoRoot, "src", "backend", "Atlas.AppHost");
const isWindows = process.platform === "win32";
const playwrightArgs = ["test", "-c", "playwright.setup.config.ts", ...process.argv.slice(2)];

/** @type {import("node:child_process").ChildProcess[]} */
const managedProcesses = [];

const services = [
  {
    name: "PlatformHost",
    command: isWindows ? "cmd.exe" : "dotnet",
    args: isWindows
      ? ["/d", "/s", "/c", "dotnet run --project ..\\backend\\Atlas.PlatformHost --no-launch-profile --no-build"]
      : ["run", "--project", "../backend/Atlas.PlatformHost", "--no-launch-profile", "--no-build"],
    cwd: frontendRoot,
    url: "http://127.0.0.1:5001/api/v1/setup/state",
    env: {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5001"
    }
  },
  {
    name: "AppHost",
    command: isWindows ? "cmd.exe" : "dotnet",
    args: isWindows
      ? ["/d", "/s", "/c", "dotnet run --project ..\\backend\\Atlas.AppHost --no-launch-profile --no-build"]
      : ["run", "--project", "../backend/Atlas.AppHost", "--no-launch-profile", "--no-build"],
    cwd: frontendRoot,
    url: "http://127.0.0.1:5002/api/v1/setup/state",
    env: {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5002"
      ,
      ATLAS_PLATFORM_CONFIG_ROOT: platformHostRoot,
      AppSetup__StateFilePath: path.resolve(appHostRoot, "app-setup-state.json")
    }
  },
  {
    name: "PlatformWeb",
    command: isWindows ? "cmd.exe" : "pnpm",
    args: isWindows
      ? ["/d", "/s", "/c", "pnpm run dev:platform-web"]
      : ["run", "dev:platform-web"],
    cwd: frontendRoot,
    url: "http://127.0.0.1:5180",
    env: {
      PLAYWRIGHT_E2E: "1"
    }
  },
  {
    name: "AppWeb",
    command: isWindows ? "cmd.exe" : "pnpm",
    args: isWindows
      ? ["/d", "/s", "/c", "pnpm run dev:app-web"]
      : ["run", "dev:app-web"],
    cwd: frontendRoot,
    url: "http://127.0.0.1:5181",
    env: {
      PLAYWRIGHT_E2E: "1"
    }
  }
];

function log(message) {
  process.stdout.write(`[run-setup-e2e] ${message}\n`);
}

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function waitForUrl(url, timeoutMs, serviceName) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < timeoutMs) {
    try {
      const response = await fetch(url, { method: "GET" });
      if (response.ok || (response.status >= 300 && response.status < 400)) {
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

  child.on("exit", (code, signal) => {
    if (code !== 0 && signal !== "SIGTERM") {
      log(`${service.name} 提前退出，code=${code ?? "null"} signal=${signal ?? "null"}`);
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
  for (const service of services) {
    spawnService(service);
  }

  for (const service of services) {
    await waitForUrl(service.url, 180_000, service.name);
  }
}

async function runPlaywrightPhase(extraArgs, displayName) {
  const exitCode = await new Promise((resolve, reject) => {
    const child = spawn(
      isWindows ? "cmd.exe" : "pnpm",
      isWindows
        ? ["/d", "/s", "/c", [".\\node_modules\\.bin\\playwright.cmd", ...playwrightArgs, ...extraArgs].join(" ")]
        : ["exec", "playwright", ...playwrightArgs, ...extraArgs],
      {
        cwd: frontendRoot,
        env: {
          ...process.env,
          PLAYWRIGHT_MANAGED_WEBSERVERS: "0"
        },
        stdio: "inherit"
      }
    );

    child.on("exit", (code) => resolve(code ?? 1));
    child.on("error", reject);
  });

  if (exitCode !== 0) {
    throw new Error(`${displayName} 失败，退出码 ${exitCode}`);
  }
}

async function main() {
  await runCommand(
    isWindows ? "cmd.exe" : "dotnet",
    isWindows
      ? ["/d", "/s", "/c", "dotnet build ..\\backend\\Atlas.PlatformHost --no-restore"]
      : ["build", "../backend/Atlas.PlatformHost", "--no-restore"],
    frontendRoot,
    {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5001"
    },
    "PlatformHost 构建"
  );

  await runCommand(
    isWindows ? "cmd.exe" : "dotnet",
    isWindows
      ? ["/d", "/s", "/c", "dotnet build ..\\backend\\Atlas.AppHost --no-restore"]
      : ["build", "../backend/Atlas.AppHost", "--no-restore"],
    frontendRoot,
    {
      ASPNETCORE_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: "http://127.0.0.1:5002"
    },
    "AppHost 构建"
  );

  await startServices();
  await runPlaywrightPhase(["--grep", "\\[platform\\]"], "平台 setup E2E");

  log("平台 setup 已完成，重启服务后继续执行应用 setup E2E");
  await cleanup();
  await startServices();
  await runPlaywrightPhase(["--grep", "\\[app\\]"], "应用 setup E2E");

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
