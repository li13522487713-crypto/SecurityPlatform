/**
 * jsonata 封装层（M02 C02-1）。
 *
 * - evaluate(expr, scope)：编译并执行
 * - evaluateAsync(expr, scope)：异步入口（jsonata 主体同步，但 binding 函数允许返回 Promise）
 * - compile(expr)：编译并缓存 AST，避免重复 parse 成本
 *
 * 强约束（PLAN.md §1.3）：
 * - 表达式不得直接调用 fetch / DOM API；jsonata 自身不暴露 IO，安全沙箱。
 * - 所有 binding 函数由调用方注入，禁止自动注入 import / require。
 */

import jsonata from 'jsonata';
import type { JsonValue } from '@atlas/lowcode-schema';

/** jsonata 编译结果（其类型由库导出，此处别名以便统一引用）。*/
export type CompiledExpression = ReturnType<typeof jsonata>;

const COMPILE_CACHE = new Map<string, CompiledExpression>();
const COMPILE_CACHE_MAX = 1000;

/** 编译并缓存。重复表达式直接命中缓存。*/
export function compile(expression: string): CompiledExpression {
  const cached = COMPILE_CACHE.get(expression);
  if (cached) return cached;
  const compiled = jsonata(expression);
  if (COMPILE_CACHE.size >= COMPILE_CACHE_MAX) {
    // 简单 LRU：清理一半，避免堆积。
    const keys = Array.from(COMPILE_CACHE.keys()).slice(0, Math.floor(COMPILE_CACHE_MAX / 2));
    for (const k of keys) COMPILE_CACHE.delete(k);
  }
  COMPILE_CACHE.set(expression, compiled);
  return compiled;
}

/** 同步语义入口（jsonata.evaluate 实际返回 Promise，但纯计算路径会立即 resolve）。*/
export async function evaluate(expression: string, scope: JsonValue): Promise<JsonValue> {
  const compiled = compile(expression);
  return (await compiled.evaluate(scope)) as JsonValue;
}

/** 异步入口的别名，便于语义层区分。*/
export const evaluateAsync = evaluate;

/** 仅供测试：清空缓存。*/
export function __clearCompileCacheForTesting(): void {
  COMPILE_CACHE.clear();
}
