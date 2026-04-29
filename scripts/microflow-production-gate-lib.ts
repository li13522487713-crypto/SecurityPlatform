import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export type DescriptorFactory = "Server" | "Connector" | "Command" | "Unsupported";

export interface BackendDescriptor {
  actionKind: string;
  schemaType: string;
  registryCategory: string;
  runtimeCategory: "ServerExecutable" | "ConnectorBacked" | "RuntimeCommand" | "ExplicitUnsupported";
  supportLevel: string;
  executor: string;
  connectorCapability?: string;
  errorCode?: string;
  realExecution: boolean;
  producesVariables: boolean;
  producesTransaction: boolean;
  producesRuntimeCommand: boolean;
  factory: DescriptorFactory;
}

export interface CheckResult {
  id: string;
  status: "pass" | "warn" | "fail" | "pending";
  summary: string;
  details?: string[];
}

export const LEGACY_ACTION_ALIASES = [
  "webserviceCall",
  "webService",
  "callExternal",
  "externalCall",
  "deleteExternal",
  "sendExternal",
  "rollbackObject",
  "castObject",
  "listUnion",
  "listIntersect",
  "listSubtract",
  "aggregate",
  "filter",
  "sort"
] as const;

export const R1_KNOWN_MODELED_ONLY_BLOCKERS = new Set(["rollback", "cast", "listOperation"]);

export const SPECIALIZED_EXECUTOR_BY_KIND: Readonly<Record<string, string>> = {
  retrieve: "RetrieveObjectActionExecutor",
  createObject: "CreateObjectActionExecutor",
  changeMembers: "ChangeObjectActionExecutor",
  commit: "CommitObjectActionExecutor",
  delete: "DeleteObjectActionExecutor",
  createList: "CreateListActionExecutor",
  changeList: "ChangeListActionExecutor",
  aggregateList: "AggregateListActionExecutor",
  filterList: "FilterListActionExecutor",
  sortList: "SortListActionExecutor",
  createVariable: "CreateVariableActionExecutor",
  changeVariable: "ChangeVariableActionExecutor",
  break: "BreakActionExecutor",
  continue: "ContinueActionExecutor",
  callMicroflow: "CallMicroflowActionExecutor",
  restCall: "RestCallActionExecutor",
  logMessage: "LogMessageActionExecutor",
  throwException: "ThrowExceptionActionExecutor"
};

export function findWorkspaceRoot(start = process.cwd()): string {
  const candidates = [
    start,
    resolve(start, ".."),
    resolve(start, "../.."),
    resolve(dirname(fileURLToPath(import.meta.url)), "..")
  ];
  for (const candidate of candidates) {
    if (existsSync(resolve(candidate, "src/backend")) && existsSync(resolve(candidate, "src/frontend"))) {
      return candidate;
    }
  }
  return resolve(dirname(fileURLToPath(import.meta.url)), "..");
}

export function readWorkspaceFile(relativePath: string, root = findWorkspaceRoot()): string {
  return readFileSync(resolve(root, relativePath), "utf8");
}

function extractDescriptorCalls(source: string): Array<{ factory: DescriptorFactory; call: string }> {
  const start = source.indexOf("public static IReadOnlyList<MicroflowActionExecutorDescriptor> BuiltInDescriptors()");
  if (start < 0) {
    throw new Error("BuiltInDescriptors() not found.");
  }
  const bodyStart = source.indexOf("[", start);
  const bodyEnd = source.indexOf("];", bodyStart);
  if (bodyStart < 0 || bodyEnd < 0) {
    throw new Error("BuiltInDescriptors array body not found.");
  }
  const body = source.slice(bodyStart, bodyEnd);
  const calls: Array<{ factory: DescriptorFactory; call: string }> = [];
  const regex = /\b(Server|Connector|Command|Unsupported)\s*\(/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(body)) !== null) {
    let depth = 0;
    let end = match.index;
    for (let index = match.index; index < body.length; index += 1) {
      const char = body[index];
      if (char === "(") {
        depth += 1;
      } else if (char === ")") {
        depth -= 1;
        if (depth === 0) {
          end = index + 1;
          break;
        }
      }
    }
    calls.push({ factory: match[1] as DescriptorFactory, call: body.slice(match.index, end) });
  }
  return calls;
}

function stringLiterals(input: string): string[] {
  const values: string[] = [];
  const regex = /"((?:[^"\\]|\\.)*)"/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(input)) !== null) {
    values.push(match[1].replace(/\\"/g, "\""));
  }
  return values;
}

function supportLevel(factory: DescriptorFactory, call: string): string {
  if (factory === "Connector") {
    return call.includes("MicroflowActionSupportLevel.Deprecated") ? "Deprecated" : "RequiresConnector";
  }
  if (factory === "Command") {
    return "ModeledOnlyConverted";
  }
  if (call.includes("MicroflowActionSupportLevel.ModeledOnlyConverted")) {
    return "ModeledOnlyConverted";
  }
  if (call.includes("MicroflowActionSupportLevel.NanoflowOnly")) {
    return "NanoflowOnly";
  }
  if (call.includes("MicroflowActionSupportLevel.Unsupported")) {
    return "Unsupported";
  }
  return factory === "Unsupported" ? "Unsupported" : "Supported";
}

function runtimeCategory(factory: DescriptorFactory): BackendDescriptor["runtimeCategory"] {
  if (factory === "Server") {
    return "ServerExecutable";
  }
  if (factory === "Connector") {
    return "ConnectorBacked";
  }
  if (factory === "Command") {
    return "RuntimeCommand";
  }
  return "ExplicitUnsupported";
}

function parseBooleanNamed(call: string, name: string, fallback: boolean): boolean {
  const named = new RegExp(`${name}\\s*:\\s*(true|false)`).exec(call);
  if (named) {
    return named[1] === "true";
  }
  const positional = new RegExp(`${name}\\s*:\\s*(true|false)`).exec(call);
  if (positional) {
    return positional[1] === "true";
  }
  return fallback;
}

export function parseBackendDescriptorsFromSource(source: string): BackendDescriptor[] {
  return extractDescriptorCalls(source).map(({ factory, call }) => {
    const strings = stringLiterals(call);
    const actionKind = strings[0] ?? "";
    const schemaType = strings[1] ?? "";
    const registryCategory = strings[2] ?? "";
    const fallbackExecutor = factory === "Unsupported" ? "ExplicitUnsupportedActionExecutor" : "";
    const executor = factory === "Unsupported" ? fallbackExecutor : strings[3] ?? fallbackExecutor;
    const connectorCapability = factory === "Connector"
      ? call.includes("MicroflowRuntimeConnectorCapability.")
        ? (call.match(/MicroflowRuntimeConnectorCapability\.([A-Za-z0-9_]+)/)?.[1] ?? strings[4])
        : strings[4]
      : factory === "Command"
        ? "ClientCommand"
        : undefined;
    return {
      actionKind,
      schemaType,
      registryCategory,
      runtimeCategory: runtimeCategory(factory),
      supportLevel: supportLevel(factory, call),
      executor,
      connectorCapability,
      errorCode: factory === "Connector" ? "RUNTIME_CONNECTOR_REQUIRED" : factory === "Unsupported" ? "RUNTIME_UNSUPPORTED_ACTION" : undefined,
      realExecution: factory === "Server" || factory === "Command",
      producesVariables: parseProducesFlag(call, "producesVariables", 4),
      producesTransaction: parseProducesFlag(call, "producesTransaction", 5),
      producesRuntimeCommand: factory === "Command",
      factory
    };
  }).filter(descriptor => descriptor.actionKind);
}

function splitTopLevelArguments(call: string): string[] {
  const open = call.indexOf("(");
  const close = call.lastIndexOf(")");
  const body = open >= 0 && close > open ? call.slice(open + 1, close) : call;
  const args: string[] = [];
  let depth = 0;
  let start = 0;
  let inString = false;
  let escaped = false;
  for (let index = 0; index < body.length; index += 1) {
    const char = body[index];
    if (inString) {
      if (escaped) {
        escaped = false;
      } else if (char === "\\") {
        escaped = true;
      } else if (char === "\"") {
        inString = false;
      }
      continue;
    }
    if (char === "\"") {
      inString = true;
      continue;
    }
    if (char === "(" || char === "[" || char === "{") {
      depth += 1;
      continue;
    }
    if (char === ")" || char === "]" || char === "}") {
      depth -= 1;
      continue;
    }
    if (char === "," && depth === 0) {
      args.push(body.slice(start, index).trim());
      start = index + 1;
    }
  }
  args.push(body.slice(start).trim());
  return args;
}

function parseProducesFlag(call: string, name: string, positionalIndex: number): boolean {
  const named = new RegExp(`${name}\\s*:\\s*(true|false)`).exec(call);
  if (named) {
    return named[1] === "true";
  }
  const args = splitTopLevelArguments(call);
  const value = args[positionalIndex];
  return value === "true";
}

export function parseBackendDescriptors(root = findWorkspaceRoot()): BackendDescriptor[] {
  return parseBackendDescriptorsFromSource(readWorkspaceFile("src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs", root));
}

export function parseStringSet(source: string, constantName: string): Set<string> {
  const declaration = new RegExp(`${constantName}\\s*=\\s*new Set<[^>]+>\\(\\[`).exec(source);
  if (!declaration) {
    return new Set();
  }
  const start = declaration.index + declaration[0].length;
  const end = source.indexOf("]);", start);
  const body = end < 0 ? source.slice(start) : source.slice(start, end);
  return new Set(stringLiterals(body).filter(value => /^[A-Za-z][A-Za-z0-9_]*$/.test(value)));
}

export function parseFrontendActionKinds(root = findWorkspaceRoot()): Set<string> {
  const registry = readWorkspaceFile("src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts", root);
  const actionRegistry = readWorkspaceFile("src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts", root);
  const kinds = new Set<string>([
    ...parseStringSet(registry, "SUPPORTED_ACTION_KINDS"),
    ...parseStringSet(registry, "PARTIAL_ACTION_KINDS"),
    ...parseStringSet(actionRegistry, "P0_ACTION_KINDS")
  ]);
  const regex = /(?:actionKind|kind|key)\s*:\s*"([A-Za-z][A-Za-z0-9_]*)"/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(actionRegistry)) !== null) {
    kinds.add(match[1]);
  }
  return kinds;
}

export function parseRegisteredPropertyFormKeys(root = findWorkspaceRoot()): Set<string> {
  const formRegistry = readWorkspaceFile("src/frontend/packages/mendix/mendix-microflow/src/property-panel/node-form-registry.ts", root);
  const keys = new Set<string>();
  const regex = /registerMicroflowNodeForm\s*\(\s*"([^"]+)"/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(formRegistry)) !== null) {
    keys.add(match[1]);
  }
  return keys;
}

export function parseMarkdownMatrixActionKinds(markdown: string): Set<string> {
  const kinds = new Set<string>();
  const rowRegex = /^\|\s*`?([A-Za-z][A-Za-z0-9_]*)`?\s*\|/gm;
  let match: RegExpExecArray | null;
  while ((match = rowRegex.exec(markdown)) !== null) {
    const value = match[1];
    if (!["actionKind", "ActionKind"].includes(value)) {
      kinds.add(value);
    }
  }
  return kinds;
}

export function detectLegacyAliasesInText(text: string): string[] {
  const found = new Set<string>();
  for (const alias of LEGACY_ACTION_ALIASES) {
    const regex = new RegExp(`(?<![A-Za-z0-9_])${alias}(?![A-Za-z0-9_])`, "u");
    if (regex.test(text)) {
      found.add(alias);
    }
  }
  return [...found].sort();
}

export function writeJson(path: string, value: unknown): void {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

export function writeText(path: string, value: string): void {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, value, "utf8");
}

export function summarizeResults(results: CheckResult[]): "go" | "conditional-go" | "no-go" {
  if (results.some(result => result.status === "fail")) {
    return "no-go";
  }
  if (results.some(result => result.status === "warn" || result.status === "pending")) {
    return "conditional-go";
  }
  return "go";
}
