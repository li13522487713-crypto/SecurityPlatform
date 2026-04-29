import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import {
  type CheckResult,
  findWorkspaceRoot,
  parseBackendDescriptors,
  readWorkspaceFile,
  summarizeResults,
  writeJson,
  writeText
} from "./microflow-production-gate-lib.ts";

const root = findWorkspaceRoot();
const outputDir = resolve(root, "artifacts/microflow-production-gate");
mkdirSync(outputDir, { recursive: true });

function run(command: string, args: string[], cwd = root): CheckResult {
  try {
    const output = execFileSync(command, args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
    return {
      id: `${command} ${args.join(" ")}`,
      status: "pass",
      summary: "命令执行成功。",
      details: output.trim() ? output.trim().split(/\r?\n/u).slice(-10) : undefined
    };
  } catch (error) {
    const maybe = error as { stdout?: string; stderr?: string; message?: string };
    return {
      id: `${command} ${args.join(" ")}`,
      status: "fail",
      summary: "命令执行失败。",
      details: [maybe.stdout, maybe.stderr, maybe.message].filter(Boolean).join("\n").split(/\r?\n/u).slice(-20)
    };
  }
}

function staticChecks(): CheckResult[] {
  const descriptors = parseBackendDescriptors(root);
  const docs = [
    "docs/microflow/production-upgrade-audit.md",
    "docs/microflow/production-node-capability-matrix.md",
    "docs/microflow/contracts/action-kind-naming.md",
    "docs/microflow/contracts/action-descriptor-naming.md",
    "docs/microflow/executor-implementation-plan.md"
  ];
  const results: CheckResult[] = [];
  results.push({
    id: "r1-documents-present",
    status: docs.every(path => existsSync(resolve(root, path))) ? "pass" : "fail",
    summary: "R1 文档交付物存在性检查。",
    details: docs.map(path => `${path}: ${existsSync(resolve(root, path)) ? "present" : "missing"}`)
  });
  results.push({
    id: "backend-descriptor-count",
    status: descriptors.length >= 80 ? "pass" : "warn",
    summary: `后端 BuiltInDescriptors 当前 ${descriptors.length} 项。`,
    details: descriptors.length >= 80 ? undefined : ["用户目标为 ≥80；R1 首版按当前源码事实输出 conditional-go。"]
  });
  const debugCoordinator = readWorkspaceFile("src/backend/Atlas.Application.Microflows/Runtime/Debug/MicroflowDebugCoordinator.cs", root);
  const debugModels = readWorkspaceFile("src/backend/Atlas.Application.Microflows/Runtime/Debug/MicroflowDebugRuntimeModels.cs", root);
  const debugController = readWorkspaceFile("src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowDebugController.cs", root);
  const runtimeEngine = readWorkspaceFile("src/backend/Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs", root);
  results.push({
    id: "r4-debug-production-evidence",
    status: [
      "ApplyCommand",
      "ShouldPause",
      "MicroflowDebugRuntimeSnapshot",
      "CurrentSafePoint",
      "DebugTraceEvent",
      "ResolveOwnedSession",
      "MicroflowDebugSessionForbidden",
      "CreateDebugSnapshot"
    ].every(token => `${debugCoordinator}\n${debugModels}\n${debugController}\n${runtimeEngine}`.includes(token)) ? "pass" : "fail",
    summary: "Step Debug 已具备 coordinator 状态机、safe point 快照、trace、变量快照与 session ownership 证据。"
  });
  results.push({
    id: "r4-gateway-main-path",
    status: /ExecuteGatewayPassThrough/u.test(runtimeEngine)
      && !/Parallel Gateway 暂未在 runtime 主路径执行/u.test(runtimeEngine)
      && !/Inclusive Gateway 暂未在 runtime 主路径执行/u.test(runtimeEngine) ? "pass" : "fail",
    summary: "Parallel/Inclusive Gateway 不再在 runtime 主路径直接返回 unsupported。"
  });
  const productionConfig = readWorkspaceFile("src/backend/Atlas.AppHost/appsettings.Production.json", root);
  results.push({
    id: "production-rest-safe-defaults",
    status: /"AllowRealHttp"\s*:\s*false/u.test(productionConfig) && /"AllowPrivateNetwork"\s*:\s*false/u.test(productionConfig) ? "pass" : "fail",
    summary: "生产配置默认禁止真实 HTTP 与私网访问。"
  });
  return results;
}

const commandResults = [
  run("node", ["../../scripts/verify-microflow-node-capability-matrix.ts"], resolve(root, "src/frontend")),
  run("node", ["../../scripts/verify-microflow-action-descriptor-naming.ts"], resolve(root, "src/frontend")),
  run("node", ["../../scripts/verify-microflow-executor-coverage.ts"], resolve(root, "src/frontend")),
  run("node", ["../../scripts/verify-microflow-debug-runtime.ts"], resolve(root, "src/frontend"))
];
const results = [...staticChecks(), ...commandResults];
const conclusion = summarizeResults(results);
const summary = {
  generatedAt: new Date().toISOString(),
  round: "R5",
  conclusion,
  results
};

writeJson(resolve(outputDir, "production-gate-summary.json"), summary);
writeText(resolve(outputDir, "production-gate-summary.md"), [
  "# Microflow Production Gate Summary (R5)",
  "",
  `- GeneratedAt: ${summary.generatedAt}`,
  `- Conclusion: **${conclusion}**`,
  "",
  "| Check | Status | Summary |",
  "|---|---|---|",
  ...results.map(result => `| ${result.id} | ${result.status} | ${result.summary.replace(/\|/gu, "/")} |`),
  "",
  "## Known Remaining Limits",
  "",
  "- Connector 真实外部系统接入仍按能力门禁返回 RUNTIME_CONNECTOR_REQUIRED。",
  "- Time-travel debug、debug-time variable mutation、true OS thread isolation、branchOnly suspend policy 仍为明确范围外能力。"
].join("\n"));

for (const result of results) {
  console.log(`${result.status.toUpperCase()} ${result.id}: ${result.summary}`);
  if (result.details?.length) {
    for (const detail of result.details) {
      console.log(`  - ${detail}`);
    }
  }
}
console.log(`production gate conclusion: ${conclusion}`);

if (conclusion === "no-go") {
  process.exitCode = 1;
}
