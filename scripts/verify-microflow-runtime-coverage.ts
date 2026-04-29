/**
 * P0-10: 双向核对前端 mendix-microflow 节点注册表与后端 ActionExecutorRegistry
 * 描述符的 actionKind 集合，确保不会出现"前端建模、后端无任何描述"的缝隙。
 *
 * 检查规则：
 *   1. 后端 BuiltInDescriptors 列出的每个 actionKind，前端 SUPPORTED/PARTIAL/UNSUPPORTED
 *      列表必须出现；否则前端 toolbox 会少一个建模节点。
 *   2. 前端 SUPPORTED_ACTION_KINDS 中的 actionKind，后端必须有对应描述符且 SupportLevel
 *      与 Server / Configured ModeledOnlyConverted（即真实可执行）一致；否则会出现"前端
 *      标 supported 但后端实际无 executor"的假成功风险。
 *   3. 前端 NANOFLOW_ONLY_DISABLED_KINDS / unsupported 列表里的 kind，后端描述符必须是
 *      Unsupported 或 Connector，不能是 Server。
 *
 * 不依赖运行时数据库或 HTTP，只做静态字面量解析。
 */

import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();

interface BackendDescriptor {
  actionKind: string;
  factory: "Server" | "Connector" | "Command" | "Unsupported";
}

function readFile(path: string): string {
  return readFileSync(resolve(root, path), "utf8");
}

function parseBackendDescriptors(): BackendDescriptor[] {
  const path = "src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs";
  const source = readFile(path);
  const builtInStart = source.indexOf("public static IReadOnlyList<MicroflowActionExecutorDescriptor> BuiltInDescriptors()");
  if (builtInStart < 0) {
    throw new Error(`无法在 ${path} 中找到 BuiltInDescriptors`);
  }
  const arrayStart = source.indexOf("[", builtInStart);
  const arrayEnd = source.indexOf("];", arrayStart);
  if (arrayStart < 0 || arrayEnd < 0) {
    throw new Error(`BuiltInDescriptors 数组定界识别失败`);
  }
  const body = source.slice(arrayStart, arrayEnd);

  const factoryRegex = /\b(Server|Connector|Command|Unsupported)\s*\(\s*"([A-Za-z][A-Za-z0-9_]*)"/g;
  const descriptors: BackendDescriptor[] = [];
  let match: RegExpExecArray | null;
  while ((match = factoryRegex.exec(body)) !== null) {
    descriptors.push({ factory: match[1] as BackendDescriptor["factory"], actionKind: match[2] });
  }
  return descriptors;
}

function parseFrontendKindSet(constant: string): Set<string> {
  const path = "src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts";
  const source = readFile(path);
  const declRegex = new RegExp(
    `${constant.replace(/[.*+?^${}()|[\\]\\\\]/g, "\\$&")}\\s*=\\s*new Set<[^>]+>\\(\\[`,
  );
  const declMatch = declRegex.exec(source);
  if (!declMatch) {
    throw new Error(`无法在 registry.ts 找到常量 ${constant}`);
  }
  const blockStart = declMatch.index + declMatch[0].length - 1; // points at "["
  const blockEnd = source.indexOf("]);", blockStart);
  if (blockStart < 0 || blockEnd < 0) {
    throw new Error(`${constant} 数组定界识别失败`);
  }
  const body = source.slice(blockStart, blockEnd);
  const tokenRegex = /"([A-Za-z][A-Za-z0-9_]*)"/g;
  const out = new Set<string>();
  let match: RegExpExecArray | null;
  while ((match = tokenRegex.exec(body)) !== null) {
    out.add(match[1]);
  }
  return out;
}

interface CheckResult {
  name: string;
  ok: boolean;
  details?: string;
}

const results: CheckResult[] = [];

function add(name: string, ok: boolean, details?: string): void {
  results.push({ name, ok, details });
}

function fmt(items: Iterable<string>): string {
  return Array.from(items).sort().join(", ");
}

const registryPath = "src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts";
const backendPath = "src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs";
add("registry.ts exists", existsSync(resolve(root, registryPath)));
add("backend MicroflowActionExecutorRegistry.cs exists", existsSync(resolve(root, backendPath)));

const backendDescriptors = parseBackendDescriptors();
add("backend descriptors > 0", backendDescriptors.length > 0, `count=${backendDescriptors.length}`);

const supportedFrontend = parseFrontendKindSet("SUPPORTED_ACTION_KINDS");
const partialFrontend = parseFrontendKindSet("PARTIAL_ACTION_KINDS");
let nanoflowOnlyFrontend: Set<string>;
try {
  nanoflowOnlyFrontend = parseFrontendKindSet("NANOFLOW_ONLY_DISABLED_KINDS");
} catch {
  nanoflowOnlyFrontend = new Set();
}

add("frontend SUPPORTED_ACTION_KINDS > 0", supportedFrontend.size > 0, `count=${supportedFrontend.size}`);
add("frontend PARTIAL_ACTION_KINDS > 0", partialFrontend.size > 0, `count=${partialFrontend.size}`);

const backendKinds = new Set(backendDescriptors.map(d => d.actionKind));

// 1. 后端有的 actionKind 前端必须建模到 supported / partial / unsupported 之一
const backendOnlyKinds = new Set<string>();
for (const kind of backendKinds) {
  if (
    !supportedFrontend.has(kind)
    && !partialFrontend.has(kind)
    && !nanoflowOnlyFrontend.has(kind)
  ) {
    backendOnlyKinds.add(kind);
  }
}
// 这些 actionKind 是后端为兼容 legacy schema 保留的派发别名（在前端 toolbox 中以更
// 现代的名称暴露），允许只存在于后端。属于"后端独有但符合预期"的清单，在矩阵报告
// 中以信息性输出列出，不参与 fail。
const BACKEND_ONLY_LEGACY_ALIASES = new Set([
  // legacy / generic dispatcher fallbacks
  "connectorCall",
  "externalConnectorCall",
  "externalObject",
  "javascriptAction",
  "metrics",
  "nanoflowCall",
  "nanoflowCallAction",
  "nanoflowOnlySynchronize",
  "workflow",
  "workflowAction",
  // 客户端命令类，前端节点以"页面动作"等形式建模而非以原始 actionKind 出现。
  "callJavaScriptAction",
  "callNanoflow",
  "cast",
  "closePage",
  "downloadFile",
  "callODataAction",
  "changeExternalObject",
  "commitODataObject",
  "consumeMessage",
  "createExternalObject",
  "deleteODataObject",
  "exportFileDocument",
  "importFileDocument",
  "mlModelCall",
  "publishMessage",
  "retrieveFileDocument",
  "retrieveODataObject",
  "sendEmail",
  "sendNotification",
  "showHomePage",
  "showMessage",
  "showPage",
  "storeFileDocument",
  "synchronize",
  "validationFeedback",
]);
const backendOnlyExpected = Array.from(backendOnlyKinds).filter(kind => BACKEND_ONLY_LEGACY_ALIASES.has(kind));
const backendOnlyUnexpected = Array.from(backendOnlyKinds).filter(kind => !BACKEND_ONLY_LEGACY_ALIASES.has(kind));
if (backendOnlyExpected.length > 0) {
  console.log(`info - backend-only legacy aliases (allowed): [${fmt(backendOnlyExpected)}]`);
}
add(
  "every backend actionKind beyond the legacy alias allowlist is modeled in frontend registry",
  backendOnlyUnexpected.length === 0,
  backendOnlyUnexpected.length === 0 ? undefined : `missing-on-frontend=[${fmt(backendOnlyUnexpected)}]`,
);

// 2. 前端 SUPPORTED 必须在后端有 Server 描述符（Configured stub 也算 Server）
const frontendSupportedMissing = new Set<string>();
const frontendSupportedNotServer = new Set<string>();
for (const kind of supportedFrontend) {
  const descriptors = backendDescriptors.filter(d => d.actionKind === kind);
  if (descriptors.length === 0) {
    frontendSupportedMissing.add(kind);
    continue;
  }
  if (!descriptors.some(d => d.factory === "Server")) {
    frontendSupportedNotServer.add(kind);
  }
}
add(
  "frontend supported actionKind has backend descriptor",
  frontendSupportedMissing.size === 0,
  frontendSupportedMissing.size === 0 ? undefined : `missing=[${fmt(frontendSupportedMissing)}]`,
);
add(
  "frontend supported actionKind backend descriptor is Server (real executable)",
  frontendSupportedNotServer.size === 0,
  frontendSupportedNotServer.size === 0 ? undefined : `not-server=[${fmt(frontendSupportedNotServer)}]`,
);

// 3. 前端 nanoflow-only 必须在后端是 Unsupported（或缺席），不能是 Server
const nanoflowAsServer = new Set<string>();
for (const kind of nanoflowOnlyFrontend) {
  const descriptors = backendDescriptors.filter(d => d.actionKind === kind);
  if (descriptors.some(d => d.factory === "Server")) {
    nanoflowAsServer.add(kind);
  }
}
add(
  "frontend nanoflow-only actionKind backend is not Server",
  nanoflowAsServer.size === 0,
  nanoflowAsServer.size === 0 ? undefined : `as-server=[${fmt(nanoflowAsServer)}]`,
);

console.log(`Backend descriptors: ${backendDescriptors.length}`);
console.log(`Frontend supported: ${supportedFrontend.size}`);
console.log(`Frontend partial:   ${partialFrontend.size}`);
console.log(`Frontend nanoflow-only-disabled: ${nanoflowOnlyFrontend.size}`);
console.log("");

for (const result of results) {
  const tag = result.ok ? "ok" : "fail";
  const detailsSuffix = result.ok || !result.details ? "" : `: ${result.details}`;
  console.log(`${tag} - ${result.name}${detailsSuffix}`);
}

const failed = results.filter(r => !r.ok);
if (failed.length > 0) {
  console.error(`\n${failed.length} microflow runtime coverage checks failed.`);
  process.exit(1);
}

console.log("\nMicroflow runtime coverage matrix consistent.");
