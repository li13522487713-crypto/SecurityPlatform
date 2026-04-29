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
  results.push({
    id: "future-round-blockers",
    status: "pending",
    summary: "R2-R5 生产化能力仍待后续轮次实现。",
    details: [
      "R2: P0 安全、ownership、mock purge、save conflict。",
      "R3: rollback/cast/listOperation 真实 executor、schema migration、connector stub、property forms。",
      "R4: trueParallel、Expression API/editor、Step Debug。",
      "R5: E2E、性能基线、production gate 终版。"
    ]
  });
  const productionConfig = readWorkspaceFile("src/backend/Atlas.AppHost/appsettings.Production.json", root);
  results.push({
    id: "production-rest-safe-defaults",
    status: productionConfig.includes("\"AllowRealHttp\":false") && productionConfig.includes("\"AllowPrivateNetwork\":false") ? "pass" : "fail",
    summary: "生产配置默认禁止真实 HTTP 与私网访问。"
  });
  return results;
}

const commandResults = [
  run("pnpm", ["exec", "tsx", "../../scripts/verify-microflow-node-capability-matrix.ts"], resolve(root, "src/frontend")),
  run("pnpm", ["exec", "tsx", "../../scripts/verify-microflow-action-descriptor-naming.ts"], resolve(root, "src/frontend")),
  run("pnpm", ["exec", "tsx", "../../scripts/verify-microflow-executor-coverage.ts"], resolve(root, "src/frontend"))
];
const results = [...staticChecks(), ...commandResults];
const conclusion = summarizeResults(results);
const summary = {
  generatedAt: new Date().toISOString(),
  round: "R1",
  conclusion,
  results
};

writeJson(resolve(outputDir, "production-gate-summary.json"), summary);
writeText(resolve(outputDir, "production-gate-summary.md"), [
  "# Microflow Production Gate Summary (R1)",
  "",
  `- GeneratedAt: ${summary.generatedAt}`,
  `- Conclusion: **${conclusion}**`,
  "",
  "| Check | Status | Summary |",
  "|---|---|---|",
  ...results.map(result => `| ${result.id} | ${result.status} | ${result.summary.replace(/\|/gu, "/")} |`),
  "",
  "## Pending Future Rounds",
  "",
  "- R2：P0 安全与生产配置阻断项。",
  "- R3：真实 executor、命名迁移、connector stub、property panel。",
  "- R4：trueParallel、Expression Editor、Step Debug。",
  "- R5：E2E、性能基线、Production Gate 终版。"
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
