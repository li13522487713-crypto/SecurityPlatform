#!/usr/bin/env node
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const root = process.cwd();
const checks = [];

function walk(dir, files = []) {
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules" || entry === "dist" || entry === ".git") {
      continue;
    }
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      walk(full, files);
      continue;
    }
    if (/\.(ts|tsx|mjs|js)$/u.test(entry)) {
      files.push(full);
    }
  }
  return files;
}

function read(path) {
  return readFileSync(resolve(root, path), "utf8");
}

function assertCheck(name, ok, details = "") {
  checks.push({ name, ok: Boolean(ok), details });
}

function filesMatching(files, pattern, allow = () => false) {
  return files
    .filter(file => !allow(relative(root, file).replaceAll("\\", "/")))
    .filter(file => pattern.test(readFileSync(file, "utf8")))
    .map(file => relative(root, file).replaceAll("\\", "/"));
}

const appFiles = walk(resolve(root, "apps/app-web/src"));
const studioFiles = walk(resolve(root, "packages/mendix/mendix-studio-core/src"));
const microflowFiles = walk(resolve(root, "packages/mendix/mendix-microflow/src"));

const appForbiddenImports = filesMatching(appFiles, /from\s+["'][^"']*(mock-microflow|local-microflow|microflow-resource-storage|adapter\/http|MicroflowApiClient|mock-metadata|mock-test-runner|@atlas\/microflow)/u);
assertCheck("app-web 不 import 微流 mock/local/http 内部或 @atlas/microflow", appForbiddenImports.length === 0, appForbiddenImports.join(", "));

const appMicroflowStorage = filesMatching(appFiles, /mendix\.microflow|atlas_microflow|atlas_mendix_microflow/u);
assertCheck("app-web 不读写微流 localStorage 数据键", appMicroflowStorage.length === 0, appMicroflowStorage.join(", "));

const appMicroflowFetch = filesMatching(appFiles, /fetch\s*\([^)]*(\/api\/microflows|microflow-metadata)/su);
assertCheck("app-web 不直接 fetch 微流 API", appMicroflowFetch.length === 0, appMicroflowFetch.join(", "));

const httpForbidden = filesMatching(studioFiles.filter(file => relative(root, file).replaceAll("\\", "/").includes("/adapter/http/") || relative(root, file).replaceAll("\\", "/").endsWith("/metadata/http-metadata-adapter.ts")), /mock-microflow|local-microflow|mock-metadata|mock-test-runner|createLocalMicroflowApiClient/u);
assertCheck("HTTP adapters 不 import mock/local", httpForbidden.length === 0, httpForbidden.join(", "));

const metadataForbidden = filesMatching(
  microflowFiles.filter(file => /\/(validators|expression|expressions|variables|property-panel)\//u.test(relative(root, file).replaceAll("\\", "/"))),
  /mock-metadata|getDefaultMockMetadataCatalog|createMockMicroflowMetadataAdapter/u,
);
assertCheck("validators/expression/variables/property-panel 不 import mock metadata", metadataForbidden.length === 0, metadataForbidden.join(", "));

const resourceStorageUsers = filesMatching([...studioFiles, ...microflowFiles], /mendix\.microflow\.resources|atlas_mendix_microflow_resources_v1|atlas_microflow_resources_v1/u, path =>
  path.endsWith("microflow-resource-storage.ts")
  || path.endsWith("local-microflow-resource-adapter.ts")
  || path.endsWith("runtime-adapter/local-adapter.ts")
  || path.includes(".spec.")
);
assertCheck("微流资源 localStorage 键仅在 local adapter/storage 中出现", resourceStorageUsers.length === 0, resourceStorageUsers.join(", "));

const config = read("packages/mendix/mendix-studio-core/src/microflow/config/microflow-adapter-config.ts");
assertCheck("production defaultMode=http", config.includes('case "production"') && config.includes('defaultMode: "http"'));
assertCheck("production allowMock=false", config.includes("allowMock: false"));
assertCheck("production allowLocal=false", config.includes("allowLocal: false"));
assertCheck("production allowMockFallback=false", config.includes("allowMockFallback: false"));
assertCheck("production allowLocalFallback=false", config.includes("allowLocalFallback: false"));

const factory = read("packages/mendix/mendix-studio-core/src/microflow/adapter/microflow-adapter-factory.ts");
assertCheck("AdapterFactory 使用 runtime policy 校验", factory.includes("validateMicroflowAdapterConfig") && factory.includes("runtimePolicy"));

for (const check of checks) {
  console.log(`${check.ok ? "ok" : "fail"} - ${check.name}${check.ok || !check.details ? "" : `: ${check.details}`}`);
}

const failed = checks.filter(check => !check.ok);
if (failed.length > 0) {
  console.error(`\n${failed.length} production mock boundary checks failed.`);
  process.exit(1);
}

console.log("\nMicroflow production mock boundary checks passed.");
