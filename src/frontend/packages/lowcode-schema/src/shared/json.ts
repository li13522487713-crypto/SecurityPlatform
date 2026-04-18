/**
 * 通用 JSON 类型（避免在低代码协议层使用 any/unknown，遵守 AGENTS.md 强类型约束）。
 *
 * 所有需要承载"任意 JSON 值"的字段必须用 JsonValue。
 */

export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonValue[] | { [key: string]: JsonValue };
export interface JsonObject {
  [key: string]: JsonValue;
}
export type JsonArray = JsonValue[];
