import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const source = readFileSync(resolve(root, "src/backend/Atlas.Application.Microflows/Runtime/Branches/MicroflowBranchRuntimeModels.cs"), "utf8");
const checks = [
  ["ParallelBranchScheduler", /class\s+ParallelBranchScheduler/.test(source)],
  ["Task.WhenAll", /Task\.WhenAll/.test(source)],
  ["ParallelGatewaySplitExecutor", /class\s+ParallelGatewaySplitExecutor/.test(source)],
  ["ParallelGatewayJoinExecutor", /class\s+ParallelGatewayJoinExecutor/.test(source)],
  ["PARALLEL_VARIABLE_WRITE_CONFLICT", /PARALLEL_VARIABLE_WRITE_CONFLICT/.test(source)],
  ["PARALLEL_WRITE_CONFLICT", /PARALLEL_WRITE_CONFLICT/.test(source)],
];
for (const [name, ok] of checks) {
  console.log(`${ok ? "ok" : "fail"} - ${name}`);
}
if (checks.some(([, ok]) => !ok)) {
  process.exit(1);
}
console.log("verify-microflow-parallel-gateway passed.");
