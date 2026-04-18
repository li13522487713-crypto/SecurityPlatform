/**
 * @atlas/lowcode-components-mini — Mini 端组件库（M15 C15-2 / C15-4）。
 *
 * 设计：
 *  - 直接复用 @atlas/lowcode-components-web 中的 ALL_METAS 列表，对每个组件做 runtimeRenderer 替换；
 *  - 不支持的组件（CodeEditor / Chart / WaterfallList 等）退化为占位组件；
 *  - 通过 MINI_CAPABILITY_MATRIX 暴露各组件在三种 mini 端（mini-wx / mini-douyin / h5）的支持度。
 */

import type { ComponentMeta, RendererType } from '@atlas/lowcode-schema';
import { ALL_METAS } from '@atlas/lowcode-components-web';
import { registerComponent, type ComponentImplementationDescriptor } from '@atlas/lowcode-component-registry';

const MINI_RENDERERS: ReadonlyArray<RendererType> = ['mini-wx', 'mini-douyin', 'h5'];

/** 组件 → 在哪几个渲染器支持。未列出的默认 3 端全支持。*/
export const MINI_CAPABILITY_MATRIX: Record<string, ReadonlyArray<RendererType>> = {
  // 不在 mini 端支持的组件
  CodeEditor: [],
  Chart: ['h5'],
  WaterfallList: ['mini-wx', 'mini-douyin', 'h5'],
  AiAvatarReply: ['h5'],
  ColorPicker: ['h5'],
  Drawer: ['mini-wx', 'h5'],
  Modal: ['mini-wx', 'mini-douyin', 'h5']
};

export function getMiniRenderers(type: string): ReadonlyArray<RendererType> {
  return MINI_CAPABILITY_MATRIX[type] ?? MINI_RENDERERS;
}

/** 把 web 端 ComponentMeta 转换为 mini 端版本（替换 runtimeRenderer + 过滤不支持组件）。*/
export const ALL_MINI_METAS: ReadonlyArray<ComponentMeta> = ALL_METAS
  .map((m) => ({ ...m, runtimeRenderer: getMiniRenderers(m.type) }))
  .filter((m) => m.runtimeRenderer.length > 0);

/** 全量注册到 component-registry。*/
export function registerAllMiniComponents(): void {
  const descriptor: ComponentImplementationDescriptor = {
    importedGlobals: [],
    importedPackages: ['@tarojs/taro', '@tarojs/components', '@atlas/lowcode-components-web']
  };
  for (const meta of ALL_MINI_METAS) {
    registerComponent(meta, { implementationDescriptor: descriptor });
  }
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-components-mini' as const;
