/**
 * 对比 runtime-messages.ts 中 zh-CN 与 en-US 的词条路径差集。
 * 用法：在 Atlas.WebApp 目录执行 `node scripts/i18n-runtime-key-diff.mjs`
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(__dirname, "..");
const filePath = path.join(appRoot, "src", "i18n", "runtime-messages.ts");
const source = fs.readFileSync(filePath, "utf8");

const { outputText } = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2022,
    esModuleInterop: true
  },
  fileName: filePath
});

const patched = outputText
  .replace(/^\s*["']use strict["'];\s*/m, "")
  .replace(/exports\.runtimeMessages\s*=\s*void\s*0;?\s*/g, "")
  .replace(/exports\.runtimeMessages\s*=\s*/, "globalThis.__RM = ");

const moduleStub = { exports: {} };
// eslint-disable-next-line no-new-func
const run = new Function("exports", "module", "globalThis", `${patched}\nreturn globalThis.__RM;`);
const runtimeMessages = run(moduleStub.exports, moduleStub, globalThis);

function collectKeys(obj, prefix = "") {
  /** @type {string[]} */
  const keys = [];
  if (obj === null || typeof obj !== "object" || Array.isArray(obj)) {
    return keys;
  }
  for (const [k, v] of Object.entries(obj)) {
    const p = prefix ? `${prefix}.${k}` : k;
    if (v !== null && typeof v === "object" && !Array.isArray(v)) {
      keys.push(...collectKeys(v, p));
    } else {
      keys.push(p);
    }
  }
  return keys;
}

const zh = runtimeMessages["zh-CN"];
const en = runtimeMessages["en-US"];
if (!zh || !en) {
  console.error("Missing zh-CN or en-US root in runtimeMessages");
  process.exit(1);
}

const zhKeys = new Set(collectKeys(zh));
const enKeys = new Set(collectKeys(en));

const onlyZh = [...zhKeys].filter((k) => !enKeys.has(k)).sort();
const onlyEn = [...enKeys].filter((k) => !zhKeys.has(k)).sort();

console.log(`runtime-messages leaf keys: zh-CN=${zhKeys.size}, en-US=${enKeys.size}`);
console.log(`\n--- Only in zh-CN (${onlyZh.length}) ---`);
onlyZh.forEach((k) => console.log(k));
console.log(`\n--- Only in en-US (${onlyEn.length}) ---`);
onlyEn.forEach((k) => console.log(k));
