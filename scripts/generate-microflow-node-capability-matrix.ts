import { resolve } from "node:path";
import {
  collectFrontendMicroflowRegistry,
  findWorkspaceRoot,
  parseBackendDescriptors,
  writeText
} from "./microflow-production-gate-lib.ts";

const root = findWorkspaceRoot();
const backend = parseBackendDescriptors(root);
const frontend = collectFrontendMicroflowRegistry(root);
const actionByKind = Object.fromEntries(frontend.actions.map(action => [action.actionKind, action]));
const nodesByActionKind = frontend.nodes.reduce<Record<string, typeof frontend.nodes>>((acc, node) => {
  if (node.actionKind) {
    acc[node.actionKind] = [...(acc[node.actionKind] ?? []), node];
  }
  return acc;
}, {});
const propertyFormKeys = frontend.propertyForms.map(form => form.key);

function esc(value: unknown): string {
  const text = value === undefined || value === null || value === "" ? "-" : String(value);
  return text.replace(/\|/gu, "/").replace(/\r?\n/gu, " ");
}

function productionDecision(descriptor: ReturnType<typeof parseBackendDescriptors>[number], formPath: string): string {
  if (["rollback", "cast", "listOperation"].includes(descriptor.actionKind)) {
    return "blocked-until-R3-real-executor";
  }
  if (descriptor.runtimeCategory === "ConnectorBacked") {
    return "blocked-until-capability-available";
  }
  if (descriptor.runtimeCategory === "ExplicitUnsupported") {
    return "not-for-production-runtime";
  }
  if (descriptor.runtimeCategory === "RuntimeCommand") {
    return "server-command-preview-only";
  }
  if (formPath === "missing" && descriptor.runtimeCategory === "ServerExecutable") {
    return "allowed-with-generic-form";
  }
  return "allowed";
}

function targetRound(descriptor: ReturnType<typeof parseBackendDescriptors>[number]): string {
  if (["rollback", "cast", "listOperation"].includes(descriptor.actionKind)) {
    return "R3";
  }
  if (descriptor.actionKind.includes("Gateway")) {
    return "R4";
  }
  if (descriptor.runtimeCategory === "ConnectorBacked") {
    return "R3";
  }
  if (descriptor.runtimeCategory === "ExplicitUnsupported") {
    return "R5-known-limitations";
  }
  return "R1/R2 gate";
}

function transactionBehavior(descriptor: ReturnType<typeof parseBackendDescriptors>[number]): string {
  if (descriptor.producesTransaction) {
    return "writes-transaction-or-object-store";
  }
  if (descriptor.runtimeCategory === "ConnectorBacked") {
    return "external-side-effect-gated";
  }
  if (descriptor.runtimeCategory === "RuntimeCommand") {
    return "client-side-effect";
  }
  return "read-or-variable-only";
}

function outputBehavior(descriptor: ReturnType<typeof parseBackendDescriptors>[number]): string {
  if (descriptor.producesRuntimeCommand) {
    return "runtime-command";
  }
  if (descriptor.producesVariables) {
    return "produces-variable";
  }
  return "no-variable-output";
}

const rows = backend.map(descriptor => {
  const action = actionByKind[descriptor.actionKind];
  const nodes = nodesByActionKind[descriptor.actionKind] ?? [];
  const firstNode = nodes[0];
  const formKey = firstNode?.propertyFormKey ?? (action ? `activity:${action.legacyActivityType}` : `activity:${descriptor.actionKind}`);
  const formRegistered = propertyFormKeys.includes(formKey);
  const formPath = formRegistered
    ? `registered:${formKey}`
    : firstNode?.propertyFormKey
      ? `declared:${firstNode.propertyFormKey}`
      : "missing";
  const toolboxVisible = firstNode
    ? firstNode.engineSupportLevel === "unsupported" ? "disabled" : "visible"
    : action
      ? action.availability === "hidden" ? "hidden-action" : "action-only"
      : "backend-only";
  const validationSupport = firstNode?.validationSupport ? "frontend-validation" : "backend-validation-only";
  return [
    descriptor.actionKind,
    descriptor.registryCategory,
    action?.officialType ?? descriptor.schemaType,
    descriptor.schemaType,
    firstNode?.registryKey ?? action?.actionKind ?? "-",
    action?.legacyActivityType ?? firstNode?.activityType ?? "-",
    toolboxVisible,
    formPath,
    validationSupport,
    descriptor.executor,
    `${descriptor.runtimeCategory}/${descriptor.supportLevel}`,
    transactionBehavior(descriptor),
    outputBehavior(descriptor),
    descriptor.runtimeCategory === "ConnectorBacked" || descriptor.runtimeCategory === "ExplicitUnsupported" ? "stops-or-error-handler" : "normal/error-flow",
    descriptor.realExecution ? "trace-frame" : descriptor.runtimeCategory === "ConnectorBacked" ? "connector-diagnostic" : "unsupported-diagnostic",
    descriptor.connectorCapability ?? "-",
    descriptor.supportLevel === "Supported" ? "-" : descriptor.supportLevel,
    productionDecision(descriptor, formPath),
    targetRound(descriptor)
  ];
});

const byDecision = rows.reduce<Record<string, number>>((acc, row) => {
  const key = row[17];
  acc[key] = (acc[key] ?? 0) + 1;
  return acc;
}, {});

const doc = [
  "# Microflow 生产节点能力矩阵",
  "",
  "> 轮次：R1 — 节点矩阵与生产门禁骨架  ",
  "> 来源：前端 node registry、action registry、property form registry 与后端 `MicroflowActionExecutorRegistry.BuiltInDescriptors()`。  ",
  "> 生成方式：`pnpm run microflow:verify:matrix` 会重新解析本表与源码并校验覆盖一致性。",
  "",
  "## 摘要",
  "",
  `- 后端 descriptor 数量：${backend.length}`,
  `- 前端 action registry 数量：${frontend.actions.length}`,
  `- 前端 node registry actionKind 数量：${Object.keys(nodesByActionKind).length}`,
  `- 显式 property form 注册数量：${propertyFormKeys.length}`,
  `- 生产决策分布：${Object.entries(byDecision).map(([key, count]) => `${key}=${count}`).join(", ")}`,
  "",
  "## 字段说明",
  "",
  "| 字段 | 含义 |",
  "|---|---|",
  "| actionKind | canonical actionKind，禁止旧别名进入 schema |",
  "| category | 后端 registry category |",
  "| Mendix semantic name | 前端标题或后端 schema type |",
  "| schema kind | 后端 descriptor schemaType |",
  "| frontend registry key | 前端 node/action registry key |",
  "| legacy activity type | 前端旧 activityType 桥接名 |",
  "| toolbox visible | toolbox / action registry 可见性 |",
  "| property panel form path | property form 声明或注册状态 |",
  "| validation support | 前端 validation 支持或后端兜底 |",
  "| runtime executor | 后端 executor / connector / command 描述 |",
  "| runtime support level | runtimeCategory + supportLevel |",
  "| transaction behavior | 事务或外部副作用行为 |",
  "| variable output behavior | 变量或命令输出行为 |",
  "| error handling | 错误路径行为 |",
  "| trace behavior | trace / diagnostic 行为 |",
  "| connector capability | connector capability gate |",
  "| known limitations | 当前限制或 descriptor reason |",
  "| production decision | 当前生产决策 |",
  "| target round | 计划闭环轮次 |",
  "",
  "## 三合一矩阵",
  "",
  "| actionKind | category | Mendix semantic name | schema kind | frontend registry key | legacy activity type | toolbox visible | property panel form path | validation support | runtime executor | runtime support level | transaction behavior | variable output behavior | error handling | trace behavior | connector capability | known limitations | production decision | target round |",
  "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|",
  ...rows.map(row => `| ${row.map(esc).join(" | ")} |`)
].join("\n");

writeText(resolve(root, "docs/microflow/production-node-capability-matrix.md"), `${doc}\n`);
console.log(`Generated docs/microflow/production-node-capability-matrix.md with ${rows.length} rows.`);
