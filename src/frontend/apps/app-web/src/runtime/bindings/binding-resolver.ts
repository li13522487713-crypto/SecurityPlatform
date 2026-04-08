/**
 * Schema Binding 解析器。
 *
 * 遍历 AMIS schema，识别 form/crud 组件，
 * 生成 DataBinding 声明。替代原 PageRuntimeRenderer 中的
 * applyRuntimeApis() 硬编码 URL 注入。
 */

import { buildRuntimeRecordsUrl } from "@/services/api-runtime";
import type { AmisSchema } from "@/types/amis";
import type { DataBinding, SchemaBindingMap } from "./binding-types";

let bindingCounter = 0;

function nextBindingKey(prefix: string): string {
  bindingCounter += 1;
  return `${prefix}_${bindingCounter}`;
}

export function resolveBindings(
  schema: AmisSchema,
  pageKey: string,
  appKey: string,
): SchemaBindingMap {
  bindingCounter = 0;
  const bindings: DataBinding[] = [];
  collectBindings(schema, pageKey, appKey, bindings);
  return { bindings };
}

function collectBindings(
  node: unknown,
  pageKey: string,
  appKey: string,
  bindings: DataBinding[],
): void {
  if (!node || typeof node !== "object") return;

  const obj = node as Record<string, unknown>;
  const baseUrl = buildRuntimeRecordsUrl(pageKey, appKey);

  if (obj.type === "form") {
    const entityKey = String(obj.dataTableKey ?? pageKey);
    const binding: DataBinding = {
      kind: "form",
      key: nextBindingKey("form"),
      entityKey,
      mode: "create",
      submitUrl: `post:${baseUrl}`,
    };

    if (obj.dataTableKey) {
      (binding as { initUrl?: string }).initUrl = `get:${baseUrl}/\${id}`;
    }

    bindings.push(binding);
  }

  if (obj.type === "crud") {
    const entityKey = String(obj.dataTableKey ?? pageKey);
    bindings.push({
      kind: "list",
      key: nextBindingKey("list"),
      entityKey,
      apiUrl: baseUrl,
    });
  }

  for (const value of Object.values(obj)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        collectBindings(item, pageKey, appKey, bindings);
      }
    } else if (value && typeof value === "object") {
      collectBindings(value, pageKey, appKey, bindings);
    }
  }
}
