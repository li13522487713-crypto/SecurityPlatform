import type { MicroflowObject } from "../schema";
import type { MicroflowNodeFormRegistry, MicroflowNodeFormRegistryItem } from "./types";

/**
 * P1-1: 抽出独立模块以便外部包扩展，且不会因 ObjectPanel 间接 import semi-icons 等
 * 重型依赖而影响轻量测试 / SSR 启动。
 */
export function getMicroflowNodeFormKey(object: MicroflowObject): string {
  return object.kind === "actionActivity" ? `activity:${object.action.kind}` : object.kind;
}

export const microflowNodeFormRegistry: MicroflowNodeFormRegistry = {};

export interface RegisterMicroflowNodeFormOptions {
  readonly allowOverride?: boolean;
}

export function registerMicroflowNodeForm(
  key: string,
  item: MicroflowNodeFormRegistryItem,
  options: RegisterMicroflowNodeFormOptions = {},
): void {
  if (!key || typeof key !== "string") {
    throw new Error("registerMicroflowNodeForm: key must be a non-empty string");
  }
  if (!item || typeof item.renderProperties !== "function") {
    throw new Error("registerMicroflowNodeForm: item.renderProperties must be a function");
  }
  if (microflowNodeFormRegistry[key] && !options.allowOverride) {
    throw new Error(`registerMicroflowNodeForm: form for key "${key}" already registered; pass allowOverride=true to replace.`);
  }
  microflowNodeFormRegistry[key] = item;
}

export function unregisterMicroflowNodeForm(key: string): void {
  delete microflowNodeFormRegistry[key];
}

export function getMicroflowNodeFormForObject(object: MicroflowObject): MicroflowNodeFormRegistryItem | undefined {
  return microflowNodeFormRegistry[getMicroflowNodeFormKey(object)];
}
