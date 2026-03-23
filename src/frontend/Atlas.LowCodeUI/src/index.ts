/**
 * Atlas LowCode UI — 轻量根入口（类型、Schema builders、工具；不含 Vue 组件默认注册）
 * @packageDocumentation
 */
// ========== 样式（全量主题 + 设计器布局，供 @atlas/lowcode-ui/style.css 单文件消费） ==========
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

// ========== Plugins（不含 amis-editor 运行时强依赖的 API） ==========
export {
  registerCustomSdkComponent,
  createSdkEditorPlugin,
  registerCustomSdkComponents,
  registerSdkEditorPlugin,
} from "./plugins/register-custom-sdk";
export { registerReactComponent, createReactEditorPlugin } from "./plugins/register-react-component";
export { registerDesignerCustomPluginsOnce } from "./plugins/register-designer-custom-plugins";
export type { RegisterDesignerCustomPluginsOptions } from "./plugins/register-designer-custom-plugins";

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
