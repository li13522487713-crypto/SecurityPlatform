import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const gatewayPath = resolve(root, "src/backend/Atlas.Application.Microflows/Runtime/Branches/MicroflowBranchRuntimeModels.cs");
const source = existsSync(gatewayPath) ? readFileSync(gatewayPath, "utf8") : "";

const checks = [
  ["inclusive split executor exists", source.includes("InclusiveGatewaySplitExecutor")],
  ["activation set model exists", source.includes("ActivationSet")],
  ["otherwise uniqueness validation exists", source.includes("INCLUSIVE_OTHERWISE_NOT_UNIQUE")],
  ["no branch selected validation exists", source.includes("INCLUSIVE_NO_BRANCH_SELECTED")],
  ["inclusive join waits active branches", source.includes("InclusiveGatewayJoinExecutor") && source.includes("activeBranchIds.All")],
] as const;

let failed = 0;
for (const [name, ok] of checks) {
  console.log(`${ok ? "ok" : "fail"} - ${name}`);
  if (!ok) failed += 1;
}
if (failed > 0) {
  console.error(`${failed} inclusive gateway checks failed.`);
  process.exit(1);
}
console.log("Microflow inclusive gateway checks passed.");
