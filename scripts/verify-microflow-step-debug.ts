import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const model = readFileSync(resolve(root, "src/backend/Atlas.Application.Microflows/Runtime/Debug/MicroflowDebugRuntimeModels.cs"), "utf8");
const controller = readFileSync(resolve(root, "src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowDebugController.cs"), "utf8");
const ui = readFileSync(resolve(root, "src/frontend/packages/mendix/mendix-microflow/src/debug/step-debug-ui.tsx"), "utf8");

const checks = [
  ["session store", model.includes("DebugSessionStore")],
  ["13 states", ["created","starting","running","pausing","paused","stepping","waitingAtJoin","completed","failed","cancelled","timedOut","expired"].every(s => model.includes(s))],
  ["breakpoints", model.includes("BreakpointDescriptor") && model.includes("ConditionalBreakpointDescriptor")],
  ["variables snapshot", model.includes("DebugVariableSnapshot") && model.includes("Redact")],
  ["watches", model.includes("DebugWatchExpression")],
  ["api commands", controller.includes("[HttpPost(\"debug-sessions/{sessionId}/commands\")]")],
  ["api variables", controller.includes("[HttpGet(\"debug-sessions/{sessionId}/variables\")]")],
  ["api evaluate", controller.includes("[HttpPost(\"debug-sessions/{sessionId}/evaluate\")]")],
  ["ui toolbar", ui.includes("Step Over") && ui.includes("Step Into") && ui.includes("Step Out")],
  ["ui panels", ui.includes("Variables") && ui.includes("Watches") && ui.includes("Call stack") && ui.includes("Branch tree")],
] as const;

let failed = 0;
for (const [name, ok] of checks) {
  console.log(`${ok ? "ok" : "fail"} - ${name}`);
  if (!ok) failed += 1;
}
if (failed > 0) {
  console.error(`${failed} microflow step debug checks failed.`);
  process.exit(1);
}
console.log("Microflow step debug checks passed.");
