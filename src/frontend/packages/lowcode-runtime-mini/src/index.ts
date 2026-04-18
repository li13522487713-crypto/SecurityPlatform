/**
 * Atlas 低代码 Mini 运行时（M15 C15-1 / C15-4 / C15-5）。
 *
 * - 复用 @atlas/lowcode-runtime-web 的 store / dispatch / events / context 抽象。
 * - 仅替换"组件渲染表"——mini 端按 @atlas/lowcode-components-mini 的能力矩阵降级。
 * - Schema 兼容：同一份 AppSchema 在 web / mini-wx / mini-douyin / h5 4 端跑通；
 *   不支持组件由 RuntimeRenderer 渲染为 fallback "[unsupported on mini]"。
 */

import type { ComponentSchema, RendererType } from '@atlas/lowcode-schema';
import { MINI_CAPABILITY_MATRIX, getMiniRenderers } from '@atlas/lowcode-components-mini';

export interface MiniRenderInfo {
  type: string;
  supported: boolean;
  fallbackText?: string;
}

export function pickRendererForMini(schema: ComponentSchema, target: RendererType): MiniRenderInfo {
  const renderers = getMiniRenderers(schema.type);
  const supported = renderers.includes(target);
  return supported ? { type: schema.type, supported } : { type: schema.type, supported: false, fallbackText: `[unsupported on ${target}: ${schema.type}]` };
}

/**
 * 校验整个 PageSchema 在指定 mini 渲染器下是否可降级渲染。
 * 返回不支持的组件 id 列表（用于设计期 / 预览期警告）。
 */
export function findUnsupportedComponents(root: ComponentSchema, target: RendererType): string[] {
  const out: string[] = [];
  walk(root, target, out);
  return out;
}

function walk(node: ComponentSchema, target: RendererType, out: string[]): void {
  if (!pickRendererForMini(node, target).supported) out.push(node.id);
  for (const child of node.children ?? []) walk(child, target, out);
  for (const slot of Object.values(node.slots ?? {})) for (const child of slot) walk(child, target, out);
}

export { MINI_CAPABILITY_MATRIX };
export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-runtime-mini' as const;
