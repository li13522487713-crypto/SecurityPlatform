/**
 * 【高级 VII-7.1 自定义组件 React 方式】
 * 将 React 组件注册进 AMIS 组件面板
 */
import type { AmisSchema, CustomReactComponentDef } from "@/types/amis";

/**
 * 注册 React 组件到 AMIS 渲染器
 *
 * @description
 * 通过 amis-core 的 Renderer 装饰器将 React 组件注册到 AMIS。
 * 注册后可在 Schema 中通过 type 引用。
 *
 * @example
 * ```ts
 * import { MyChart } from './MyChart';
 *
 * registerReactComponent({
 *   type: 'atlas-chart-widget',
 *   label: '自定义图表',
 *   tags: ['business', 'chart'],
 *   component: MyChart,
 *   scaffold: { type: 'atlas-chart-widget', title: '图表标题' },
 * });
 * ```
 */
export async function registerReactComponent(def: CustomReactComponentDef): Promise<void> {
  try {
    const amisCore = await import("amis-core");
    const { Renderer } = amisCore;

    Renderer({
      type: def.type,
      name: def.type,
      component: def.component as unknown,
    });
  } catch (error) {
    console.warn(`[Atlas LowCodeUI] Failed to register React component "${def.type}":`, error);
  }
}

/**
 * 为 React 组件创建编辑器插件配置
 */
export function createReactEditorPlugin(def: CustomReactComponentDef): Record<string, unknown> {
  return {
    rendererName: def.type,
    name: def.label,
    tags: def.tags ?? ["自定义"],
    icon: def.icon ?? "fa fa-puzzle-piece",
    scaffold: def.scaffold ?? { type: def.type },
    previewSchema: def.previewSchema ?? { type: def.type },
  };
}

/**
 * 注册 React 组件到 amis-editor 编辑器面板
 */
export async function registerReactEditorPlugin(def: CustomReactComponentDef): Promise<void> {
  try {
    const amisEditorModule = "amis-editor";
    const { registerEditorPlugin, BasePlugin } = await import(/* @vite-ignore */ amisEditorModule) as {
      registerEditorPlugin: (cls: unknown) => void;
      BasePlugin: new () => Record<string, unknown>;
    };

    class CustomPlugin extends BasePlugin {
      rendererName = def.type;
      name = def.label;
      description = "";
      tags = def.tags ?? ["自定义"];
      icon = def.icon ?? "fa fa-puzzle-piece";
      scaffold = def.scaffold ?? { type: def.type };
      previewSchema = def.previewSchema ?? { type: def.type };
    }

    registerEditorPlugin(CustomPlugin);
  } catch (error) {
    console.warn(`[Atlas LowCodeUI] amis-editor not available, skipping plugin registration for "${def.type}":`, error);
  }
}

/**
 * 批量注册 React 组件（渲染器 + 编辑器）
 */
export async function registerReactComponents(defs: CustomReactComponentDef[]): Promise<void> {
  for (const def of defs) {
    await registerReactComponent(def);
    await registerReactEditorPlugin(def);
  }
}
