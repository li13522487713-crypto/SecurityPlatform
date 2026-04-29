import { existsSync } from "node:fs";
import { resolve } from "node:path";
import {
  collectFrontendMicroflowRegistry,
  parseBackendDescriptors,
  parseFrontendActionKinds,
  parseMarkdownMatrixActionKinds,
  parseRegisteredPropertyFormKeys,
  readWorkspaceFile,
  findWorkspaceRoot,
  type CheckResult
} from "./microflow-production-gate-lib.ts";

const root = findWorkspaceRoot();
const matrixPath = "docs/microflow/production-node-capability-matrix.md";
const results: CheckResult[] = [];

function add(id: string, status: CheckResult["status"], summary: string, details?: string[]): void {
  results.push({ id, status, summary, details });
}

function sorted(values: Iterable<string>): string[] {
  return [...values].sort((a, b) => a.localeCompare(b));
}

function difference(left: Iterable<string>, right: Set<string>): string[] {
  return sorted([...left].filter(value => !right.has(value)));
}

const descriptors = parseBackendDescriptors(root);
const descriptorKinds = new Set(descriptors.map(descriptor => descriptor.actionKind));
const frontendKinds = parseFrontendActionKinds(root);
const propertyFormKeys = parseRegisteredPropertyFormKeys(root);
const frontendSnapshot = collectFrontendMicroflowRegistry(root);

add(
  "backend-descriptors-present",
  descriptors.length > 0 ? "pass" : "fail",
  `后端 BuiltInDescriptors 采集到 ${descriptors.length} 个 actionKind。`
);

add(
  "backend-descriptor-volume",
  descriptors.length >= 80 ? "pass" : "warn",
  `R1 目标为 ≥80 个 actionKind；当前源码采集到 ${descriptors.length} 个。`,
  descriptors.length >= 80 ? undefined : ["当前仓库 BuiltInDescriptors 尚未达到用户目标体量，production gate 保持 conditional-go。"]
);

const matrixExists = existsSync(resolve(root, matrixPath));
add(
  "matrix-document-exists",
  matrixExists ? "pass" : "fail",
  `${matrixPath} ${matrixExists ? "存在" : "缺失"}。`
);

const matrixKinds = matrixExists
  ? parseMarkdownMatrixActionKinds(readWorkspaceFile(matrixPath, root))
  : new Set<string>();
const missingInMatrix = difference(descriptorKinds, matrixKinds);
add(
  "matrix-covers-backend-descriptors",
  missingInMatrix.length === 0 ? "pass" : "fail",
  "节点能力矩阵覆盖全部后端 descriptor actionKind。",
  missingInMatrix.length === 0 ? undefined : [`矩阵缺失：${missingInMatrix.join(", ")}`]
);

const frontendMissing = difference(frontendKinds, descriptorKinds);
add(
  "frontend-action-kinds-covered-by-backend",
  frontendMissing.length === 0 ? "pass" : "fail",
  "前端 registry 暴露的 actionKind 均有后端 descriptor。",
  frontendMissing.length === 0 ? undefined : [`后端缺失：${frontendMissing.join(", ")}`]
);

const modeledOnlyExpected = ["rollback", "cast", "listOperation"];
const supportedButModeled = descriptors
  .filter(descriptor => modeledOnlyExpected.includes(descriptor.actionKind))
  .filter(descriptor => descriptor.executor === "ConfiguredMicroflowActionExecutor" || descriptor.supportLevel === "ModeledOnlyConverted")
  .map(descriptor => descriptor.actionKind);
const implementedR3Executors = descriptors
  .filter(descriptor => modeledOnlyExpected.includes(descriptor.actionKind))
  .filter(descriptor => descriptor.executor !== "ConfiguredMicroflowActionExecutor" && descriptor.supportLevel !== "ModeledOnlyConverted")
  .map(descriptor => descriptor.actionKind);
add(
  "r1-known-modeled-only-blockers-marked",
  supportedButModeled.length + implementedR3Executors.length === modeledOnlyExpected.length ? "pass" : "fail",
  "rollback/cast/listOperation 在 R1 被识别为 modeled-only blocker；R3 后允许升级为真实 executor。",
  [
    `modeled-only：${supportedButModeled.join(", ") || "(none)"}`,
    `implemented：${implementedR3Executors.join(", ") || "(none)"}`
  ]
);

const connectorWithoutCapability = descriptors
  .filter(descriptor => descriptor.runtimeCategory === "ConnectorBacked")
  .filter(descriptor => !descriptor.connectorCapability)
  .map(descriptor => descriptor.actionKind);
add(
  "connector-backed-has-capability",
  connectorWithoutCapability.length === 0 ? "pass" : "fail",
  "ConnectorBacked descriptor 均声明 capability gate。",
  connectorWithoutCapability.length === 0 ? undefined : [`缺 capability：${connectorWithoutCapability.join(", ")}`]
);

add(
  "property-form-registry-readable",
  "pass",
  `property form registry 可解析；当前显式注册 key 数量 ${propertyFormKeys.size}。`,
  propertyFormKeys.size === 0 ? ["R1 仅建立矩阵，专用 forms 将在 R3 补齐。"] : undefined
);

add(
  "frontend-action-collector",
  frontendSnapshot.actions.length > 0 ? "pass" : "fail",
  `前端 action registry collector 采集到 ${frontendSnapshot.actions.length} 个 action。`
);

add(
  "frontend-node-collector",
  frontendSnapshot.nodes.length > 0 ? "pass" : "fail",
  `前端 node registry collector 采集到 ${frontendSnapshot.nodes.length} 个节点。`
);

for (const result of results) {
  const tag = result.status.toUpperCase();
  console.log(`${tag} ${result.id}: ${result.summary}`);
  for (const detail of result.details ?? []) {
    console.log(`  - ${detail}`);
  }
}

const failed = results.filter(result => result.status === "fail");
if (failed.length > 0) {
  console.error(`\nverify-microflow-node-capability-matrix: ${failed.length} checks failed.`);
  process.exit(1);
}

console.log("\nverify-microflow-node-capability-matrix: PASS");
