import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const editorPath = resolve(root, "src/frontend/packages/mendix/mendix-microflow/src/expression-editor/index.tsx");
const packagePath = resolve(root, "src/frontend/packages/mendix/mendix-microflow/package.json");
const editor = existsSync(editorPath) ? readFileSync(editorPath, "utf8") : "";
const pkg = existsSync(packagePath) ? readFileSync(packagePath, "utf8") : "";

const checks = [
  ["ExpressionEditor component", editor.includes("export function ExpressionEditor")],
  ["diagnostics rendering", editor.includes("MicroflowExpressionDiagnostic") && editor.includes("Diagnostic")],
  ["completions", editor.includes("completions") && editor.includes("Completion")],
  ["preview support", editor.includes("preview") && editor.includes("onPreview")],
  ["format support", editor.includes("onFormat")],
  ["package export", pkg.includes("./expression-editor")],
] as const;

let failed = 0;
for (const [name, ok] of checks) {
  console.log(`${ok ? "ok" : "fail"} - ${name}`);
  if (!ok) failed += 1;
}
if (failed > 0) {
  console.error(`${failed} microflow expression editor checks failed.`);
  process.exit(1);
}
console.log("Microflow expression editor checks passed.");
