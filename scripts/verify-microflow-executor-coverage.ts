import { existsSync } from "node:fs";
import { resolve } from "node:path";
import {
  R1_KNOWN_MODELED_ONLY_BLOCKERS,
  SPECIALIZED_EXECUTOR_BY_KIND,
  type CheckResult,
  findWorkspaceRoot,
  parseBackendDescriptors,
  readWorkspaceFile
} from "./microflow-production-gate-lib.ts";

function add(results: CheckResult[], id: string, status: CheckResult["status"], summary: string, details?: string[]): void {
  results.push({ id, status, summary, details });
}

export function verifyExecutorCoverage(root = findWorkspaceRoot()): CheckResult[] {
  const results: CheckResult[] = [];
  const descriptors = parseBackendDescriptors(root);
  const registrySource = readWorkspaceFile("src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs", root);
  const byKind = new Map(descriptors.map(descriptor => [descriptor.actionKind, descriptor]));

  add(
    results,
    "coverage.descriptor-count",
    descriptors.length >= 80 ? "pass" : "warn",
    `后端 BuiltInDescriptors 当前覆盖 ${descriptors.length} 个 actionKind。`,
    descriptors.length >= 80 ? undefined : ["R1 记录为目标差距；后续轮次需补齐 Mendix L5 全量条目至 ≥80。"]
  );

  const missingSpecialized = Object.entries(SPECIALIZED_EXECUTOR_BY_KIND)
    .filter(([kind, executor]) => !byKind.has(kind) || !registrySource.includes(`GetService<${executor}>`))
    .map(([kind, executor]) => `${kind}:${executor}`);
  add(
    results,
    "coverage.specialized-dispatch",
    missingSpecialized.length === 0 ? "pass" : "fail",
    "supported 真实 executor 必须在 TryGet 中有 DI 派发。",
    missingSpecialized
  );

  const modeledOnlyFakeSuccess = descriptors
    .filter(descriptor => descriptor.runtimeCategory === "ServerExecutable")
    .filter(descriptor => descriptor.executor === "ConfiguredMicroflowActionExecutor")
    .map(descriptor => descriptor.actionKind);
  const unexpectedFakeSuccess = modeledOnlyFakeSuccess.filter(kind => !R1_KNOWN_MODELED_ONLY_BLOCKERS.has(kind));
  add(
    results,
    "coverage.no-unexpected-configured-server",
    unexpectedFakeSuccess.length === 0 ? "pass" : "fail",
    "ServerExecutable 不得通过 ConfiguredMicroflowActionExecutor 假成功，R1 已知 blocker 除外。",
    unexpectedFakeSuccess.length > 0 ? unexpectedFakeSuccess : modeledOnlyFakeSuccess.map(kind => `${kind} 是 R1 已知 blocker，production gate 标 conditional-go。`)
  );

  const connectorWithoutCapability = descriptors
    .filter(descriptor => descriptor.runtimeCategory === "ConnectorBacked")
    .filter(descriptor => !descriptor.connectorCapability || descriptor.errorCode !== "RUNTIME_CONNECTOR_REQUIRED")
    .map(descriptor => descriptor.actionKind);
  add(
    results,
    "coverage.connector-capability-gate",
    connectorWithoutCapability.length === 0 ? "pass" : "fail",
    "connectorBacked action 必须具备 capability gate 并返回 RUNTIME_CONNECTOR_REQUIRED。",
    connectorWithoutCapability
  );

  const unsupportedBad = descriptors
    .filter(descriptor => descriptor.runtimeCategory === "ExplicitUnsupported")
    .filter(descriptor => descriptor.realExecution || descriptor.errorCode !== "RUNTIME_UNSUPPORTED_ACTION")
    .map(descriptor => descriptor.actionKind);
  add(
    results,
    "coverage.explicit-unsupported-no-success",
    unsupportedBad.length === 0 ? "pass" : "fail",
    "explicitUnsupported action 不得返回 success。",
    unsupportedBad
  );

  const requiredRuntimeFiles = [
    "src/backend/Atlas.Application.Microflows/Runtime/Actions/ObjectActionExecutors.cs",
    "src/backend/Atlas.Application.Microflows/Runtime/Actions/ListActionExecutors.cs",
    "src/backend/Atlas.Application.Microflows/Runtime/Actions/VariableActionExecutors.cs",
    "src/backend/Atlas.Application.Microflows/Runtime/Actions/CallMicroflowActionExecutor.cs",
    "src/backend/Atlas.Application.Microflows/Runtime/Actions/RestCallActionExecutor.cs"
  ];
  const missingFiles = requiredRuntimeFiles.filter(path => !existsSync(resolve(root, path)));
  add(
    results,
    "coverage.runtime-files-present",
    missingFiles.length === 0 ? "pass" : "fail",
    "R1 coverage 依赖的真实 executor 源文件必须存在。",
    missingFiles
  );

  return results;
}

function print(results: CheckResult[]): void {
  for (const result of results) {
    const details = result.details?.length ? `\n  - ${result.details.join("\n  - ")}` : "";
    console.log(`${result.status.toUpperCase()} ${result.id}: ${result.summary}${details}`);
  }
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const results = verifyExecutorCoverage();
  print(results);
  const failed = results.filter(result => result.status === "fail");
  if (failed.length > 0) {
    console.error(`\nverify-microflow-executor-coverage failed: ${failed.length} failing checks.`);
    process.exit(1);
  }
  console.log("\nverify-microflow-executor-coverage passed.");
}
