import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function run(command: string, args: string[]): void {
  execFileSync(command, args, { cwd: root, stdio: "inherit" });
}

function read(relativePath: string): string {
  return readFileSync(join(root, relativePath), "utf8");
}

function verifySourceContracts(): void {
  const evaluator = read("src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionEvaluator.cs");
  const parser = read("src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionParser.cs");
  const tokenizer = read("src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionTokenizer.cs");
  const models = read("src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionModels.cs");
  const typeInference = read("src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionTypeInference.cs");
  const runner = read("src/backend/Atlas.Application.Microflows/Services/MicroflowMockRuntimeRunner.cs");
  const navigator = read("src/backend/Atlas.Application.Microflows/Services/MicroflowFlowNavigator.cs");

  for (const [name, content] of Object.entries({ evaluator, parser, tokenizer, models, typeInference })) {
    assert(!/CSharpScript|CodeDom|Roslyn|CompileAssembly|DynamicMethod|Expression\.Compile/.test(content), `${name} must not use dynamic code execution`);
  }

  assert(models.includes("IMicroflowExpressionEvaluator"), "IMicroflowExpressionEvaluator contract is missing");
  assert(tokenizer.includes("DollarVariable"), "Tokenizer must support dollar variables");
  assert(parser.includes("ParseIfExpression"), "Parser must support if/then/else");
  assert(typeInference.includes("InferMember"), "Type inference must support member access");
  assert(evaluator.includes("EvaluateFunction"), "Evaluator must support controlled function dispatch");
  assert(runner.includes("EvaluateExpressionOrThrow"), "MockRuntimeRunner must call ExpressionEvaluator");
  assert(navigator.includes("DisableExpressionEvaluation"), "FlowNavigator must keep expression evaluation opt-out");
}

function main(): void {
  verifySourceContracts();
  run("dotnet", [
    "test",
    "tests/Atlas.AppHost.Tests/Atlas.AppHost.Tests.csproj",
    "--filter",
    "FullyQualifiedName~MicroflowExpressionEvaluatorTests",
    `-p:BaseOutputPath=${join(root, "artifacts", "test-bin")}${process.platform === "win32" ? "\\" : "/"}`,
  ]);
  console.log("verify-microflow-expression-evaluator: pass");
}

main();
