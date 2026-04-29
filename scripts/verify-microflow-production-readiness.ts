import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { join, relative, resolve } from "node:path";
import { spawnSync } from "node:child_process";

type Status = "pass" | "fail" | "warn";

type Check = {
  name: string;
  status: Status;
  details: string;
};

const root = process.cwd();
const checks: Check[] = [];
const warnings: string[] = [];
const artifactRoot = resolve(root, "artifacts/microflow-release/round61");
const apiBaseUrl = (process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002").replace(/\/+$/u, "");
const skipBuilds = process.env.MICROFLOW_READINESS_SKIP_BUILDS === "1";
const skipLiveHealth = process.env.MICROFLOW_READINESS_SKIP_LIVE_HEALTH === "1";

mkdirSync(artifactRoot, { recursive: true });

function add(name: string, ok: boolean, details = ""): void {
  checks.push({ name, status: ok ? "pass" : "fail", details });
}

function warn(name: string, details: string): void {
  checks.push({ name, status: "warn", details });
  warnings.push(`${name}: ${details}`);
}

function read(path: string): string {
  return readFileSync(resolve(root, path), "utf8");
}

function exists(path: string): boolean {
  return existsSync(resolve(root, path));
}

function run(name: string, command: string[], cwd = root): void {
  if (skipBuilds && /build|test/i.test(command.join(" "))) {
    warn(name, "MICROFLOW_READINESS_SKIP_BUILDS=1，构建类检查本次跳过。");
    return;
  }

  const started = Date.now();
  const result = spawnSync(command[0], command.slice(1), {
    cwd,
    shell: process.platform === "win32",
    encoding: "utf8",
    timeout: 10 * 60 * 1000,
    maxBuffer: 16 * 1024 * 1024,
  });
  const outputPath = join(artifactRoot, `${name.replace(/[^a-z0-9]+/giu, "-").toLowerCase()}.log`);
  writeFileSync(outputPath, `${result.stdout ?? ""}\n${result.stderr ?? ""}`);
  checks.push({
    name,
    status: result.status === 0 ? "pass" : "fail",
    details: `${command.join(" ")} exited ${result.status}; ${Date.now() - started}ms; log=${relative(root, outputPath).replaceAll("\\", "/")}`,
  });
}

function walk(dir: string, files: string[] = []): string[] {
  if (!existsSync(dir)) {
    return files;
  }

  for (const entry of readdirSync(dir)) {
    if (["node_modules", "dist", "bin", "obj", ".git", "artifacts"].includes(entry)) {
      continue;
    }

    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      walk(full, files);
      continue;
    }

    if (/\.(cs|ts|tsx|js|mjs|json|md|http|yaml|yml)$/u.test(entry)) {
      files.push(full);
    }
  }

  return files;
}

async function getJson(path: string): Promise<unknown> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      "X-Workspace-Id": process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace",
      "X-Tenant-Id": process.env.MICROFLOW_TENANT_ID ?? "demo-tenant",
      "X-User-Id": process.env.MICROFLOW_USER_ID ?? "round61-readiness",
      "X-Trace-Id": `round61-${Date.now()}`,
    },
  });
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }
  return await response.json();
}

function asObject(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? value as Record<string, unknown> : {};
}

async function liveHealth(name: string, path: string, accept: string[]): Promise<void> {
  if (skipLiveHealth) {
    warn(name, "MICROFLOW_READINESS_SKIP_LIVE_HEALTH=1，HTTP health 检查本次跳过。");
    return;
  }

  try {
    const body = asObject(await getJson(path));
    const data = asObject(body.data);
    const status = String(data.status ?? "");
    add(name, body.success === true && accept.includes(status), `status=${status}`);
  } catch (error) {
    add(name, false, error instanceof Error ? error.message : String(error));
  }
}

async function main(): Promise<void> {
  run("frontend production build", ["pnpm", "--dir", "src/frontend", "run", "build:app-web"]);
  run("backend release build", ["dotnet", "build", "Atlas.SecurityPlatform.slnx", "-c", "Release"]);
  run("no mock production", ["npx", "tsx", "scripts/verify-microflow-production-no-mock.ts"]);

  await liveHealth("api health", "/api/v1/microflows/health", ["ok", "healthy"]);
  await liveHealth("storage health", "/api/v1/microflows/storage/health", ["ok", "healthy", "degraded"]);
  await liveHealth("metadata health", "/api/v1/microflow-metadata/health", ["ok", "healthy", "degraded"]);
  await liveHealth("runtime health", "/api/v1/microflows/runtime/health", ["healthy", "degraded"]);

  const productionConfigPath = "src/backend/Atlas.AppHost/appsettings.Production.json";
  add("production appsettings exists", exists(productionConfigPath));
  const productionConfig = exists(productionConfigPath) ? read(productionConfigPath) : "";
  add("appsettings production safety", productionConfig.includes('"AllowRealHttp": false') && productionConfig.includes('"AllowPrivateNetwork": false') && productionConfig.includes('"SeedEnabled": false') && productionConfig.includes('"EnableInternalDebugApi": false'));
  add("runtime limits config", productionConfig.includes('"MaxSteps": 5000') && productionConfig.includes('"RunTimeoutSeconds": 300') && productionConfig.includes('"MaxConcurrentRuns": 20'));
  add("retention config", productionConfig.includes('"RunSessionRetentionDays": 30') && productionConfig.includes('"MaxRunSessionsPerResource": 1000'));
  add("audit config", productionConfig.includes('"AuditLogEnabled": true'));
  add("observability config", productionConfig.includes('"StructuredLoggingEnabled": true'));

  const docs = [
    "docs/microflow/release/round61-production-readiness.md",
    "docs/microflow/release/inner-test-readiness.md",
    "docs/microflow/release/known-limitations.md",
    "docs/microflow/release/ops-runbook.md",
    "docs/microflow/release/deployment-guide.md",
    "docs/microflow/release/rollback-guide.md",
    "docs/microflow/release/backup-restore-guide.md",
    "docs/microflow/release/monitoring-alerting-guide.md",
    "docs/microflow/release/security-configuration.md",
    "docs/microflow/release/release-checklist.md",
    "docs/microflow/e2e/round60-full-e2e-report.md",
  ];
  for (const doc of docs) {
    add(`doc exists: ${doc}`, exists(doc));
  }

  const allFiles = walk(root);
  const flowgramPersistence = allFiles
    .map(file => ({ rel: relative(root, file).replaceAll("\\", "/"), text: readFileSync(file, "utf8") }))
    .filter(item => /src\/backend\/.*Microflow.*\.(cs|json)$/u.test(item.rel))
    .filter(item => !item.rel.endsWith("MicroflowResourceService.cs") && !item.rel.endsWith("MicroflowSchemaJsonHelper.cs") && !item.rel.endsWith("MicroflowValidationService.cs"))
    .filter(item => /workflowJson|flowgram/iu.test(item.text))
    .map(item => item.rel);
  add("FlowGram JSON persistence grep", flowgramPersistence.length === 0, flowgramPersistence.join(", "));

  const httpFile = "src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http";
  add("MicroflowBackend.http readiness section", exists(httpFile) && read(httpFile).includes("Round 61 - Production Readiness"));
  add("Swagger / OpenAPI documented", exists("docs/microflow/contracts/openapi-draft.yaml") || read("src/backend/Atlas.AppHost/Program.cs").includes("UseOpenApi"));
  add("database schema verify documented", exists("docs/microflow/release/deployment-guide.md") && read("docs/microflow/release/deployment-guide.md").includes("MicroflowSchemaMigration"));
  add("permission smoke verify documented", exists("docs/microflow/release/security-configuration.md") && read("docs/microflow/release/security-configuration.md").includes("MICROFLOW_PERMISSION_DENIED"));

  const failed = checks.filter(check => check.status === "fail");
  const readinessStatus = failed.length === 0
    ? warnings.length > 0 ? "conditional-go" : "go"
    : "no-go";
  const report = {
    readinessStatus,
    failedChecks: failed,
    warnings,
    checks,
    generatedAt: new Date().toISOString(),
    apiBaseUrl,
  };
  const reportPath = join(artifactRoot, "readiness-summary.json");
  writeFileSync(reportPath, JSON.stringify(report, null, 2));
  writeFileSync(join(artifactRoot, "readiness-summary.md"), [
    `# Microflow Round 61 Production Readiness`,
    ``,
    `- readinessStatus: ${readinessStatus}`,
    `- failedChecks: ${failed.length}`,
    `- warnings: ${warnings.length}`,
    `- reportPath: ${relative(root, reportPath).replaceAll("\\", "/")}`,
    ``,
    ...checks.map(check => `- ${check.status.toUpperCase()} ${check.name}: ${check.details}`),
    ``,
  ].join("\n"));

  console.log(JSON.stringify({
    readinessStatus,
    failedChecks: failed.map(check => check.name),
    warnings,
    reportPath: relative(root, reportPath).replaceAll("\\", "/"),
  }, null, 2));

  if (readinessStatus === "no-go") {
    process.exit(1);
  }
}

void main();
