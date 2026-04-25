#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../..');

const defaultCozeRoot = 'D:\\Code\\coze-studio-main';
const cozeRoot = path.resolve(process.argv[2] ?? process.env.COZE_STUDIO_ROOT ?? defaultCozeRoot);
const reportPath = path.resolve(repoRoot, 'docs/coze-source-drift-report.md');

const targetRoots = [
  'base/src',
  'nodes/src',
  'variable/src',
  'playground/src/node-registries',
  'playground/src/nodes-v2'
];

const sourceWorkflowRoot = path.join(cozeRoot, 'frontend/packages/workflow');
const localWorkflowRoot = path.join(repoRoot, 'src/frontend/packages/workflow');
const allowlistPath = path.join(__dirname, 'allowlist.json');

const allowlist = JSON.parse(await readFile(allowlistPath, 'utf8'));
const allowed = new Map(
  (allowlist.allowedDifferences ?? []).map(item => [toSlash(item.path), item.reason])
);

if (!existsSync(sourceWorkflowRoot)) {
  throw new Error(`Coze workflow root not found: ${sourceWorkflowRoot}`);
}

if (!existsSync(localWorkflowRoot)) {
  throw new Error(`Local workflow root not found: ${localWorkflowRoot}`);
}

const ignoredDirectories = new Set(['node_modules', 'dist', 'lib', 'coverage', '.turbo']);
const ignoredExtensions = new Set(['.map']);

function toSlash(value) {
  return value.replaceAll(path.sep, '/').replaceAll('\\', '/');
}

async function collectFiles(root, relativeRoot) {
  const absoluteRoot = path.join(root, relativeRoot);
  if (!existsSync(absoluteRoot)) {
    return [];
  }

  const results = [];

  async function walk(current) {
    const entries = await readdir(current, { withFileTypes: true });
    for (const entry of entries) {
      if (entry.isDirectory()) {
        if (!ignoredDirectories.has(entry.name)) {
          await walk(path.join(current, entry.name));
        }
        continue;
      }

      if (!entry.isFile()) {
        continue;
      }

      const ext = path.extname(entry.name);
      if (ignoredExtensions.has(ext)) {
        continue;
      }

      const absolutePath = path.join(current, entry.name);
      results.push(toSlash(path.relative(root, absolutePath)));
    }
  }

  await walk(absoluteRoot);
  return results;
}

function sha256(content) {
  return createHash('sha256').update(content).digest('hex');
}

async function readMaybe(root, relativePath) {
  const absolutePath = path.join(root, relativePath);
  if (!existsSync(absolutePath)) {
    return null;
  }

  return readFile(absolutePath, 'utf8');
}

function firstDifferentLine(left, right) {
  const leftLines = left.split(/\r?\n/);
  const rightLines = right.split(/\r?\n/);
  const max = Math.max(leftLines.length, rightLines.length);

  for (let i = 0; i < max; i += 1) {
    if ((leftLines[i] ?? '') !== (rightLines[i] ?? '')) {
      return {
        line: i + 1,
        local: leftLines[i] ?? '',
        coze: rightLines[i] ?? ''
      };
    }
  }

  return null;
}

const allPaths = new Set();
for (const root of targetRoots) {
  for (const file of await collectFiles(localWorkflowRoot, root)) {
    allPaths.add(file);
  }

  for (const file of await collectFiles(sourceWorkflowRoot, root)) {
    allPaths.add(file);
  }
}

const rows = [];
for (const relativePath of [...allPaths].sort()) {
  const localContent = await readMaybe(localWorkflowRoot, relativePath);
  const cozeContent = await readMaybe(sourceWorkflowRoot, relativePath);

  if (localContent === null) {
    rows.push({ relativePath, status: 'missing-local', allowed: false });
    continue;
  }

  if (cozeContent === null) {
    rows.push({ relativePath, status: 'atlas-only', allowed: allowed.has(relativePath) });
    continue;
  }

  const localHash = sha256(localContent);
  const cozeHash = sha256(cozeContent);
  if (localHash === cozeHash) {
    rows.push({ relativePath, status: 'same', allowed: true });
    continue;
  }

  rows.push({
    relativePath,
    status: 'different',
    allowed: allowed.has(relativePath),
    reason: allowed.get(relativePath),
    diff: firstDifferentLine(localContent, cozeContent)
  });
}

const summary = rows.reduce(
  (acc, row) => {
    acc[row.status] = (acc[row.status] ?? 0) + 1;
    if (row.status !== 'same' && !row.allowed) {
      acc.unallowed += 1;
    }
    return acc;
  },
  { same: 0, different: 0, 'missing-local': 0, 'atlas-only': 0, unallowed: 0 }
);

const now = new Date().toISOString();
const changedRows = rows.filter(row => row.status !== 'same');

const markdown = [
  '# Coze Source Drift Report',
  '',
  `Generated at: ${now}`,
  '',
  `Coze root: \`${cozeRoot}\``,
  '',
  `Local workflow root: \`${localWorkflowRoot}\``,
  '',
  '## Summary',
  '',
  `- Same files: ${summary.same}`,
  `- Different files: ${summary.different}`,
  `- Missing locally: ${summary['missing-local']}`,
  `- Atlas-only files: ${summary['atlas-only']}`,
  `- Unallowed drift count: ${summary.unallowed}`,
  '',
  '## Compared Roots',
  '',
  ...targetRoots.map(root => `- \`${root}\``),
  '',
  '## Drift Details',
  '',
  changedRows.length === 0 ? 'No drift detected.' : '',
  ...changedRows.flatMap(row => {
    const reason = row.allowed ? row.reason ?? 'allowed' : 'not allowed';
    const lines = [
      `### \`${row.relativePath}\``,
      '',
      `- Status: \`${row.status}\``,
      `- Allowlist: ${row.allowed ? 'yes' : 'no'} (${reason})`
    ];

    if (row.diff) {
      lines.push(
        `- First different line: ${row.diff.line}`,
        '',
        '```diff',
        `- ${row.diff.coze}`,
        `+ ${row.diff.local}`,
        '```'
      );
    }

    return [...lines, ''];
  }),
  '## CI Guidance',
  '',
  'Use this report as a drift guard. A non-zero unallowed drift count means a local workflow file differs from Coze upstream without a documented reason in `tools/coze-source-diff/allowlist.json`.'
].join('\n');

await mkdir(path.dirname(reportPath), { recursive: true });
await writeFile(reportPath, markdown, 'utf8');

console.log(`Coze source drift report written: ${reportPath}`);
console.log(`Unallowed drift count: ${summary.unallowed}`);

if (process.env.COZE_SOURCE_DIFF_STRICT === '1' && summary.unallowed > 0) {
  process.exitCode = 1;
}
