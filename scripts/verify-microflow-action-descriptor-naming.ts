import { existsSync } from "node:fs";
import { resolve } from "node:path";
import {
  detectLegacyAliasesInText,
  findWorkspaceRoot,
  LEGACY_ACTION_ALIASES,
  parseBackendDescriptors,
  readWorkspaceFile,
  type CheckResult
} from "./microflow-production-gate-lib.ts";

function print(results: CheckResult[]): void {
  for (const result of results) {
    const tag = result.status === "pass" ? "ok" : result.status;
    console.log(`${tag} - ${result.id}: ${result.summary}`);
    for (const detail of result.details ?? []) {
      console.log(`  - ${detail}`);
    }
  }
}

function main(): void {
  const root = findWorkspaceRoot();
  const results: CheckResult[] = [];
  const descriptors = parseBackendDescriptors(root);
  const canonicalKinds = new Set(descriptors.map(descriptor => descriptor.actionKind));
  const descriptorAliases = [...LEGACY_ACTION_ALIASES].filter(alias => canonicalKinds.has(alias));
  results.push({
    id: "descriptor-canonical-action-kind",
    status: descriptorAliases.length === 0 ? "pass" : "fail",
    summary: descriptorAliases.length === 0
      ? "后端 BuiltInDescriptors 未注册禁止进入 schema 的旧 actionKind。"
      : "后端 BuiltInDescriptors 注册了禁止进入 schema 的旧 actionKind。",
    details: descriptorAliases
  });

  const scannedFiles = [
    "src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs",
    "src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts",
    "src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts",
    "src/frontend/packages/mendix/mendix-studio-core/src/microflow/contracts/examples/sample-requests.ts",
    "docs/microflow/production-node-capability-matrix.md"
  ];
  const violations: string[] = [];
  for (const file of scannedFiles) {
    if (!existsSync(resolve(root, file))) {
      continue;
    }
    const aliases = detectLegacyAliasesInText(readWorkspaceFile(file, root))
      .filter(alias => !["aggregate", "filter", "sort"].includes(alias));
    const allowedInDocs = file.endsWith("production-node-capability-matrix.md")
      ? aliases.filter(alias => ["aggregate", "filter", "sort"].includes(alias))
      : [];
    const unexpected = aliases.filter(alias => !allowedInDocs.includes(alias));
    for (const alias of unexpected) {
      violations.push(`${file}: ${alias}`);
    }
  }
  results.push({
    id: "legacy-alias-not-in-schema-or-registry",
    status: violations.length === 0 ? "pass" : "fail",
    summary: violations.length === 0
      ? "旧 actionKind 别名未进入 schema 样例、前端 registry 或后端 descriptor。"
      : "旧 actionKind 别名进入了 schema 样例、前端 registry 或后端 descriptor。",
    details: violations
  });

  const namingDoc = "docs/microflow/contracts/action-kind-naming.md";
  const descriptorDoc = "docs/microflow/contracts/action-descriptor-naming.md";
  const docsOk = existsSync(resolve(root, namingDoc))
    && existsSync(resolve(root, descriptorDoc))
    && LEGACY_ACTION_ALIASES.every(alias => readWorkspaceFile(namingDoc, root).includes(alias));
  results.push({
    id: "naming-documents",
    status: docsOk ? "pass" : "fail",
    summary: docsOk
      ? "命名规范文档存在，并列出禁止旧别名与迁移策略。"
      : "命名规范文档缺失，或未覆盖全部禁止旧别名。",
    details: docsOk ? undefined : [namingDoc, descriptorDoc]
  });

  print(results);
  const failed = results.filter(result => result.status === "fail");
  if (failed.length > 0) {
    console.error(`\n${failed.length} microflow action descriptor naming checks failed.`);
    process.exit(1);
  }
  console.log("\nMicroflow action descriptor naming checks passed.");
}

main();
