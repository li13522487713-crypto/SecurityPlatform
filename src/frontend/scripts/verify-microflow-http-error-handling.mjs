#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const read = path => readFileSync(resolve(root, path), "utf8");
const checks = [];

function assertCheck(name, condition) {
  checks.push({ name, ok: Boolean(condition) });
}

const codes = read("packages/mendix/mendix-studio-core/src/microflow/contracts/api/api-error-codes.ts");
const envelope = read("packages/mendix/mendix-studio-core/src/microflow/contracts/api/api-envelope.ts");
const error = read("packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-error.ts");
const client = read("packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-client.ts");
const resourceTab = read("packages/mendix/mendix-studio-core/src/microflow/resource/MicroflowResourceTab.tsx");
const editorPage = read("packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorPage.tsx");
const editor = read("packages/mendix/mendix-microflow/src/editor/index.tsx");
const publish = read("packages/mendix/mendix-studio-core/src/microflow/publish/PublishMicroflowModal.tsx");
const versions = read("packages/mendix/mendix-studio-core/src/microflow/versions/MicroflowVersionsDrawer.tsx");
const references = read("packages/mendix/mendix-studio-core/src/microflow/references/MicroflowReferencesDrawer.tsx");
const appConfig = read("apps/app-web/src/app/microflow-adapter-config.ts");

for (const code of [
  "MICROFLOW_UNAUTHORIZED",
  "MICROFLOW_PERMISSION_DENIED",
  "MICROFLOW_NOT_FOUND",
  "MICROFLOW_VERSION_CONFLICT",
  "MICROFLOW_VALIDATION_FAILED",
  "MICROFLOW_PUBLISH_BLOCKED",
  "MICROFLOW_METADATA_LOAD_FAILED",
  "MICROFLOW_NETWORK_ERROR",
  "MICROFLOW_TIMEOUT",
  "MICROFLOW_SERVICE_UNAVAILABLE",
]) {
  assertCheck(`错误码存在 ${code}`, codes.includes(code));
}

assertCheck("MicroflowApiError carries httpStatus/traceId/raw", envelope.includes("httpStatus?: number") && envelope.includes("traceId?: string") && envelope.includes("raw?: unknown"));
assertCheck("MicroflowApiException exists", error.includes("class MicroflowApiException extends Error"));
assertCheck("error utils exist", error.includes("getMicroflowErrorUserMessage") && error.includes("isVersionConflictError") && error.includes("isNetworkError"));
assertCheck("401 triggers onUnauthorized", client.includes("response.status === 401") && client.includes("onUnauthorized"));
assertCheck("403 triggers onForbidden", client.includes("response.status === 403") && client.includes("onForbidden"));
assertCheck("client catches network errors", client.includes("catch (caught)") && client.includes("normalizeMicroflowApiError(caught)"));
assertCheck("client rejects non-envelope JSON", client.includes("Microflow API response is not a valid envelope"));
assertCheck("ResourceTab uses unified error state", resourceTab.includes("MicroflowErrorState"));
assertCheck("EditorPage uses error helpers", editorPage.includes("isVersionConflictError") && editorPage.includes("MicroflowErrorState"));
assertCheck("MicroflowEditor catches save/validate/testRun", editor.includes("handleSave") && editor.includes("applyApiValidationIssues") && editor.includes("运行服务不可用"));
assertCheck("PublishModal keeps inline API error", publish.includes("apiError") && publish.includes("MicroflowErrorState"));
assertCheck("VersionsDrawer has error state", versions.includes("setError") && versions.includes("MicroflowErrorState"));
assertCheck("ReferencesDrawer has error state", references.includes("setError") && references.includes("MicroflowErrorState"));
assertCheck("app-web only passes callbacks", appConfig.includes("onUnauthorized") && appConfig.includes("onForbidden") && appConfig.includes("onApiError") && !appConfig.includes("MICROFLOW_NOT_FOUND") && !appConfig.includes("MICROFLOW_PERMISSION_DENIED"));

const microflowPackage = read("packages/mendix/mendix-microflow/src/editor/index.tsx")
  + read("packages/mendix/mendix-studio-core/src/microflow/resource/MicroflowResourceTab.tsx")
  + read("packages/mendix/mendix-studio-core/src/microflow/publish/PublishMicroflowModal.tsx");
assertCheck("microflow UI does not use browser alert", !/alert\s*\(/u.test(microflowPackage));

for (const check of checks) {
  console.log(`${check.ok ? "ok" : "fail"} - ${check.name}`);
}

const failed = checks.filter(check => !check.ok);
if (failed.length > 0) {
  console.error(`\n${failed.length} microflow HTTP error handling checks failed.`);
  process.exit(1);
}

console.log("\nMicroflow HTTP error handling checks passed.");
