import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";

const args = process.argv.slice(2);

function readArg(name, fallback = "") {
  const index = args.findIndex((arg) => arg === name);
  if (index < 0 || index === args.length - 1) return fallback;
  return args[index + 1];
}

function hasFlag(name) {
  return args.includes(name);
}

const target = readArg("--target", "app-web");
const strict = hasFlag("--strict");
const allowAutoFill = hasFlag("--allow-auto-fill");
const includeUnused = hasFlag("--include-unused");

const supportedTargets = new Set(["app-web", "platform-web"]);
if (!supportedTargets.has(target)) {
  console.error(`[i18n-audit] 无效 target: ${target}`);
  process.exit(2);
}

const repoRoot = path.resolve(process.cwd(), "..");
const appSrc = path.join(repoRoot, "frontend", "apps", target, "src");
const sharedUiSrc = path.join(repoRoot, "frontend", "packages", "shared-ui", "src");
const localeZhPath = target === "app-web"
  ? path.join(appSrc, "app", "messages.ts")
  : path.join(appSrc, "i18n", "zh-CN.ts");
const localeEnPath = target === "app-web"
  ? path.join(appSrc, "app", "messages.ts")
  : path.join(appSrc, "i18n", "en-US.ts");

function loadLocaleObject(filePath, locale) {
  const raw = fs.readFileSync(filePath, "utf8");
  const transformed =
    target === "app-web"
      ? `${raw
          .replace(/^\s*import\s+.*$/gm, "")
          .replace(/^\s*export\s+type\s+.*$/gm, "")
          .replace(/:\s*typeof\s+\w+\s*=/g, " =")
          .replace(/\s+as\s+const/g, "")
          .replace(/^\s*const\s+/gm, "const ")
          .replace(/export\s+const\s+APP_MESSAGES\s*=\s*/, "module.exports = ")}\nmodule.exports = module.exports["${locale}"];`
      : raw
          .replace(/^\s*import\s+.*$/gm, "")
          .replace(/^\s*export\s+default\s+/, "module.exports = ");
  const sandbox = { module: { exports: {} }, exports: {} };
  vm.runInNewContext(transformed, sandbox, { filename: filePath });
  return sandbox.module.exports;
}

function flattenKeys(obj, prefix = "", output = new Set()) {
  if (!obj || typeof obj !== "object") return output;
  for (const [key, value] of Object.entries(obj)) {
    const next = prefix ? `${prefix}.${key}` : key;
    if (value && typeof value === "object" && !Array.isArray(value)) {
      flattenKeys(value, next, output);
      continue;
    }
    output.add(next);
  }
  return output;
}

function walkSourceFiles(dirPath, output = []) {
  if (!fs.existsSync(dirPath)) return output;
  for (const entry of fs.readdirSync(dirPath, { withFileTypes: true })) {
    const absolutePath = path.join(dirPath, entry.name);
    if (entry.isDirectory()) {
      if (["node_modules", "dist", "playwright-report", "test-results"].includes(entry.name)) continue;
      walkSourceFiles(absolutePath, output);
      continue;
    }
    if (!/\.(vue|ts|tsx)$/.test(entry.name)) continue;
    output.push(absolutePath);
  }
  return output;
}

function extractUsedI18nKeys(filePaths) {
  const keys = new Set();
  const pattern = /(?:\bt|\$t)\(\s*['"]([a-zA-Z0-9_.-]+)['"]/g;

  for (const filePath of filePaths) {
    const content = fs.readFileSync(filePath, "utf8");
    let match = pattern.exec(content);
    while (match) {
      keys.add(match[1]);
      match = pattern.exec(content);
    }
  }
  return keys;
}

function splitTokens(key) {
  return key
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[._-]/g, " ")
    .split(/\s+/)
    .map((token) => token.trim())
    .filter(Boolean);
}

function canAutoFill(key) {
  return key.includes(".") && splitTokens(key).length > 1;
}

function buildNamespaceStats(keys) {
  const stats = new Map();
  for (const key of keys) {
    const namespace = key.split(".")[0] || "unknown";
    stats.set(namespace, (stats.get(namespace) ?? 0) + 1);
  }
  return Array.from(stats.entries()).sort((a, b) => b[1] - a[1]);
}

const localeZh = loadLocaleObject(localeZhPath, "zh-CN");
const localeEn = loadLocaleObject(localeEnPath, "en-US");
const localeZhKeys = flattenKeys(localeZh);
const localeEnKeys = flattenKeys(localeEn);

const files = target === "app-web"
  ? walkSourceFiles(appSrc)
  : [...walkSourceFiles(appSrc), ...walkSourceFiles(sharedUiSrc)];
const usedKeys = extractUsedI18nKeys(files);

const missingZh = [];
const missingEn = [];
for (const key of usedKeys) {
  if (!localeZhKeys.has(key)) missingZh.push(key);
  if (!localeEnKeys.has(key)) missingEn.push(key);
}

missingZh.sort();
missingEn.sort();

const unresolvedZh = allowAutoFill ? missingZh.filter((key) => !canAutoFill(key)) : missingZh;
const unresolvedEn = allowAutoFill ? missingEn.filter((key) => !canAutoFill(key)) : missingEn;

const autoFillZhCount = missingZh.length - unresolvedZh.length;
const autoFillEnCount = missingEn.length - unresolvedEn.length;

console.log(`\n[i18n-audit] target=${target}`);
console.log(`[i18n-audit] used keys=${usedKeys.size}`);
console.log(`[i18n-audit] zh missing=${missingZh.length}, unresolved=${unresolvedZh.length}, autofill=${autoFillZhCount}`);
console.log(`[i18n-audit] en missing=${missingEn.length}, unresolved=${unresolvedEn.length}, autofill=${autoFillEnCount}`);

if (missingZh.length > 0 || missingEn.length > 0) {
  const unionMissing = Array.from(new Set([...missingZh, ...missingEn])).sort();
  const namespaceStats = buildNamespaceStats(unionMissing);
  console.log("[i18n-audit] missing namespace top:");
  for (const [namespace, count] of namespaceStats.slice(0, 20)) {
    console.log(`  - ${namespace}: ${count}`);
  }
}

if (unresolvedZh.length > 0) {
  console.log("\n[i18n-audit] unresolved zh keys:");
  for (const key of unresolvedZh) {
    console.log(`  ${key}`);
  }
}

if (unresolvedEn.length > 0) {
  console.log("\n[i18n-audit] unresolved en keys:");
  for (const key of unresolvedEn) {
    console.log(`  ${key}`);
  }
}

if (includeUnused) {
  const usedKeySet = new Set(usedKeys);
  const unusedZh = Array.from(localeZhKeys).filter((key) => !usedKeySet.has(key)).sort();
  const unusedEn = Array.from(localeEnKeys).filter((key) => !usedKeySet.has(key)).sort();
  console.log(`\n[i18n-audit] unused zh=${unusedZh.length}, unused en=${unusedEn.length}`);
}

if (strict && (unresolvedZh.length > 0 || unresolvedEn.length > 0)) {
  console.error("[i18n-audit] strict 模式检查失败。");
  process.exit(1);
}
