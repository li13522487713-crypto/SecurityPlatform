import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const standalonePath = resolve(root, "src/frontend/packages/mendix/mendix-microflow/src/expression-editor/index.tsx");
const propertyPanelPath = resolve(root, "src/frontend/packages/mendix/mendix-microflow/src/property-panel/expression/ExpressionEditor.tsx");
const cmHostPath = resolve(root, "src/frontend/packages/mendix/mendix-microflow/src/expression-editor/codemirror-microflow-expression.tsx");
const packagePath = resolve(root, "src/frontend/packages/mendix/mendix-microflow/package.json");
const standalone = existsSync(standalonePath) ? readFileSync(standalonePath, "utf8") : "";
const propertyPanel = existsSync(propertyPanelPath) ? readFileSync(propertyPanelPath, "utf8") : "";
const cmHost = existsSync(cmHostPath) ? readFileSync(cmHostPath, "utf8") : "";
const pkg = existsSync(packagePath) ? readFileSync(packagePath, "utf8") : "";

const checks = [
  ["standalone ExpressionEditor export", standalone.includes("export function ExpressionEditor")],
  ["property panel lazy codemirror", propertyPanel.includes("lazy(") && propertyPanel.includes("codemirror-microflow-expression")],
  ["codemirror host uses EditorView", cmHost.includes("EditorView") && cmHost.includes("@codemirror/view")],
  ["diagnostics rendering", standalone.includes("MicroflowExpressionDiagnostic") && standalone.includes("Diagnostic")],
  ["package codemirror deps", pkg.includes("@codemirror/view") && pkg.includes("@codemirror/state")],
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
