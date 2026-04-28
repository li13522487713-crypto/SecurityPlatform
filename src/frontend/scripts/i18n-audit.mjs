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
const scanPackages = hasFlag("--scan-packages");
const checkLabelsCoverage = hasFlag("--check-labels-coverage");

const supportedTargets = new Set(["app-web"]);
if (!supportedTargets.has(target)) {
  console.error(`[i18n-audit] 无效 target: ${target}`);
  process.exit(2);
}

const repoRoot = path.resolve(process.cwd(), "..");
const frontendRoot = path.join(repoRoot, "frontend");
const appSrc = path.join(frontendRoot, "apps", target, "src");
const localeZhPath = path.join(appSrc, "app", "messages.ts");
const localeEnPath = path.join(appSrc, "app", "messages.ts");
const baselinePath = path.join(frontendRoot, "scripts", "i18n-baseline.json");

function loadLocaleObject(filePath, locale) {
  const raw = fs.readFileSync(filePath, "utf8");
  const transformed = `${raw
    .replace(/^\s*import\s+.*$/gm, "")
    .replace(/^\s*export\s+type\s+.*$/gm, "")
    .replace(/:\s*typeof\s+\w+\s*=/g, " =")
    .replace(/\s+as\s+const/g, "")
    .replace(/^\s*const\s+/gm, "const ")
    .replace(/export\s+const\s+APP_MESSAGES\s*=\s*/, "module.exports = ")}\nmodule.exports = module.exports["${locale}"];`;
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
    if (!/\.(ts|tsx)$/.test(entry.name)) continue;
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

const files = walkSourceFiles(appSrc);
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

/* =============================================================
 * 包级 i18n 收口校验（Atlas 自有 packages，不含 coze 上游 fork）
 * ----------------------------------------------------------- */

function loadBaseline() {
  const fallback = {
    allowedCjkPackages: [],
    allowedCjkFiles: [],
    skipLabelsCoveragePackages: [],
    skipLabelsCoverageConsts: [],
  };
  if (!fs.existsSync(baselinePath)) return fallback;
  try {
    return { ...fallback, ...JSON.parse(fs.readFileSync(baselinePath, "utf8")) };
  } catch (err) {
    console.error(`[i18n-audit] 无法解析 baseline: ${err.message}`);
    return fallback;
  }
}

function listAtlasPackages() {
  const packagesDir = path.join(frontendRoot, "packages");
  if (!fs.existsSync(packagesDir)) return [];
  const out = [];
  for (const entry of fs.readdirSync(packagesDir, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const pkgRoot = path.join(packagesDir, entry.name);
    const pkgJsonPath = path.join(pkgRoot, "package.json");
    if (!fs.existsSync(pkgJsonPath)) continue;
    let pkgJson;
    try {
      pkgJson = JSON.parse(fs.readFileSync(pkgJsonPath, "utf8"));
    } catch {
      continue;
    }
    if (typeof pkgJson.name !== "string" || !pkgJson.name.startsWith("@atlas/")) {
      continue;
    }
    out.push({ name: pkgJson.name, dirName: entry.name, root: pkgRoot, pkgJson });
  }
  return out;
}

function stripCommentsPreservingLines(source) {
  let result = source.replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, " "));
  result = result.replace(/(^|[^:\\])\/\/[^\n]*/g, (_match, prefix) => prefix + "");
  return result;
}

function fileHasCjk(content) {
  return /[\u4e00-\u9fff]/.test(content);
}

function isExemptFile(relPath) {
  if (/[\\/](__tests__|__mocks__|mock|mocks|fixtures)[\\/]/i.test(relPath)) return true;
  if (/\.(test|spec|stories|fixture|fixtures)\.(ts|tsx)$/i.test(relPath)) return true;
  return false;
}

const cjkFindings = [];
if (scanPackages) {
  const baseline = loadBaseline();
  const allowedPkgSet = new Set(baseline.allowedCjkPackages || []);
  const allowedFileSet = new Set((baseline.allowedCjkFiles || []).map((p) => p.replace(/\\/g, "/")));

  for (const pkg of listAtlasPackages()) {
    if (allowedPkgSet.has(pkg.dirName)) continue;
    const srcDir = path.join(pkg.root, "src");
    if (!fs.existsSync(srcDir)) continue;
    const tsFiles = walkSourceFiles(srcDir);
    for (const filePath of tsFiles) {
      const relRaw = path.relative(frontendRoot, filePath).replace(/\\/g, "/");
      if (isExemptFile(relRaw)) continue;
      if (allowedFileSet.has(relRaw)) continue;
      const raw = fs.readFileSync(filePath, "utf8");
      const stripped = stripCommentsPreservingLines(raw);
      if (!fileHasCjk(stripped)) continue;
      const lines = raw.split("\n");
      const strippedLines = stripped.split("\n");
      const hits = [];
      for (let i = 0; i < lines.length; i += 1) {
        if (!/[\u4e00-\u9fff]/.test(strippedLines[i] ?? "")) continue;
        hits.push({ line: i + 1, text: lines[i].trim().slice(0, 160) });
      }
      if (hits.length === 0) continue;
      cjkFindings.push({ packageDir: pkg.dirName, packageName: pkg.name, file: relRaw, hits });
    }
  }

  console.log(`\n[i18n-audit] scan-packages: Atlas 自有包 ${listAtlasPackages().length} 个，CJK 硬编码命中文件=${cjkFindings.length}`);
  if (cjkFindings.length > 0) {
    const byPkg = new Map();
    for (const f of cjkFindings) {
      if (!byPkg.has(f.packageDir)) byPkg.set(f.packageDir, []);
      byPkg.get(f.packageDir).push(f);
    }
    for (const [pkgDir, items] of byPkg) {
      console.log(`  [package] ${pkgDir} (${items.length} files)`);
      for (const it of items) {
        console.log(`    - ${it.file} (hits=${it.hits.length})`);
        for (const hit of it.hits.slice(0, 3)) {
          console.log(`        L${hit.line}: ${hit.text}`);
        }
        if (it.hits.length > 3) {
          console.log(`        ... +${it.hits.length - 3} more`);
        }
      }
    }
  }
}

/* =============================================================
 * 包级 Labels 覆盖校验：包导出 XXX_LABELS_KEYS 时，宿主必须穷举注入
 * ----------------------------------------------------------- */

function extractLabelsKeysExports(filePath) {
  const content = fs.readFileSync(filePath, "utf8");
  /* 同一文件内提取所有 *Labels 类型名与 default*Labels 常量名作为宿主接管候选；
     用 RegExp literal 避免字符串模式跨平台（PowerShell）的转义干扰。 */
  const labelTypes = [];
  const labelTypeRe = /export\s+type\s+([A-Z][A-Za-z0-9]*Labels)\b/g;
  let lt;
  while ((lt = labelTypeRe.exec(content)) !== null) labelTypes.push(lt[1]);

  const defaultConsts = [];
  const defaultConstRe = /export\s+const\s+(default[A-Z][A-Za-z0-9]*Labels)\b/g;
  let dc;
  while ((dc = defaultConstRe.exec(content)) !== null) defaultConsts.push(dc[1]);

  const out = [];
  const exportPattern =
    /export\s+const\s+([A-Z][A-Z0-9_]*_LABELS_KEYS)\s*=\s*\[([\s\S]*?)\]\s*as\s+const/g;
  let match = exportPattern.exec(content);
  while (match) {
    const constName = match[1];
    const arrBody = match[2];
    const keys = [];
    const keyPattern = /['"]([a-zA-Z0-9_]+)['"]/g;
    let km = keyPattern.exec(arrBody);
    while (km) {
      keys.push(km[1]);
      km = keyPattern.exec(arrBody);
    }
    out.push({ constName, keys, labelTypes, defaultConsts });
    match = exportPattern.exec(content);
  }
  return out;
}

function findHostLabelsUsage(exp, files) {
  const candidates = [exp.constName, ...(exp.labelTypes ?? []), ...(exp.defaultConsts ?? [])];
  if (candidates.length === 0) return [];
  const re = new RegExp(`\\b(?:${candidates.join("|")})\\b`);
  const out = [];
  for (const filePath of files) {
    const content = fs.readFileSync(filePath, "utf8");
    if (re.test(content)) {
      out.push(path.relative(frontendRoot, filePath).replace(/\\/g, "/"));
    }
  }
  return out;
}

const labelsCoverageMisses = [];
if (checkLabelsCoverage) {
  const baseline = loadBaseline();
  const skipPkgSet = new Set(baseline.skipLabelsCoveragePackages || []);
  const hostFiles = walkSourceFiles(appSrc);

  const exportedLabelsConsts = [];
  for (const pkg of listAtlasPackages()) {
    if (skipPkgSet.has(pkg.dirName)) continue;
    const srcDir = path.join(pkg.root, "src");
    if (!fs.existsSync(srcDir)) continue;
    for (const filePath of walkSourceFiles(srcDir)) {
      if (isExemptFile(filePath)) continue;
      const found = extractLabelsKeysExports(filePath);
      for (const item of found) {
        exportedLabelsConsts.push({
          packageDir: pkg.dirName,
          packageName: pkg.name,
          file: path.relative(frontendRoot, filePath).replace(/\\/g, "/"),
          constName: item.constName,
          keys: item.keys,
          labelTypes: item.labelTypes,
          defaultConsts: item.defaultConsts,
        });
      }
    }
  }

  console.log(`\n[i18n-audit] check-labels-coverage: Atlas 自有包导出 LABELS_KEYS 常量 ${exportedLabelsConsts.length} 个`);

  const skipConstsSet = new Set(baseline.skipLabelsCoverageConsts || []);
  for (const exp of exportedLabelsConsts) {
    if (skipConstsSet.has(exp.constName)) {
      console.log(`  [SKIP] ${exp.packageName} :: ${exp.constName} (baseline 豁免)`);
      continue;
    }
    const usages = findHostLabelsUsage(exp, hostFiles);
    if (usages.length === 0) {
      labelsCoverageMisses.push({
        packageName: exp.packageName,
        constName: exp.constName,
        reason: "未在 app-web 任何文件中引用（包括 Labels 类型与 default 常量）",
        keys: exp.keys
      });
      console.log(`  [MISS] ${exp.packageName} :: ${exp.constName} (candidates=${[exp.constName, ...(exp.labelTypes ?? []), ...(exp.defaultConsts ?? [])].join(',')})`);
    } else {
      console.log(`  [OK]   ${exp.packageName} :: ${exp.constName} -- ${usages.length} 处使用`);
    }
  }
}

/* =============================================================
 * 退出码：strict 模式下任一项目失败都退出 1
 * ----------------------------------------------------------- */

let strictFailReason = null;
if (strict) {
  if (unresolvedZh.length > 0 || unresolvedEn.length > 0) {
    strictFailReason = "app-web messages 中英 keys 未对齐";
  } else if (scanPackages && cjkFindings.length > 0) {
    strictFailReason = "Atlas 自有包内仍有 CJK 硬编码（未在 baseline 豁免）";
  } else if (checkLabelsCoverage && labelsCoverageMisses.length > 0) {
    strictFailReason = "包导出的 LABELS_KEYS 未在宿主中引用，宿主未穷举注入";
  }
}

if (strictFailReason) {
  console.error(`[i18n-audit] strict 模式检查失败：${strictFailReason}`);
  process.exit(1);
}
