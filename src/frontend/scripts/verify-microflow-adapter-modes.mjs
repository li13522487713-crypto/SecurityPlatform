#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = process.cwd();
const read = path => readFileSync(resolve(root, path), "utf8");
const checks = [];

function assertCheck(name, condition) {
  checks.push({ name, ok: Boolean(condition) });
}

const factory = read("packages/mendix/mendix-studio-core/src/microflow/adapter/microflow-adapter-factory.ts");
const config = read("packages/mendix/mendix-studio-core/src/microflow/config/microflow-adapter-config.ts");
const httpClient = read("packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-client.ts");
const httpResource = read("packages/mendix/mendix-studio-core/src/microflow/adapter/http/http-resource-adapter.ts");
const httpRuntime = read("packages/mendix/mendix-studio-core/src/microflow/adapter/http/http-runtime-adapter.ts");
const validation = read("packages/mendix/mendix-studio-core/src/microflow/adapter/microflow-validation-adapter.ts");
const appResource = read("apps/app-web/src/app/pages/microflow-resource-tab.tsx");
const appEditor = read("apps/app-web/src/app/pages/microflow-editor-page.tsx");
const appConfig = read("apps/app-web/src/app/microflow-adapter-config.ts");

assertCheck("MicroflowAdapterMode exists", config.includes('type MicroflowAdapterMode = "mock" | "local" | "http"'));
assertCheck("MicroflowAdapterFactoryConfig exists", config.includes("interface MicroflowAdapterFactoryConfig"));
assertCheck("MicroflowAdapterBundle exists", factory.includes("interface MicroflowAdapterBundle"));
assertCheck("mock bundle factory exists", factory.includes("createMockMicroflowAdapterBundle"));
assertCheck("local bundle factory exists", factory.includes("createLocalMicroflowAdapterBundle"));
assertCheck("http bundle factory exists", factory.includes("createHttpMicroflowAdapterBundle"));
assertCheck("main bundle factory exists", factory.includes("createMicroflowAdapterBundle"));
assertCheck("MicroflowApiClient exists", httpClient.includes("class MicroflowApiClient"));
assertCheck("HTTP ResourceAdapter maps publish", httpResource.includes("/publish"));
assertCheck("HTTP MetadataAdapter uses ApiClient", read("packages/mendix/mendix-studio-core/src/microflow/metadata/http-metadata-adapter.ts").includes("MicroflowApiClient"));
assertCheck("HTTP RuntimeAdapter maps test run", httpRuntime.includes("/test-run"));
assertCheck("ValidationAdapter local/http exists", validation.includes("createLocalMicroflowValidationAdapter") && validation.includes("createHttpMicroflowValidationAdapter"));
assertCheck("app-web passes adapterConfig to resource tab", appResource.includes("adapterConfig={createAppMicroflowAdapterConfig"));
assertCheck("app-web passes adapterConfig to editor page", appEditor.includes("adapterConfig={createAppMicroflowAdapterConfig"));
assertCheck("app-web config only imports public core", appConfig.includes("@atlas/mendix-studio-core") && !appConfig.includes("adapter/http"));
assertCheck("production default is not mock", config.includes('return isProduction ? "http" : "local"'));
assertCheck("http adapter does not import mock adapter", !httpResource.includes("mock") && !httpRuntime.includes("mock"));
assertCheck("app-web does not directly fetch microflow", !/fetch\s*\(/u.test(appResource + appEditor + appConfig));
assertCheck("app-web does not directly touch localStorage microflow data", !(appResource + appEditor + appConfig).includes("localStorage"));

const failed = checks.filter(check => !check.ok);
for (const check of checks) {
  console.log(`${check.ok ? "ok" : "fail"} - ${check.name}`);
}

if (failed.length > 0) {
  console.error(`\n${failed.length} microflow adapter mode checks failed.`);
  process.exit(1);
}

console.log("\nMicroflow adapter mode checks passed.");
