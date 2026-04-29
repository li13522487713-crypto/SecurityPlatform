import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const controller = resolve(root, "src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowDebugController.cs");
const source = existsSync(controller) ? readFileSync(controller, "utf8") : "";
const endpoints = ["debug-sessions", "commands", "variables", "evaluate", "trace"];
let failed = 0;
for (const endpoint of endpoints) {
  const ok = source.includes(endpoint);
  console.log(`${ok ? "ok" : "fail"} - debug api ${endpoint}`);
  if (!ok) failed += 1;
}
const auth = source.includes("[Route(\"api/v1/microflows") && source.includes("MicroflowApiControllerBase");
console.log(`${auth ? "ok" : "fail"} - debug api uses api/v1 authorized base`);
if (!auth) failed += 1;
if (failed > 0) process.exit(1);
console.log("Microflow debug API checks passed.");
