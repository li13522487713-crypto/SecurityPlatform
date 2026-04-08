/**
 * AMIS Binding 适配器。
 *
 * 将 BindingResolver 生成的 DataBinding 声明
 * 转换为 AMIS 组件的 api / initApi 配置，
 * 自动注入到 schema 中。
 */

import type { AmisSchema } from "@/types/amis";
import type { SchemaBindingMap } from "../bindings/binding-types";

/**
 * 将已解析的 bindings 应用到 AMIS schema 上。
 * 遍历 schema 树，对 form/crud 节点自动补齐 api/initApi。
 */
export function applyAmisBindings(
  schema: AmisSchema,
  bindingMap: SchemaBindingMap,
): void {
  applyBindingsToNode(schema, bindingMap);
}

function applyBindingsToNode(
  node: unknown,
  bindingMap: SchemaBindingMap,
): void {
  if (!node || typeof node !== "object") return;

  const obj = node as Record<string, unknown>;

  if (obj.type === "form") {
    const formBinding = bindingMap.bindings.find((b) => b.kind === "form");
    if (formBinding && formBinding.kind === "form") {
      if (!obj.api) {
        obj.api = formBinding.submitUrl;
      }
      if (!obj.initApi && formBinding.initUrl && obj.dataTableKey) {
        obj.initApi = formBinding.initUrl;
      }
    }
  }

  if (obj.type === "crud") {
    const listBinding = bindingMap.bindings.find((b) => b.kind === "list");
    if (listBinding && listBinding.kind === "list") {
      if (!obj.api) {
        obj.api = listBinding.apiUrl;
      }
    }
  }

  for (const value of Object.values(obj)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        applyBindingsToNode(item, bindingMap);
      }
    } else if (value && typeof value === "object") {
      applyBindingsToNode(value, bindingMap);
    }
  }
}
