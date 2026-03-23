/**
 * 【高级 VII-7.1 自定义组件 SDK 方式】
 * 通过 custom 类型注册带 onMount/onUpdate/onUnmount 钩子的自定义组件
 */
import type { AmisSchema, CustomSdkComponentDef } from "@/types/amis";

/**
 * 注册 SDK 方式自定义组件到 AMIS
 *
 * @description
 * AMIS 支持通过 `custom` 类型注册自定义渲染组件，组件通过 DOM 操作方式实现。
 * 适用于需要使用 Vue/jQuery 等框架渲染的场景。
 *
 * @example
 * ```ts
 * registerCustomSdkComponent({
 *   type: 'atlas-progress-ring',
 *   label: '进度环',
 *   tags: ['business'],
 *   onMount: (dom, data) => {
 *     dom.innerHTML = `<div class="ring">${data.value}%</div>`;
 *   },
 *   onUpdate: (dom, data) => {
 *     dom.querySelector('.ring')!.textContent = `${data.value}%`;
 *   },
 *   onUnmount: (dom) => {
 *     dom.innerHTML = '';
 *   },
 * });
 * ```
 */
export function registerCustomSdkComponent(def: CustomSdkComponentDef): AmisSchema {
  return {
    type: "custom",
    name: def.type,
    label: def.label,
    ...(def.icon ? { icon: def.icon } : {}),
    onMount: (dom: HTMLElement, value: unknown, onChange: (value: unknown) => void, data: Record<string, unknown>) => {
      def.onMount(dom, data, onChange);
    },
    onUpdate: def.onUpdate
      ? (dom: HTMLElement, value: unknown, onChange: (value: unknown) => void, data: Record<string, unknown>) => {
          def.onUpdate!(dom, data);
        }
      : undefined,
    onUnmount: def.onUnmount
      ? (dom: HTMLElement) => {
          def.onUnmount!(dom);
        }
      : undefined,
  };
}

/**
 * 创建用于 amis-editor 编辑器面板的自定义组件注册
 */
export function createSdkEditorPlugin(def: CustomSdkComponentDef & {
  previewHtml?: string;
}): Record<string, unknown> {
  return {
    rendererName: def.type,
    name: def.label,
    tags: def.tags ?? ["自定义"],
    icon: def.icon ?? "fa fa-puzzle-piece",
    scaffold: {
      type: "custom",
      name: def.type,
      label: def.label,
    },
    previewSchema: {
      type: "tpl",
      tpl: def.previewHtml ?? `<div style="padding:16px;background:#f5f5f5;border-radius:4px;text-align:center">${def.label}</div>`,
    },
  };
}

const registeredSdkEditorPluginTypes = new Set<string>();

/**
 * 将 SDK custom 组件注册到 amis-editor 左侧组件面板（需已安装 amis-editor）
 */
export async function registerSdkEditorPlugin(
  def: CustomSdkComponentDef & { previewHtml?: string },
): Promise<void> {
  if (registeredSdkEditorPluginTypes.has(def.type)) {
    return;
  }
  try {
    const amisEditorModule = "amis-editor";
    const { registerEditorPlugin, BasePlugin } = await import(amisEditorModule) as {
      registerEditorPlugin: (cls: unknown) => void;
      BasePlugin: new () => Record<string, unknown>;
    };

    const meta = createSdkEditorPlugin(def);

    class SdkPlugin extends BasePlugin {
      rendererName = meta.rendererName as string;
      name = meta.name as string;
      description = "";
      tags = meta.tags as string[];
      icon = meta.icon as string;
      scaffold = meta.scaffold as Record<string, unknown>;
      previewSchema = meta.previewSchema as Record<string, unknown>;
    }

    registerEditorPlugin(SdkPlugin);
    registeredSdkEditorPluginTypes.add(def.type);
  } catch (error) {
    console.warn(`[Atlas LowCodeUI] amis-editor not available, skipping SDK plugin "${def.type}":`, error);
  }
}

/**
 * 批量注册多个 SDK 自定义组件
 */
export function registerCustomSdkComponents(defs: CustomSdkComponentDef[]): AmisSchema[] {
  return defs.map(registerCustomSdkComponent);
}
