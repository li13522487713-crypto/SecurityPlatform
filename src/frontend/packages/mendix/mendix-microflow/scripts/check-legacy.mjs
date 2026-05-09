import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const packageRoot = path.resolve(__dirname, "..");
const patterns = [
  { key: "LegacyMicroflow", query: "LegacyMicroflow" },
  { key: "schema/legacy", query: "schema/legacy" },
  { key: "legacy-host-layout", query: "legacy-host-layout" },
  { key: "NativeMicroflowEditor", query: "NativeMicroflowEditor" },
  { key: "p0-action-runtime", query: "p0-action-runtime" },
  { key: "legacyActivityType", query: "legacyActivityType" },
  { key: "toLegacyGraph", query: "toLegacyGraph" },
  { key: "ensureAuthoringSchema", query: "ensureAuthoringSchema" },
];

function walkSourceFiles(rootDir) {
  const files = [];
  const stack = [rootDir];
  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) {
      continue;
    }
    const entries = readdirSync(current, { withFileTypes: true });
    for (const entry of entries) {
      const absolutePath = path.join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(absolutePath);
        continue;
      }
      if (!entry.isFile()) {
        continue;
      }
      if (absolutePath.endsWith(".ts") || absolutePath.endsWith(".tsx")) {
        files.push(absolutePath);
      }
    }
  }
  return files;
}

function countPatternInSource(files, query) {
  let count = 0;
  for (const file of files) {
    const lines = readFileSync(file, "utf8").split(/\r?\n/u);
    for (const line of lines) {
      if (line.includes(query)) {
        count += 1;
      }
    }
  }
  return count;
}

function collectSnapshot() {
  const sourceFiles = walkSourceFiles(path.resolve(packageRoot, "src"));
  const snapshot = {};
  for (const patternDef of patterns) {
    snapshot[patternDef.key] = countPatternInSource(sourceFiles, patternDef.query);
  }
  return snapshot;
}

function printSnapshot(title, snapshot) {
  console.log(title);
  for (const patternDef of patterns) {
    const count = snapshot[patternDef.key] ?? 0;
    console.log(`- ${patternDef.key}: ${count}`);
  }
}

function checkSnapshot(snapshot) {
  const violations = [];

  for (const patternDef of patterns) {
    const key = patternDef.key;
    const current = snapshot[key] ?? 0;
    if (current > 0) {
      violations.push({ key, current });
    }
  }

  printSnapshot("Current legacy snapshot:", snapshot);
  if (violations.length === 0) {
    console.log("Legacy guard check passed.");
    return;
  }

  console.error("Legacy guard check failed. Legacy debt must stay at zero:");
  for (const item of violations) {
    console.error(`- ${item.key}: current=${item.current}`);
  }
  process.exit(1);
}

const snapshot = collectSnapshot();
checkSnapshot(snapshot);
