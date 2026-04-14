import type { AppBuilderConfig, AppInputComponent, AppOutputComponent } from "../types";

export function newComponentId(): string {
  if (typeof globalThis.crypto !== "undefined" && typeof globalThis.crypto.randomUUID === "function") {
    return globalThis.crypto.randomUUID();
  }
  return `id-${Date.now()}-${Math.random().toString(36).slice(2, 11)}`;
}

export function createEmptyInput(): AppInputComponent {
  return {
    id: newComponentId(),
    type: "text",
    label: "",
    variableKey: "",
    required: false
  };
}

export function createEmptyOutput(): AppOutputComponent {
  return {
    id: newComponentId(),
    type: "text",
    label: "",
    sourceExpression: ""
  };
}

export function defaultAppBuilderConfig(): AppBuilderConfig {
  return {
    inputs: [],
    outputs: [],
    layoutMode: "form"
  };
}

/** 保存或运行前校验：变量键非空且不重复。 */
export function validateAppBuilderConfig(config: AppBuilderConfig): string | null {
  const keys = config.inputs.map(i => i.variableKey.trim()).filter(Boolean);
  const seen = new Set<string>();
  for (const k of keys) {
    if (seen.has(k)) {
      return "存在重复的变量键，请修改后再保存。";
    }
    seen.add(k);
  }
  for (const row of config.inputs) {
    if (!row.variableKey.trim()) {
      return "存在未填写变量键的输入项，请补全或删除该项。";
    }
  }
  return null;
}

/** 从预览接口返回的 outputs 中按表达式取值（支持顶层键与点路径）。 */
export function resolveOutputValue(sourceExpression: string, outputs: Record<string, unknown>): unknown {
  const key = sourceExpression.trim();
  if (!key) {
    return undefined;
  }
  if (Object.prototype.hasOwnProperty.call(outputs, key)) {
    return outputs[key];
  }
  const parts = key.split(".").filter(Boolean);
  let current: unknown = outputs;
  for (const part of parts) {
    if (current !== null && typeof current === "object" && part in (current as Record<string, unknown>)) {
      current = (current as Record<string, unknown>)[part];
    } else {
      return undefined;
    }
  }
  return current;
}
