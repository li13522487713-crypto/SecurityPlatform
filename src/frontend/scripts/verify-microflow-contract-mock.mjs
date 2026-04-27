import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const root = new URL("..", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/u, "$1");

function read(path) {
  return readFileSync(join(root, path), "utf8");
}

function walk(dir) {
  return readdirSync(dir).flatMap(name => {
    const path = join(dir, name);
    const stat = statSync(path);
    return stat.isDirectory() ? walk(path) : [path];
  });
}

const checks = [];

function check(name, condition, details = "") {
  checks.push({ name, condition, details });
}

const openapi = read("../../docs/microflow/contracts/openapi-draft.yaml");
const mockIndex = read("packages/mendix/mendix-studio-core/src/microflow/contracts/mock-api/index.ts");
const responseHelper = read("packages/mendix/mendix-studio-core/src/microflow/contracts/mock-api/mock-api-response.ts");
const store = read("packages/mendix/mendix-studio-core/src/microflow/contracts/mock-api/mock-api-store.ts");
const browser = read("packages/mendix/mendix-studio-core/src/microflow/contracts/mock-api/browser.ts");
const node = read("packages/mendix/mendix-studio-core/src/microflow/contracts/mock-api/node.ts");
const appMain = read("apps/app-web/src/main.tsx");
const appConfig = read("apps/app-web/src/app/microflow-adapter-config.ts");
const appFiles = walk(join(root, "apps/app-web/src")).filter(file => /\.(ts|tsx)$/u.test(file));

const pathMatches = [...openapi.matchAll(/^  (\/api\/[^\s:]+):$/gmu)].map(match => match[1]);
const missingPaths = pathMatches.filter(path => !mockIndex.includes(path));
check("OpenAPI draft paths have contract mock declaration", missingPaths.length === 0, missingPaths.join(", "));

for (const helper of ["ok", "fail", "validationFailed", "notFound", "forbidden", "unauthorized", "versionConflict", "publishBlocked", "serviceUnavailable", "withTraceId"]) {
  check(`response helper ${helper} exists`, responseHelper.includes(`function ${helper}`) || responseHelper.includes(`function ${helper}<`));
}

for (const endpoint of ["listResources", "createResource", "schema/migrate", "microflow-metadata", "validateMockMicroflow", "publishMockMicroflow", "compare-current", "references", "impact", "test-run", "runs/:runId/trace"]) {
  const files = walk(join(root, "packages/mendix/mendix-studio-core/src/microflow/contracts/mock-api")).map(file => readFileSync(file, "utf8"));
  check(`mock endpoint coverage: ${endpoint}`, files.some(content => content.includes(endpoint)));
}

for (const scenario of ["unauthorized", "forbidden", "not-found", "version-conflict", "validation-failed", "publish-blocked", "service-unavailable", "network"]) {
  check(`error scenario ${scenario}`, read("packages/mendix/mendix-studio-core/src/microflow/contracts/mock-api/mock-error-handlers.ts").includes(`"${scenario}"`));
}

check("mock store reset exists", store.includes("resetMicroflowContractMockStore"));
check("mock store seeds published resource", store.includes("mf-order-process"));
check("mock store seeds changedAfterPublish resource", store.includes("mf-order-breaking"));
check("mock store seeds archived resource", store.includes("mf-legacy-cleanup"));
check("mock store seeds rest sample", store.includes("mf-rest-error-handling"));
check("mock store seeds loop sample", store.includes("mf-loop-processing"));
check("mock store seeds object type decision sample", store.includes("mf-object-type-decision"));
check("browser worker uses setupWorker", browser.includes("setupWorker"));
check("browser worker blocks production", browser.includes("isProduction()"));
check("node server uses setupServer", node.includes("setupServer"));
check("app-web starts public helper only", appMain.includes("startMicroflowContractMockWorker"));
check("app-web mock mode forces http", appConfig.includes('contractMockEnabled ? "http"'));
check("app-web mock apiBaseUrl defaults /api", appConfig.includes('?? "/api"'));
check("MSW worker file exists", statSync(join(root, "apps/app-web/public/mockServiceWorker.js")).isFile());

const forbiddenAppImports = appFiles
  .map(file => ({ file, content: readFileSync(file, "utf8") }))
  .filter(item => /mock-api\/|mock-api-store|mock-.*handlers|local-microflow|mock-microflow-resource-adapter/u.test(item.content))
  .map(item => relative(root, item.file));
check("app-web does not import mock store/handlers/local adapters", forbiddenAppImports.length === 0, forbiddenAppImports.join(", "));

const failed = checks.filter(item => !item.condition);
if (failed.length) {
  console.error("verify-microflow-contract-mock: failed");
  for (const item of failed) {
    console.error(`- ${item.name}${item.details ? `: ${item.details}` : ""}`);
  }
  process.exit(1);
}

console.log(`verify-microflow-contract-mock: pass (${checks.length} checks)`);
