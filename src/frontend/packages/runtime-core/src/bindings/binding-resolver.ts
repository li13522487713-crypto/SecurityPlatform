/**
 * Schema Binding 解析器。
 *
 * 遍历 AMIS schema，识别 form/crud 组件，生成 DataBinding 声明。
 */

import type { AmisSchema } from "../types";
import type { DataBinding, SchemaBindingMap } from "./binding-types";

export interface BindingResolverOptions {
  buildRuntimeRecordsUrl?: (pageKey: string, appKey?: string) => string;
}

let bindingCounter = 0;

function nextBindingKey(prefix: string): string {
  bindingCounter += 1;
  return `${prefix}_${bindingCounter}`;
}

function buildDefaultRuntimeUrl(pageKey: string): string {
  return `/api/app/runtime/pages/${encodeURIComponent(pageKey)}/records`;
}

export function resolveBindings(
  schema: AmisSchema,
  pageKey: string,
  appKey: string,
  options: BindingResolverOptions = {},
): SchemaBindingMap {
  bindingCounter = 0;
  const bindings: DataBinding[] = [];
  collectBindings(schema, pageKey, appKey, options, bindings);
  return { bindings };
}

function collectBindings(
  node: unknown,
  pageKey: string,
  appKey: string,
  options: BindingResolverOptions,
  bindings: DataBinding[],
): void {
  if (!node || typeof node !== "object") return;

  const obj = node as Record<string, unknown>;
  const buildUrl = options.buildRuntimeRecordsUrl ?? buildDefaultRuntimeUrl;
  const baseUrl = buildUrl(pageKey, appKey);

  if (obj.type === "form") {
    const entityKey = String(obj.dataTableKey ?? pageKey);
    const binding: DataBinding = {
      kind: "form",
      key: nextBindingKey("form"),
      entityKey,
      mode: "create",
      submitUrl: `post:${baseUrl}`,
      ...(obj.dataTableKey ? { initUrl: `get:${baseUrl}/\${id}` } : {}),
    };
    bindings.push(binding);
  }

  if (obj.type === "crud") {
    const entityKey = String(obj.dataTableKey ?? pageKey);
    bindings.push({
      kind: "list",
      key: nextBindingKey("list"),
      entityKey,
      apiUrl: `get:${baseUrl}`,
    });
  }

  for (const value of Object.values(obj)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        collectBindings(item, pageKey, appKey, options, bindings);
      }
    } else if (value && typeof value === "object") {
      collectBindings(value, pageKey, appKey, options, bindings);
    }
  }
}
