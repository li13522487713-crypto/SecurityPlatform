/**
 * 运行时基础类型定义。
 *
 * 所有 runtime 子模块共享的通用类型集中于此，
 * 避免各模块重复定义或硬编码字符串字面量。
 */

export type Id = string;
export type Key = string;

export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];
export interface JsonObject {
  [key: string]: JsonValue;
}

export type StringMap = Record<string, string>;
export type ValueMap = Record<string, unknown>;

export type RuntimeEntryMode = "public-runtime" | "workspace-runtime";
export type RuntimePageMode = "view" | "edit" | "create";
export type RuntimeStatus = "ready" | "running" | "success" | "failed" | "cancelled";
