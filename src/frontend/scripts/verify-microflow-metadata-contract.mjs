/**
 * 门禁：微流「生产」子目录不得直接引用 mock 元数据符号。
 * 允许：*.spec.ts / *.spec.tsx / mock-metadata.ts / metadata-adapter.ts
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const MICROFLOW_SRC = path.resolve(__dirname, "../packages/mendix/mendix-microflow/src");
const GUARDED_REL_SEGMENTS = [
  ["validators"],
  ["expressions"],
  ["variables"],
  ["property-panel"],
  ["flowgram"],
];

const FORBIDDEN = [
  /mockMicroflowMetadataCatalog/,
  /mockEntities\b/,
  /mockAttributes\b/,
  /mockEnumerations\b/,
  /mockAssociations\b/,
];

function* walkFiles(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const e of entries) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) {
      yield* walkFiles(full);
    } else if (e.isFile() && /\.(ts|tsx)$/.test(e.name)) {
      yield full;
    }
  }
}

function isAllowedFile(absPath) {
  const base = path.basename(absPath);
  if (/\.spec\.(ts|tsx)$/.test(base)) {
    return true;
  }
  const rel = path.relative(MICROFLOW_SRC, absPath).replace(/\\/g, "/");
  if (rel === "metadata/mock-metadata.ts" || rel === "metadata/metadata-adapter.ts") {
    return true;
  }
  return false;
}

function isGuardedPath(absPath) {
  const rel = path.relative(MICROFLOW_SRC, absPath).replace(/\\/g, "/");
  return GUARDED_REL_SEGMENTS.some((segments) => segments.every((s, i) => rel.split("/")[i] === s));
}

const violations = [];
for (const file of walkFiles(MICROFLOW_SRC)) {
  if (!isGuardedPath(file) || isAllowedFile(file)) {
    continue;
  }
  const text = fs.readFileSync(file, "utf8");
  for (const re of FORBIDDEN) {
    if (re.test(text)) {
      violations.push(`${path.relative(MICROFLOW_SRC, file)}: matches ${re}`);
    }
  }
}

if (violations.length) {
  console.error("verify-microflow-metadata-contract: forbidden mock metadata references:\n" + violations.join("\n"));
  process.exit(1);
}

console.log("verify-microflow-metadata-contract: OK (guarded microflow paths clean).");
