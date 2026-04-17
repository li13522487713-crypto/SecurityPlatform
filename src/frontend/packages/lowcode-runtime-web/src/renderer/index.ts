/**
 * RuntimeRenderer 协议层（M08 C08-1 / C08-2 / C08-7 / C08-8）。
 *
 * 设计要点：
 * - 本包暴露"无 React 副作用"的渲染描述符与 hooks（包外可独立单测）；
 * - 真正的 React `<RuntimeRenderer/>` 组件由调用方（lowcode-preview-web / lowcode-runtime-mini）装配，
 *   以避免本包强依赖 React 体系。
 *
 * 多端类型分发：
 *  - `web` → 调用方传入 web 渲染表
 *  - `mini_program` → M15 lowcode-components-mini
 *  - `hybrid` → 自动选择
 *
 * 生命周期 + 错误边界 + 性能埋点接口预留。
 */

import type { ComponentSchema, PageSchema } from '@atlas/lowcode-schema';

export type RenderMode = 'production' | 'preview' | 'debug';

export interface RenderDescriptor {
  schema: ComponentSchema;
  mode: RenderMode;
  parentId?: string;
}

/** 把 PageSchema 展开为按渲染顺序的扁平描述符列表。*/
export function flattenPage(page: PageSchema, mode: RenderMode = 'production'): RenderDescriptor[] {
  const out: RenderDescriptor[] = [];
  walk(page.root, undefined, mode, out);
  return out;
}

function walk(c: ComponentSchema, parentId: string | undefined, mode: RenderMode, out: RenderDescriptor[]): void {
  out.push({ schema: c, mode, parentId });
  for (const child of c.children ?? []) walk(child, c.id, mode, out);
  for (const slotChildren of Object.values(c.slots ?? {})) {
    for (const child of slotChildren) walk(child, c.id, mode, out);
  }
}

export interface PerformanceMark {
  componentId: string;
  phase: 'render' | 'event' | 'workflow' | 'chatflow';
  durationMs: number;
  startedAt: number;
}

const MARKS: PerformanceMark[] = [];

export function recordPerformanceMark(mark: PerformanceMark): void {
  MARKS.push(mark);
  // 简单截断防止内存膨胀
  if (MARKS.length > 5000) MARKS.splice(0, 1000);
}

export function readPerformanceMarks(): ReadonlyArray<PerformanceMark> {
  return MARKS;
}

export function __resetPerformanceMarksForTesting(): void {
  MARKS.length = 0;
}
