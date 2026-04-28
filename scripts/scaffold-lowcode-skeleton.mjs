#!/usr/bin/env node
// 一次性创建当前保留的 lowcode-* package 骨架（M00 预创建）。
// 此脚本只生成 package.json / src/index.ts / README.md / tsconfig.json，不写实现。
// 真正的实现由 M01-M20 各里程碑分别填充。

import { promises as fs } from "node:fs";
import path from "node:path";

const ROOT = path.resolve(new URL(".", import.meta.url).pathname, "..");
const PKG_ROOT = path.join(ROOT, "src/frontend/packages");

// 当前保留包的定义，与实际仓库清单保持一致。
const PACKAGES = [
  // 协议层
  { dir: "lowcode-schema", name: "@atlas/lowcode-schema", desc: "Atlas 低代码 Schema 协议层（M01）。" },
  { dir: "lowcode-expression", name: "@atlas/lowcode-expression", desc: "Atlas 低代码表达式引擎（jsonata + 模板 + 7 作用域，M02）。" },
  { dir: "lowcode-action-runtime", name: "@atlas/lowcode-action-runtime", desc: "Atlas 低代码动作运行时（7 内置动作 + 编排，M03）。" },
  // 设计器
  { dir: "lowcode-editor-canvas", name: "@atlas/lowcode-editor-canvas", desc: "Atlas 低代码画布（dnd-kit + 三布局 + 快捷键，M04）。" },
  { dir: "lowcode-editor-outline", name: "@atlas/lowcode-editor-outline", desc: "Atlas 低代码结构树（M05）。" },
  { dir: "lowcode-editor-inspector", name: "@atlas/lowcode-editor-inspector", desc: "Atlas 低代码检查器（属性/样式/事件三 Tab，M05）。" },
  { dir: "lowcode-property-forms", name: "@atlas/lowcode-property-forms", desc: "Atlas 低代码属性表单 + 5 种值源 + 6 类内容参数（M05）。" },
  { dir: "lowcode-component-registry", name: "@atlas/lowcode-component-registry", desc: "Atlas 低代码组件注册表 + 元数据驱动校验器（M06）。" },
  { dir: "lowcode-components-web", name: "@atlas/lowcode-components-web", desc: "Atlas 低代码 Web 组件库（30+ 组件 + AI 原生，M06）。" },
  { dir: "lowcode-components-mini", name: "@atlas/lowcode-components-mini", desc: "Atlas 低代码 Mini 组件库（Taro 多端，M15）。" },
  // 运行时
  { dir: "lowcode-runtime-web", name: "@atlas/lowcode-runtime-web", desc: "Atlas 低代码 Web 运行时（M08）。" },
  { dir: "lowcode-runtime-mini", name: "@atlas/lowcode-runtime-mini", desc: "Atlas 低代码 Mini 运行时（M15）。" },
  // 适配器
  { dir: "lowcode-workflow-adapter", name: "@atlas/lowcode-workflow-adapter", desc: "Atlas 低代码 Workflow 适配器（M09）。" },
  { dir: "lowcode-chatflow-adapter", name: "@atlas/lowcode-chatflow-adapter", desc: "Atlas 低代码 Chatflow 适配器（M11）。" },
  { dir: "lowcode-asset-adapter", name: "@atlas/lowcode-asset-adapter", desc: "Atlas 低代码 Asset 适配器（M10）。" },
  // 周边
  { dir: "lowcode-debug-client", name: "@atlas/lowcode-debug-client", desc: "Atlas 低代码调试台 + 6 维 trace（M13）。" },
  { dir: "lowcode-versioning-client", name: "@atlas/lowcode-versioning-client", desc: "Atlas 低代码版本管理（M14）。" },
  { dir: "lowcode-web-sdk", name: "@atlas/lowcode-web-sdk", desc: "Atlas 低代码 Web SDK（M17）。" },
  { dir: "lowcode-collab-yjs", name: "@atlas/lowcode-collab-yjs", desc: "Atlas 低代码协同编辑（Yjs + 自定义 SignalR provider，M16）。" }
];

const TSCONFIG = {
  compilerOptions: {
    target: "ES2022",
    module: "ESNext",
    moduleResolution: "bundler",
    jsx: "react-jsx",
    lib: ["ES2022", "DOM", "DOM.Iterable"],
    strict: true,
    noUnusedLocals: true,
    noUnusedParameters: true,
    noImplicitOverride: true,
    noFallthroughCasesInSwitch: true,
    esModuleInterop: true,
    skipLibCheck: true,
    forceConsistentCasingInFileNames: true,
    isolatedModules: true,
    declaration: true,
    declarationMap: true,
    sourceMap: true,
    types: []
  },
  include: ["src"]
};

function makePkgJson(name, desc) {
  return {
    name,
    private: true,
    version: "0.0.0",
    description: desc,
    type: "module",
    main: "src/index.ts",
    types: "src/index.ts",
    exports: {
      ".": "./src/index.ts"
    },
    scripts: {
      lint: `echo '${name}: no lint configured yet'`,
      build: `echo '${name}: source-only package, no build step'`,
      test: `echo '${name}: no tests yet'`
    }
  };
}

function makeIndexTs(name) {
  return `// ${name} — 由 M00 预创建的空骨架。
// 真实导出由对应里程碑（见 PLAN.md）逐步填充。
// 当前导出此占位常量以避免 ts isolatedModules 报"空模块"错误。

export const __ATLAS_LOWCODE_PACKAGE__ = ${JSON.stringify(name)};
`;
}

function makeReadme(name, desc) {
  return `# ${name}

> ${desc}

此包为 M00 预创建的骨架。具体能力将由对应里程碑实现，禁止在该里程碑之外引入实现。

- 完整规格请见仓库根 \`PLAN.md\` 与 \`docs/lowcode-*-spec.md\`。
- 严格遵守 docx §十一推荐 10 项栈，禁止引入未授权依赖。
`;
}

async function ensureDir(p) {
  await fs.mkdir(p, { recursive: true });
}

async function writeIfMissing(file, content) {
  try {
    await fs.access(file);
    // 已存在则跳过，避免覆盖里程碑实现
    return false;
  } catch {
    await fs.writeFile(file, content);
    return true;
  }
}

async function main() {
  let created = 0;
  let skipped = 0;
  for (const pkg of PACKAGES) {
    const dir = path.join(PKG_ROOT, pkg.dir);
    await ensureDir(path.join(dir, "src"));
    const pj = path.join(dir, "package.json");
    const tj = path.join(dir, "tsconfig.json");
    const ix = path.join(dir, "src/index.ts");
    const rd = path.join(dir, "README.md");

    let touched = false;
    if (await writeIfMissing(pj, JSON.stringify(makePkgJson(pkg.name, pkg.desc), null, 2) + "\n")) touched = true;
    if (await writeIfMissing(tj, JSON.stringify(TSCONFIG, null, 2) + "\n")) touched = true;
    if (await writeIfMissing(ix, makeIndexTs(pkg.name))) touched = true;
    if (await writeIfMissing(rd, makeReadme(pkg.name, pkg.desc))) touched = true;
    if (touched) created++; else skipped++;
  }
  // eslint-disable-next-line no-console
  console.log(`[scaffold-lowcode-skeleton] packages created/touched=${created}, fully-skipped=${skipped}`);
}

main().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err);
  process.exit(1);
});
