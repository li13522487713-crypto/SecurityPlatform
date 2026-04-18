/**
 * loading / error 状态自动挂载（M03 C03-4）。
 *
 * 与 docx §七 Workflow.loading / Workflow.error 自动绑定语义对齐。
 * 单纯产出 RuntimeStatePatch 数组，由 dispatcher 走 commitPatches 提交。
 */

import type { RuntimeStatePatch } from '@atlas/lowcode-schema';

export function buildLoadingPatches(targets: ReadonlyArray<string>, loading: boolean): RuntimeStatePatch[] {
  return targets.map((id) => ({
    scope: 'component',
    componentId: id,
    path: `component.${id}.loading`,
    op: loading ? 'set' : 'set',
    value: loading
  }));
}

export function buildErrorPatches(targets: ReadonlyArray<string>, error: { message: string; kind?: string } | null): RuntimeStatePatch[] {
  return targets.map((id) => ({
    scope: 'component',
    componentId: id,
    path: `component.${id}.error`,
    op: error ? 'set' : 'unset',
    value: error ?? undefined
  }));
}
