import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const backend = resolve(root, "src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionEditorServices.cs");
const controller = resolve(root, "src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowExpressionsController.cs");
const frontend = resolve(root, "src/frontend/packages/mendix/mendix-microflow/src/expressions/expression-tokenizer.ts");

const backendSource = existsSync(backend) ? readFileSync(backend, "utf8") : "";
const controllerSource = existsSync(controller) ? readFileSync(controller, "utf8") : "";
const frontendSource = existsSync(frontend) ? readFileSync(frontend, "utf8") : "";

const checks = [
  ["lexer service", backendSource.includes("MicroflowExpressionLexer")],
  ["parser service", backendSource.includes("MicroflowExpressionParserService")],
  ["type checker", backendSource.includes("MicroflowExpressionTypeChecker")],
  ["formatter", backendSource.includes("MicroflowExpressionFormatter")],
  ["completion provider", backendSource.includes("MicroflowExpressionCompletionProvider")],
  ["diagnostics provider", backendSource.includes("MicroflowExpressionDiagnosticsProvider")],
  ["preview service", backendSource.includes("MicroflowExpressionPreviewService")],
  ["api parse", controllerSource.includes("[HttpPost(\"parse\")]")],
  ["api validate", controllerSource.includes("[HttpPost(\"validate\")]")],
  ["api infer-type", controllerSource.includes("[HttpPost(\"infer-type\")]")],
  ["api completions", controllerSource.includes("[HttpPost(\"completions\")]")],
  ["api format", controllerSource.includes("[HttpPost(\"format\")]")],
  ["api preview", controllerSource.includes("[HttpPost(\"preview\")]")],
  ["metadataVersion gate", controllerSource.includes("MicroflowExpressionApiMetadata")],
  ["frontend tokenizer parity", frontendSource.includes("tokenize") || frontendSource.includes("ExpressionToken")],
] as const;

let failed = 0;
for (const [name, ok] of checks) {
  console.log(`${ok ? "ok" : "fail"} - ${name}`);
  if (!ok) failed += 1;
}
if (failed > 0) {
  console.error(`${failed} microflow expression language checks failed.`);
  process.exit(1);
}
console.log("Microflow expression language checks passed.");
