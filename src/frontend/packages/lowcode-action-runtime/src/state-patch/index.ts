/**
 * 事务式状态补丁（M03 C03-3）。
 *
 * - 基于 immer 的不可变更新。
 * - 一次执行链可累积多个 RuntimeStatePatch；commit 成功 → 合并到主状态；任意一步失败 → 整体回滚。
 * - 与 dispatch 协议（M13）输出的 statePatches 完全对齐（@atlas/lowcode-schema/runtime）。
 *
 * 注：补丁的 path 已经包含作用域前缀（page.* / app.* / component.<id>.*）。
 *      跨作用域写入受 scope-guard 模块拦截，本模块不重复校验。
 */

import { produce, type Draft } from 'immer';
import type { JsonObject, JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';

/**
 * 应用单个补丁到草稿状态。
 * 路径形式：a.b.c[0].d；本模块 M03 阶段仅支持纯 dot-path（M05 属性面板编辑保证）。
 */
export function applyPatch(draft: Draft<JsonObject>, patch: RuntimeStatePatch): void {
  const segments = parsePath(patch.path);
  if (segments.length === 0) return;

  // 走到倒数第二段，沿途自动建对象。
  // immer 的 Draft 类型递归收敛较慢，这里以 unknown 桥接更稳；
  // 业务上保证 path 已经经过 scope-guard 校验。
  let cur: unknown = draft;
  for (let i = 0; i < segments.length - 1; i++) {
    const seg = segments[i];
    const next = (cur as Record<string, unknown>)[seg];
    if (next === undefined || next === null || typeof next !== 'object') {
      (cur as Record<string, unknown>)[seg] = {};
    }
    cur = (cur as Record<string, unknown>)[seg];
  }
  const last = segments[segments.length - 1];

  switch (patch.op) {
    case 'set':
      (cur as Record<string, unknown>)[last] = patch.value as unknown;
      break;
    case 'merge': {
      const existing = (cur as Record<string, unknown>)[last];
      if (existing && typeof existing === 'object' && !Array.isArray(existing) && patch.value && typeof patch.value === 'object' && !Array.isArray(patch.value)) {
        (cur as Record<string, unknown>)[last] = { ...(existing as object), ...(patch.value as object) };
      } else {
        (cur as Record<string, unknown>)[last] = patch.value as unknown;
      }
      break;
    }
    case 'unset':
      delete (cur as Record<string, unknown>)[last];
      break;
  }
}

/** 一组补丁的事务式提交：成功合并；失败回滚（保持原状态）。*/
export function commitPatches(state: JsonObject, patches: ReadonlyArray<RuntimeStatePatch>): { next: JsonObject; applied: number } {
  if (patches.length === 0) return { next: state, applied: 0 };
  const next = produce(state, (draft: Draft<JsonObject>) => {
    for (const p of patches) {
      applyPatch(draft, p);
    }
  });
  return { next, applied: patches.length };
}

/** 安全 dot-path 解析；不支持 [index] 语法（M03 阶段已知约束）。*/
export function parsePath(path: string): string[] {
  return path.split('.').filter((s) => s.length > 0);
}

/** 仅供测试。*/
export function readPath(state: JsonObject, path: string): JsonValue | undefined {
  const segs = parsePath(path);
  let cur: unknown = state;
  for (const s of segs) {
    if (cur === undefined || cur === null || typeof cur !== 'object') return undefined;
    cur = (cur as Record<string, unknown>)[s];
  }
  return cur as JsonValue | undefined;
}
