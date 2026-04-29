/**
 * 静态断言：协作式 Step Debug（DebugCoordinator + 引擎安全点 + DebugSessionId 透传）在源码中保持存在。
 * 从任意 cwd 运行（通过 findWorkspaceRoot）。
 */

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { findWorkspaceRoot } from "./microflow-production-gate-lib.ts";

const root = findWorkspaceRoot();

function read(rel: string): string {
  return readFileSync(resolve(root, rel), "utf8");
}

const coordinatorIf = read("src/backend/Atlas.Application.Microflows/Runtime/Debug/IMicroflowDebugCoordinator.cs");
const coordinatorImpl = read("src/backend/Atlas.Application.Microflows/Runtime/Debug/MicroflowDebugCoordinator.cs");
const debugModels = read("src/backend/Atlas.Application.Microflows/Runtime/Debug/MicroflowDebugRuntimeModels.cs");
const di = read("src/backend/Atlas.Application.Microflows/DependencyInjection/MicroflowApplicationServiceCollectionExtensions.cs");
const engine = read("src/backend/Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs");
const execRequest = read("src/backend/Atlas.Application.Microflows/Abstractions/IMicroflowTestRunService.cs");
const callMf = read("src/backend/Atlas.Application.Microflows/Runtime/Actions/CallMicroflowActionExecutor.cs");
const debugController = read("src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowDebugController.cs");

const checks = [
  ["IMicroflowDebugCoordinator.WaitAtSafePointAsync", coordinatorIf.includes("WaitAtSafePointAsync")],
  ["IMicroflowDebugCoordinator.ReleaseOnePause", coordinatorIf.includes("ReleaseOnePause")],
  ["MicroflowDebugCoordinator implements interface", coordinatorImpl.includes("MicroflowDebugCoordinator : IMicroflowDebugCoordinator")],
  ["MicroflowDebugCoordinator command state machine", coordinatorImpl.includes("ApplyCommand") && coordinatorImpl.includes("ShouldPause")],
  ["MicroflowDebugCoordinator safe point snapshot", coordinatorIf.includes("MicroflowDebugRuntimeSnapshot") && coordinatorImpl.includes("CurrentSafePoint")],
  ["MicroflowDebugSession ownership fields", debugModels.includes("TenantId") && debugModels.includes("WorkspaceId") && debugModels.includes("CreatedBy")],
  ["MicroflowDebug trace/variables/callstack", debugModels.includes("DebugTraceEvent") && debugModels.includes("DebugVariableSnapshot") && debugModels.includes("DebugCallStackFrame")],
  ["DI TryAddSingleton<IMicroflowDebugCoordinator", di.includes("TryAddSingleton<IMicroflowDebugCoordinator, MicroflowDebugCoordinator>")],
  ["Engine injects IMicroflowDebugCoordinator", engine.includes("IMicroflowDebugCoordinator")],
  ["Engine DebugCheckpointAsync", engine.includes("DebugCheckpointAsync")],
  ["Engine BeforeNode / AfterNode checkpoints", engine.includes("MicroflowDebugPausePhase.BeforeNode") && engine.includes("MicroflowDebugPausePhase.AfterNode")],
  ["MicroflowExecutionRequest.DebugSessionId", execRequest.includes("DebugSessionId")],
  ["CallMicroflow passes DebugSessionId", callMf.includes("DebugSessionId") && callMf.includes("RuntimeExecutionContext")],
  ["MicroflowDebugController enforces session ownership", debugController.includes("ResolveOwnedSession") && debugController.includes("MicroflowDebugSessionForbidden")]
] as const;

let failed = 0;
for (const [name, ok] of checks) {
  console.log(`${ok ? "ok" : "fail"} - ${name}`);
  if (!ok) failed += 1;
}

if (failed > 0) {
  console.error(`\nverify-microflow-debug-runtime: ${failed} check(s) failed.`);
  process.exit(1);
}
console.log("\nverify-microflow-debug-runtime passed.");
