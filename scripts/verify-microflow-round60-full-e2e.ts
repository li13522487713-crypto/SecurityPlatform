import { spawn } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";

type CaseStatus = "pass" | "fail" | "blocked" | "skippedKnownLimitation";
type Severity = "blocker" | "critical" | "major" | "minor" | "known limitation";

type MatrixCase = {
  caseId: string;
  title: string;
  area: string;
  command: string[];
  precondition: string;
  expected: string;
  severity: Severity;
};

type CaseResult = MatrixCase & {
  actual: string;
  status: CaseStatus;
  durationMs: number;
  evidence: string[];
};

const root = process.cwd();
const artifactRoot = resolve(root, "artifacts/microflow-e2e/round60");
const evidenceDirs = [
  "screenshots",
  "traces",
  "http-responses",
  "backend-logs",
  "runtime-run-sessions",
  "runtime-trace-samples",
];
const apiBaseUrl = (process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002").replace(/\/+$/u, "");
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "demo-user";
const roundPrefix = process.env.MICROFLOW_ROUND60_PREFIX ?? "R60_E2E_";
const resetEnabled = process.env.MICROFLOW_ROUND60_RESET !== "0";
const cleanupAfter = process.env.MICROFLOW_ROUND60_CLEANUP === "1";

const environment = {
  apiBaseUrl,
  workspaceId,
  tenantId,
  userId,
  databaseProvider: process.env.Database__Provider ?? process.env.DATABASE_PROVIDER ?? "SqlSugar/SQLite by AppHost configuration",
  nodeVersion: process.version,
  platform: process.platform,
};

const matrix: MatrixCase[] = [
  {
    caseId: "R60-FE-ADAPTER-MODES",
    title: "前端 HTTP Adapter 模式与 no mock/local fallback",
    area: "frontend-adapter",
    command: ["pnpm", "--dir", "src/frontend", "run", "verify:microflow-adapter-modes"],
    precondition: "前端依赖已安装",
    expected: "app-web 通过统一 adapter bundle 切换模式，生产路径禁止 mock/local fallback",
    severity: "critical",
  },
  {
    caseId: "R60-FE-NO-PROD-MOCK",
    title: "生产构建禁用微流 mock/local",
    area: "frontend-adapter",
    command: ["pnpm", "--dir", "src/frontend", "run", "verify:microflow-no-production-mock"],
    precondition: "前端依赖已安装",
    expected: "生产策略默认 http，禁止 enableMockFallback",
    severity: "critical",
  },
  {
    caseId: "R60-FE-ERROR-HANDLING",
    title: "前端 HTTP 错误态映射",
    area: "frontend-error-state",
    command: ["pnpm", "--dir", "src/frontend", "run", "verify:microflow-http-error-handling"],
    precondition: "前端依赖已安装",
    expected: "401/403/404/409/422/5xx/network 均映射为 MicroflowApiException 与用户可见错误态",
    severity: "major",
  },
  {
    caseId: "R60-RESOURCE-SCHEMA",
    title: "Resource / Schema 真实 HTTP E2E",
    area: "resource-schema",
    command: ["node", "src/frontend/scripts/verify-microflow-resource-schema-integration.mjs"],
    precondition: "AppHost 正在监听 MICROFLOW_API_BASE_URL",
    expected: "health/list/create/schema load/save/rename/favorite/duplicate/archive/restore/delete/404/schema invalid/version conflict 全部通过",
    severity: "blocker",
  },
  {
    caseId: "R60-METADATA-SELECTOR",
    title: "Metadata / Selector 后端 catalog",
    area: "metadata-selector",
    command: ["npx", "tsx", "scripts/verify-microflow-metadata-integration.ts"],
    precondition: "AppHost 正在监听 MICROFLOW_API_BASE_URL",
    expected: "metadata catalog、entity、enumeration、microflow refs 与 404 错误 envelope 可用",
    severity: "critical",
  },
  {
    caseId: "R60-VALIDATION-PROBLEM",
    title: "Validation / ProblemPanel 后端 issues",
    area: "validation-problem-panel",
    command: ["npx", "tsx", "scripts/verify-microflow-validation-integration.ts"],
    precondition: "AppHost 正在监听 MICROFLOW_API_BASE_URL",
    expected: "edit/save/publish/testRun 与 missing start、fieldPath、metadata issue 均返回正确 issue",
    severity: "critical",
  },
  {
    caseId: "R60-PUBLISH-VERSION-REFERENCES-TESTRUN",
    title: "Publish / Version / References / TestRun / Debug 综合链路",
    area: "publish-version-references-testrun",
    command: ["npx", "tsx", "scripts/verify-microflow-publish-version-references-testrun-debug-integration.ts"],
    precondition: "AppHost 正在监听 MICROFLOW_API_BASE_URL",
    expected: "publish、versions、compare、impact、references、test-run、get run、trace、cancel 全部通过",
    severity: "blocker",
  },
  { caseId: "R60-RUNTIME-PLAN", title: "ExecutionPlanLoader", area: "runtime-basic", command: ["npx", "tsx", "scripts/verify-microflow-execution-plan-loader.ts"], precondition: "AppHost 可用", expected: "inline/current/version plan 生成且不包含 FlowGram JSON", severity: "critical" },
  { caseId: "R60-FLOW-NAVIGATOR", title: "FlowNavigator", area: "runtime-basic", command: ["npx", "tsx", "scripts/verify-microflow-flow-navigator.ts"], precondition: "AppHost 可用", expected: "Start/End、Decision、Merge、unsupported、loop、maxSteps dry-run 正确", severity: "critical" },
  { caseId: "R60-VARIABLE-STORE", title: "VariableStore", area: "runtime-variable", command: ["npx", "tsx", "scripts/verify-microflow-variable-store.ts"], precondition: "AppHost 可用", expected: "参数、系统变量、action outputs、loop scope、latestError/latestHttpResponse snapshot 正确", severity: "major" },
  { caseId: "R60-EXPRESSION-EVALUATOR", title: "ExpressionEvaluator", area: "runtime-expression", command: ["npx", "tsx", "scripts/verify-microflow-expression-evaluator.ts"], precondition: "AppHost 可用", expected: "表达式 parse/type/eval 与 Runtime integration 正确", severity: "major" },
  { caseId: "R60-METADATA-RESOLVER-ENTITY-ACCESS", title: "MetadataResolver / EntityAccess Stub", area: "runtime-metadata", command: ["npx", "tsx", "scripts/verify-microflow-metadata-resolver-entity-access.ts"], precondition: "AppHost 可用", expected: "metadata resolve、member path、EntityAccess stub 与 diagnostic API 正确", severity: "major" },
  { caseId: "R60-TRANSACTION-MANAGER", title: "TransactionManager", area: "runtime-transaction", command: ["npx", "tsx", "scripts/verify-microflow-transaction-manager.ts"], precondition: "AppHost 可用", expected: "begin/commit/rollback/savepoint/action transaction log 与 RunSession summary 正确", severity: "major" },
  { caseId: "R60-ACTION-EXECUTORS", title: "ActionExecutor 全量覆盖", area: "runtime-action", command: ["npx", "tsx", "scripts/verify-microflow-action-executors-full-coverage.ts"], precondition: "AppHost 可用", expected: "前端 actionKind 与后端 registry/support matrix/validation/runtime 对齐", severity: "critical" },
  { caseId: "R60-LOOP", title: "Loop / Break / Continue", area: "runtime-loop", command: ["npx", "tsx", "scripts/verify-microflow-loop-runtime.ts"], precondition: "AppHost 可用", expected: "iterable/empty/while/break/continue/scope/trace 正确", severity: "critical" },
  { caseId: "R60-CALLSTACK", title: "CallMicroflow / CallStack", area: "runtime-callstack", command: ["npx", "tsx", "scripts/verify-microflow-callstack-runtime.ts"], precondition: "AppHost 可用", expected: "参数、返回、child trace、recursion、max depth 与 error propagation 正确", severity: "critical" },
  { caseId: "R60-REST-LOG", title: "RestCall / LogMessage", area: "runtime-rest-log", command: ["npx", "tsx", "scripts/verify-microflow-restcall-logmessage-runtime.ts"], precondition: "AppHost 可用", expected: "REST mock/security/response variables/latestHttpResponse 与 RuntimeLog 正确", severity: "critical" },
  { caseId: "R60-ERROR-HANDLING", title: "ErrorHandling 四模式", area: "runtime-error-handling", command: ["npx", "tsx", "scripts/verify-microflow-error-handling-runtime.ts"], precondition: "源码可读；部分断言为静态契约", expected: "rollback/customWithRollback/customWithoutRollback/continue 语义与文档/.http 对齐", severity: "critical" },
  { caseId: "R60-HARDENING", title: "Trace / Cancel / Timeout / Limits hardening", area: "runtime-hardening", command: ["npx", "tsx", "scripts/verify-microflow-runtime-hardening.ts"], precondition: "AppHost 可用", expected: "cancel、trace/log 落库、maxSteps、maxIterations、REST security/timeout 保护存在", severity: "critical" },
];

function ensureArtifacts(): void {
  mkdirSync(artifactRoot, { recursive: true });
  for (const dir of evidenceDirs) {
    mkdirSync(join(artifactRoot, dir), { recursive: true });
  }
}

function isBlocked(output: string): boolean {
  return /ECONNREFUSED|fetch failed|Failed to fetch|connection refused|无法连接|No such file or directory|not recognized as/i.test(output);
}

function runCommand(command: string[], env: NodeJS.ProcessEnv): Promise<{ code: number | null; output: string; durationMs: number }> {
  const started = Date.now();
  return new Promise((resolveResult) => {
    const child = spawn(command[0], command.slice(1), {
      cwd: root,
      env,
      shell: process.platform === "win32",
    });
    const chunks: string[] = [];
    child.stdout.on("data", (chunk) => chunks.push(String(chunk)));
    child.stderr.on("data", (chunk) => chunks.push(String(chunk)));
    child.on("error", (error) => {
      chunks.push(error.stack ?? error.message);
      resolveResult({ code: 1, output: chunks.join(""), durationMs: Date.now() - started });
    });
    child.on("close", (code) => {
      resolveResult({ code, output: chunks.join(""), durationMs: Date.now() - started });
    });
  });
}

async function api(method: string, path: string, body?: unknown): Promise<any> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method,
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-Workspace-Id": workspaceId,
      "X-Tenant-Id": tenantId,
      "X-User-Id": userId,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const payload = text ? JSON.parse(text) : undefined;
  if (!response.ok || typeof payload?.success !== "boolean") {
    throw new Error(`${method} ${path} failed: HTTP ${response.status} ${text}`);
  }
  return payload;
}

function makeSchema(id: string, name: string, nodeCount = 2): any {
  const objects = Array.from({ length: Math.max(2, nodeCount) }, (_, index) => {
    if (index === 0) {
      return { id: "start", kind: "startEvent", officialType: "Microflows$StartEvent" };
    }
    if (index === nodeCount - 1) {
      return { id: "end", kind: "endEvent", officialType: "Microflows$EndEvent" };
    }
    return { id: `log-${index}`, kind: "actionActivity", officialType: "Microflows$ActionActivity", action: { id: `log-${index}-action`, kind: "logMessage", template: { text: `node ${index}` } } };
  });
  const flows = objects.slice(0, -1).map((object, index) => ({
    id: `f-${object.id}-${objects[index + 1].id}`,
    kind: "sequence",
    originObjectId: object.id,
    destinationObjectId: objects[index + 1].id,
  }));
  return {
    schemaVersion: "1.0.0",
    id,
    stableId: id,
    name,
    displayName: name,
    moduleId: "verify-module",
    moduleName: "Verify",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { id: "root-collection", objects, flows },
    flows: [],
    variables: {},
    validation: { issues: [] },
  };
}

async function cleanupRound60Resources(): Promise<string[]> {
  const deleted: string[] = [];
  for (const keyword of [roundPrefix, "E2E_MF_"]) {
    const payload = await api("GET", `/api/microflows?workspaceId=${encodeURIComponent(workspaceId)}&keyword=${encodeURIComponent(keyword)}&pageIndex=1&pageSize=200`);
    const items = payload.data?.items ?? [];
    for (const item of items) {
      const name = String(item.name ?? item.displayName ?? "");
      if (!name.startsWith(roundPrefix) && !name.startsWith("E2E_MF_")) {
        continue;
      }
      await api("DELETE", `/api/microflows/${encodeURIComponent(String(item.id))}`);
      deleted.push(String(item.id));
    }
  }
  return deleted;
}

async function seedRound60Resources(): Promise<string[]> {
  const seeded: string[] = [];
  for (const sample of [
    { suffix: "Blank", nodes: 2 },
    { suffix: "ObjectCrud", nodes: 6 },
    { suffix: "ListSample", nodes: 8 },
    { suffix: "LoopSample", nodes: 8 },
    { suffix: "CallParent", nodes: 5 },
    { suffix: "RestCall", nodes: 4 },
    { suffix: "ErrorHandling", nodes: 5 },
    { suffix: "PublishReference", nodes: 3 },
    { suffix: "LargeGraph", nodes: 120 },
  ]) {
    const name = `${roundPrefix}${sample.suffix}_${Date.now()}`;
    const created = await api("POST", "/api/microflows", {
      workspaceId,
      input: {
        name,
        displayName: name,
        description: "Round60 E2E seed/reset fixture.",
        moduleId: "verify-module",
        moduleName: "Verify",
        tags: ["round60", "e2e", sample.suffix],
        parameters: [],
        returnType: { kind: "void" },
      },
    });
    const id = String(created.data.id);
    await api("PUT", `/api/microflows/${id}/schema`, {
      saveReason: "round60 seed",
      schema: makeSchema(id, name, sample.nodes),
    });
    seeded.push(id);
  }
  writeFileSync(join(artifactRoot, "seeded-resources.json"), JSON.stringify({ prefix: roundPrefix, seeded }, null, 2));
  return seeded;
}

async function getCommitHash(): Promise<string> {
  const result = await runCommand(["git", "rev-parse", "--short", "HEAD"], process.env);
  return result.code === 0 ? result.output.trim() : "unknown";
}

function writeReports(results: CaseResult[], startedAt: string, durationMs: number, commitHash: string): void {
  const passCount = results.filter((item) => item.status === "pass").length;
  const failCount = results.filter((item) => item.status === "fail").length;
  const blockedCount = results.filter((item) => item.status === "blocked").length;
  const knownLimitationCount = results.filter((item) => item.status === "skippedKnownLimitation").length;
  const failedCases = results.filter((item) => item.status === "fail" || item.status === "blocked");
  const summary = {
    round: 60,
    title: "Microflow Full E2E Regression / Inner Test Readiness",
    startedAt,
    endedAt: new Date().toISOString(),
    durationMs,
    passCount,
    failCount,
    blockedCount,
    knownLimitationCount,
    environment: { ...environment, commitHash },
    reportPath: join(artifactRoot, "e2e-summary.md"),
    results,
  };
  writeFileSync(join(artifactRoot, "e2e-summary.json"), JSON.stringify(summary, null, 2));
  writeFileSync(join(artifactRoot, "failed-cases.json"), JSON.stringify(failedCases, null, 2));
  writeFileSync(join(artifactRoot, "coverage-matrix.json"), JSON.stringify(results, null, 2));

  const lines = [
    "# Round60 Microflow Full E2E Summary",
    "",
    `- Started: ${startedAt}`,
    `- Duration: ${Math.round(durationMs / 1000)}s`,
    `- Environment: ${apiBaseUrl}`,
    `- Commit: ${commitHash}`,
    `- Result: pass=${passCount}, fail=${failCount}, blocked=${blockedCount}, knownLimitation=${knownLimitationCount}`,
    "",
    "## Cases",
    "",
    "| Case | Area | Status | Severity | Duration | Evidence |",
    "| --- | --- | --- | --- | ---: | --- |",
    ...results.map((item) => `| ${item.caseId} | ${item.area} | ${item.status} | ${item.severity} | ${Math.round(item.durationMs / 1000)}s | ${item.evidence.map((value) => value.replaceAll("\\", "/")).join("<br>")} |`),
    "",
    "## Failed / Blocked",
    "",
    failedCases.length === 0 ? "None." : failedCases.map((item) => `- ${item.caseId}: ${item.actual.split("\n").slice(-6).join(" ")}`).join("\n"),
  ];
  writeFileSync(join(artifactRoot, "e2e-summary.md"), `${lines.join("\n")}\n`);
}

async function main(): Promise<void> {
  ensureArtifacts();
  const startedAt = new Date().toISOString();
  const started = Date.now();
  const commitHash = await getCommitHash();
  const seedMessages: string[] = [];

  try {
    if (resetEnabled) {
      const deleted = await cleanupRound60Resources();
      seedMessages.push(`reset deleted ${deleted.length} prefixed resources`);
      const seeded = await seedRound60Resources();
      seedMessages.push(`seeded ${seeded.length} prefixed resources`);
    }
  } catch (error) {
    seedMessages.push(`seed/reset blocked: ${error instanceof Error ? error.message : String(error)}`);
  }

  const env = {
    ...process.env,
    MICROFLOW_API_BASE_URL: apiBaseUrl,
    MICROFLOW_WORKSPACE_ID: workspaceId,
    MICROFLOW_TENANT_ID: tenantId,
    MICROFLOW_USER_ID: userId,
    MICROFLOW_ROUND60_PREFIX: roundPrefix,
  };

  const results: CaseResult[] = [];
  for (const item of matrix) {
    const commandResult = await runCommand(item.command, env);
    const evidencePath = join(artifactRoot, "traces", `${item.caseId}.log`);
    writeFileSync(evidencePath, commandResult.output);
    const status: CaseStatus = commandResult.code === 0
      ? "pass"
      : isBlocked(commandResult.output)
        ? "blocked"
        : "fail";
    results.push({
      ...item,
      actual: commandResult.output.trim().slice(-4000),
      status,
      durationMs: commandResult.durationMs,
      evidence: [evidencePath],
    });
    console.log(`${status.toUpperCase()} ${item.caseId} ${item.title}`);
  }

  try {
    if (cleanupAfter) {
      const deleted = await cleanupRound60Resources();
      seedMessages.push(`cleanup deleted ${deleted.length} prefixed resources`);
    }
  } catch (error) {
    seedMessages.push(`cleanup blocked: ${error instanceof Error ? error.message : String(error)}`);
  }

  writeFileSync(join(artifactRoot, "seed-reset.log"), `${seedMessages.join("\n")}\n`);
  writeReports(results, startedAt, Date.now() - started, commitHash);

  const failed = results.filter((item) => item.status === "fail" || item.status === "blocked");
  console.log(`Round60 summary: pass=${results.length - failed.length}, fail=${failed.filter((item) => item.status === "fail").length}, blocked=${failed.filter((item) => item.status === "blocked").length}, report=${join(artifactRoot, "e2e-summary.md")}`);
  if (failed.length > 0) {
    process.exitCode = 1;
  }
}

main().catch((error) => {
  ensureArtifacts();
  const message = error instanceof Error ? error.stack ?? error.message : String(error);
  writeFileSync(join(artifactRoot, "round60-fatal.log"), message);
  console.error(message);
  process.exitCode = 1;
});
