import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";
import { spawnSync } from "node:child_process";

type Check = {
  name: string;
  ok: boolean;
  details?: string;
};

const root = process.cwd();
const checks: Check[] = [];

function add(name: string, ok: boolean, details = ""): void {
  checks.push({ name, ok, details });
}

function read(path: string): string {
  return readFileSync(resolve(root, path), "utf8");
}

function walk(dir: string, files: string[] = []): string[] {
  if (!existsSync(dir)) {
    return files;
  }

  for (const entry of readdirSync(dir)) {
    if (["node_modules", "dist", "bin", "obj", ".git"].includes(entry)) {
      continue;
    }

    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      walk(full, files);
      continue;
    }

    if (/\.(ts|tsx|js|mjs|cs|json)$/u.test(entry)) {
      files.push(full);
    }
  }

  return files;
}

function matching(files: string[], pattern: RegExp, allow: (path: string) => boolean = () => false): string[] {
  return files
    .map(file => ({ full: file, rel: relative(root, file).replaceAll("\\", "/") }))
    .filter(item => !allow(item.rel))
    .filter(item => pattern.test(readFileSync(item.full, "utf8")))
    .map(item => item.rel);
}

const appWebFiles = walk(resolve(root, "src/frontend/apps/app-web/src"));
const studioFiles = walk(resolve(root, "src/frontend/packages/mendix/mendix-studio-core/src"));
const microflowFiles = walk(resolve(root, "src/frontend/packages/mendix/mendix-microflow/src"));
const backendFiles = walk(resolve(root, "src/backend"));

const frontendScript = spawnSync("pnpm", ["--dir", "src/frontend", "run", "verify:microflow-no-production-mock"], {
  cwd: root,
  shell: process.platform === "win32",
  encoding: "utf8",
});
add("frontend no production mock verify", frontendScript.status === 0, frontendScript.stdout + frontendScript.stderr);

const appForbidden = matching(appWebFiles, /mock-microflow|local-microflow|microflow-resource-storage|createLocalMicroflow|createMockMicroflow/u);
add("app-web production path does not import mock/local resource adapters", appForbidden.length === 0, appForbidden.join(", "));

const mswStart = read("src/frontend/apps/app-web/src/main.tsx");
add("app-web production does not start MSW", mswStart.includes("VITE_MICROFLOW_API_MOCK") && (mswStart.includes("import.meta.env.PROD !== true") || mswStart.includes("env.PROD !== true")), "src/frontend/apps/app-web/src/main.tsx");

const adapterConfig = read("src/frontend/packages/mendix/mendix-studio-core/src/microflow/config/microflow-adapter-config.ts");
add("mendix-studio-core production default adapter is http", adapterConfig.includes('defaultMode: "http"'));
add("mendix-studio-core production forbids mock/local", adapterConfig.includes("allowMock: false") && adapterConfig.includes("allowLocal: false"));
add("mendix-studio-core production forbids mock/local fallback", adapterConfig.includes("allowMockFallback: false") && adapterConfig.includes("allowLocalFallback: false"));

const localStorageLeaks = matching([...appWebFiles, ...studioFiles, ...microflowFiles], /atlas_mendix_microflow_resources_v1|atlas_microflow_resources_v1|mendix\.microflow\.resources/u, path =>
  path.endsWith("microflow-resource-storage.ts")
  || path.endsWith("local-microflow-resource-adapter.ts")
  || path.includes(".spec.")
  || path.endsWith("runtime-adapter/local-adapter.ts")
);
add("microflow localStorage resource keys are limited to local adapter/storage/tests", localStorageLeaks.length === 0, localStorageLeaks.join(", "));

const backendMockFallback = matching(backendFiles, /fallback\s+mock|mock catalog|getDefaultMockMetadataCatalog|SeedEnabled.*true/su, path =>
  path.includes("/bin/")
  || path.includes("/obj/")
  || path.includes("appsettings.Development.json")
  || path.includes("appsettings.Production.json")
  || path.includes("MicroflowMockRuntimeRunner.cs")
  || path.includes("MicroflowRuntimeHttpModels.cs")
);
add("backend production path does not advertise mock catalog fallback/default seed", backendMockFallback.length === 0, backendMockFallback.join(", "));

const productionConfig = read("src/backend/Atlas.AppHost/appsettings.Production.json");
add("production disables metadata seed", productionConfig.includes('"SeedEnabled": false') && productionConfig.includes('"ForceSeed": false'));
add("production disables real HTTP and private network by default", productionConfig.includes('"AllowRealHttp": false') && productionConfig.includes('"AllowPrivateNetwork": false'));
add("production disables internal debug defaults", productionConfig.includes('"EnableInternalDebugApi": false') && productionConfig.includes('"EnableVerboseTrace": false'));

const sampleAppLeaks = matching([...appWebFiles, ...studioFiles], /SAMPLE_PROCUREMENT_APP|"app_procurement"|'app_procurement'|"mod_procurement"|'mod_procurement'|MENDIX_STUDIO_DEV_SAMPLE_APP_ID/u, path =>
  // sample 数据本体允许保留，由 store / dev sample 卡片显式守卫
  path.endsWith("packages/mendix/mendix-studio-core/src/sample-app.ts")
  // 仅 dev 模式下加载示例数据（store.loadSampleApp / mendix-studio-index-page dev sample 卡片）
  || path.endsWith("packages/mendix/mendix-studio-core/src/store.ts")
  || path.endsWith("packages/mendix/mendix-studio-core/src/components/studio-header.tsx")
  || path.endsWith("packages/mendix/mendix-studio-core/src/mendix-studio-index-page.tsx")
  // 测试与 fixtures 不参与生产构建
  || path.includes(".spec.")
  || path.includes("__tests__")
);
add("app-web/studio-core production path does not embed Procurement sample identifiers", sampleAppLeaks.length === 0, sampleAppLeaks.join(", "));

for (const check of checks) {
  console.log(`${check.ok ? "ok" : "fail"} - ${check.name}${check.ok || !check.details ? "" : `: ${check.details}`}`);
}

const failed = checks.filter(check => !check.ok);
if (failed.length > 0) {
  console.error(`\n${failed.length} microflow production no-mock checks failed.`);
  process.exit(1);
}

console.log("\nMicroflow production no-mock checks passed.");
