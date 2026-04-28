import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.cwd();

function read(relativePath: string): string {
  return readFileSync(join(root, relativePath), "utf8");
}

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function assertIncludes(file: string, needles: string[]): void {
  const content = read(file);
  for (const needle of needles) {
    assert(content.includes(needle), `${file} missing ${needle}`);
  }
}

function main(): void {
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/ErrorHandling/IMicroflowErrorHandlingService.cs", [
    "IMicroflowErrorHandlingService",
    "Handle(",
    "CompleteHandler(",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/ErrorHandling/MicroflowErrorHandlingService.cs", [
    "MicroflowErrorHandlingType.Rollback",
    "PrepareCustomWithRollback",
    "PrepareCustomWithoutRollback",
    "ContinueAfterError",
    "RuntimeContinueNotAllowed",
    "RuntimeErrorHandlerMaxDepthExceeded",
    "errorHandling",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Runtime/RuntimeExecutionContext.cs", [
    "$latestError",
    "$latestHttpResponse",
    "$latestSoapFault",
    "RecordHandledError",
    "RecordContinuedError",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Services/MicroflowMockRuntimeRunner.cs", [
    "ErrorHandlingService.Handle",
    "RuntimeErrorCode.RuntimeErrorEventReached",
    "PushErrorHandlerScope",
    "AddErrorHandlingFrame",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Services/MicroflowValidationService.cs", [
    "ErrorHandlerDuplicated",
    "latestSoapFault",
    "ErrorHandlerContinueNotAllowed",
  ]);
  assertIncludes("src/backend/Atlas.Application.Microflows/Models/MicroflowRuntimeDtos.cs", [
    "RuntimeErrorHandlerNotFound",
    "RuntimeErrorHandlerFailed",
    "RuntimeErrorHandlerRecursion",
    "RuntimeErrorHandlerMaxDepthExceeded",
    "RuntimeContinueNotAllowed",
    "ErrorHandlingSummary",
  ]);
  assertIncludes("src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http", [
    "Runtime Round 58 - ErrorHandling",
    "transactionRolledBack=true",
    "errorHandlingSummary",
    "errorHandlerFlowId",
  ]);
  assertIncludes("docs/microflow/contracts/runtime-error-handling-contract.md", [
    "第 58 轮",
    "customWithRollback",
    "customWithoutRollback",
    "$latestSoapFault",
  ]);
  console.log("verify-microflow-error-handling-runtime: PASS");
}

main();
