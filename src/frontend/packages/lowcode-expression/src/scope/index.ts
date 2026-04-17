/**
 * 作用域守卫与隔离（M02 C02-3 / C02-4）。
 *
 * 7 种作用域根：page / app / system / component / event / workflow.outputs / chatflow.outputs
 * - 可写：page / app
 * - 只读：system / component / event / workflow.outputs / chatflow.outputs
 *
 * 与 @atlas/lowcode-schema/guards 的 isWritableScope / isReadonlyScope / inferScopeRoot / assertWritable 完全对齐；
 * 此模块在表达式引擎调用点提供更细粒度的"仅读"模式与"读写"模式入口（与 M03 action-runtime 双层校验互补）。
 */

import {
  ScopeViolationError,
  inferScopeRoot,
  isReadonlyScope,
  isWritableScope,
  assertWritable
} from '@atlas/lowcode-schema/guards';
import type { ScopeRoot } from '@atlas/lowcode-schema/shared';

export { ScopeViolationError, inferScopeRoot, isReadonlyScope, isWritableScope, assertWritable };

/** 表达式只读模式（默认）：禁止写入任何作用域。仅当读取时调用 inferScopeRoot 即可。*/
export function ensureReadOnly(path: string): void {
  // 表达式默认场景为读取，因此无需校验；保留入口以便 Monaco 提示与未来扩展。
  void path;
}

/**
 * 表达式写入模式：仅 set_variable 动作或显式写入场景调用。
 * 对应 PLAN.md §M02 C02-4 + §M03 C03-5（action-runtime scope-guard）双层校验。
 */
export function ensureWritablePath(path: string): void {
  assertWritable(path);
}

/** 用于 Monaco LSP：返回路径所属的作用域根（未识别返回 undefined）。*/
export function classifyScope(path: string): ScopeRoot | undefined {
  return inferScopeRoot(path);
}
