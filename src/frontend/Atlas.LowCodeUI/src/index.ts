/**
 * Atlas LowCode UI — 统一导出
 * @packageDocumentation
 */
import type { App, Plugin } from "vue";

// ========== 样式 ==========
import "./styles/amis-theme.css";
import "./styles/designer.css";

// ========== 类型导出 ==========
export type {
  AmisSchema,
  AmisEnv,
  AmisFetcherConfig,
  AmisFetcherResult,
  AmisNotifyType,
  AmisActionContext,
  AmisRendererProps,
  AmisRendererEmits,
  AmisDesignerProps,
  AmisDesignerEmits,
  PagePreviewProps,
  SchemaJsonEditorProps,
  AmisEnvOptions,
  CustomSdkComponentDef,
  CustomReactComponentDef,
  SchemaHistoryEntry,
  SchemaHistoryOptions,
} from "./types/amis";

// ========== 组件导出 ==========
export { default as AmisRenderer } from "./components/AmisRenderer/index.vue";
export { default as AmisDesigner } from "./components/AmisDesigner/index.vue";
export { default as PagePreview } from "./components/PagePreview/index.vue";
export { default as SchemaJsonEditor } from "./components/SchemaJsonEditor/index.vue";
export { default as ThemeProvider } from "./components/ThemeProvider/index.vue";

// ========== Composables 导出 ==========
export { useAmisEnv } from "./composables/useAmisEnv";
export { useAmisLocale, normalizeAmisLocale, type AmisLocaleValue } from "./composables/useAmisLocale";
export { useSchemaHistory, type SchemaHistoryReturn } from "./composables/useSchemaHistory";

// ========== Plugins 导出 ==========
export { registerCustomSdkComponent, createSdkEditorPlugin, registerCustomSdkComponents } from "./plugins/register-custom-sdk";
export { registerReactComponent, createReactEditorPlugin, registerReactEditorPlugin, registerReactComponents } from "./plugins/register-react-component";

// ========== Utils 导出 ==========
export * from "./utils/permission-expr";
export * from "./utils/perf-config";

// ========== Schema Builders — 表单 ==========
export * from "./schemas/form/base-controls";
export * from "./schemas/form/select-controls";
export * from "./schemas/form/date-upload-controls";
export * from "./schemas/form/layout-modes";
export * from "./schemas/form/validation";

// ========== Schema Builders — 数据展示 ==========
export * from "./schemas/data/crud";
export * from "./schemas/data/table-list-cards";
export * from "./schemas/data/chart";
export * from "./schemas/data/stat-misc";

// ========== Schema Builders — 布局 ==========
export * from "./schemas/layout/page";
export * from "./schemas/layout/grid-flex";
export * from "./schemas/layout/panel-tabs-collapse";
export * from "./schemas/layout/wizard";
export * from "./schemas/layout/dialog-drawer";

// ========== Schema Builders — 动作 ==========
export * from "./schemas/action/ajax-url";
export * from "./schemas/action/dialog-drawer-reload";
export * from "./schemas/action/broadcast";
export * from "./schemas/action/condition";

// ========== Schema Builders — 数据源 ==========
export * from "./schemas/datasource/api-config";
export * from "./schemas/datasource/data-chain";
export * from "./schemas/datasource/mapping-filter";
export * from "./schemas/datasource/adapter";

// ========== Vue Plugin ==========
import AmisRenderer from "./components/AmisRenderer/index.vue";
import AmisDesigner from "./components/AmisDesigner/index.vue";
import PagePreview from "./components/PagePreview/index.vue";
import SchemaJsonEditor from "./components/SchemaJsonEditor/index.vue";
import ThemeProvider from "./components/ThemeProvider/index.vue";

const AtlasLowCodeUI: Plugin = {
  install(app: App) {
    app.component("AmisRenderer", AmisRenderer);
    app.component("AmisDesigner", AmisDesigner);
    app.component("PagePreview", PagePreview);
    app.component("SchemaJsonEditor", SchemaJsonEditor);
    app.component("ThemeProvider", ThemeProvider);
  },
};

export default AtlasLowCodeUI;
