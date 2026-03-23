/**
 * Vue 插件入口 — 按需注册组件（避免根包默认全量注册）
 */
import type { App, Component, Plugin } from "vue";

import AmisRenderer from "../components/AmisRenderer/index.vue";
import AmisDesigner from "../components/AmisDesigner/index.vue";
import PagePreview from "../components/PagePreview/index.vue";
import SchemaJsonEditor from "../components/SchemaJsonEditor/index.vue";
import ThemeProvider from "../components/ThemeProvider/index.vue";

export { registerReactEditorPlugin, registerReactComponents } from "../plugins/register-react-component";
export {
  registerCustomSdkComponent,
  createSdkEditorPlugin,
  registerCustomSdkComponents,
  registerSdkEditorPlugin,
} from "../plugins/register-custom-sdk";
export { registerDesignerCustomPluginsOnce } from "../plugins/register-designer-custom-plugins";
export type { RegisterDesignerCustomPluginsOptions } from "../plugins/register-designer-custom-plugins";

export type AtlasLowCodePluginComponents = {
  AmisRenderer?: Component;
  AmisDesigner?: Component;
  PagePreview?: Component;
  SchemaJsonEditor?: Component;
  ThemeProvider?: Component;
};

const defaultComponents: Required<AtlasLowCodePluginComponents> = {
  AmisRenderer,
  AmisDesigner,
  PagePreview,
  SchemaJsonEditor,
  ThemeProvider,
};

/**
 * 按需注册组件。未传入的组件使用库内默认实现。
 */
export function createAtlasLowCodePlugin(options?: { components?: AtlasLowCodePluginComponents }): Plugin {
  const merged: Required<AtlasLowCodePluginComponents> = {
    ...defaultComponents,
    ...options?.components,
  };

  return {
    install(app: App) {
      app.component("AmisRenderer", merged.AmisRenderer);
      app.component("AmisDesigner", merged.AmisDesigner);
      app.component("PagePreview", merged.PagePreview);
      app.component("SchemaJsonEditor", merged.SchemaJsonEditor);
      app.component("ThemeProvider", merged.ThemeProvider);
    },
  };
}

/**
 * 注册全部内置组件（与旧版 `app.use(AtlasLowCodeUI)` 行为一致，建议改用 createAtlasLowCodePlugin）。
 */
export function createFullAtlasLowCodePlugin(): Plugin {
  if (typeof console !== "undefined" && typeof console.warn === "function") {
    console.warn(
      "[@atlas/lowcode-ui] createFullAtlasLowCodePlugin 将全量注册组件；为降低首屏成本，请优先使用 createAtlasLowCodePlugin 或从 @atlas/lowcode-ui/renderer、@atlas/lowcode-ui/designer 按需引入。",
    );
  }
  return createAtlasLowCodePlugin();
}

export default createFullAtlasLowCodePlugin;
