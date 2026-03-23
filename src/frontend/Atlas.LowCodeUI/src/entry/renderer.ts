/**
 * 渲染器入口 — 仅导出 Amis 渲染相关组件与 composables（不含设计器 / Monaco）
 */
import "../styles/amis-theme.css";

export type {
  AmisSchema,
  AmisEnv,
  AmisFetcherConfig,
  AmisFetcherResult,
  AmisNotifyType,
  AmisActionContext,
  AmisRendererProps,
  AmisRendererEmits,
  PagePreviewProps,
  AmisEnvOptions,
} from "../types/amis";

export { default as AmisRenderer } from "../components/AmisRenderer/index.vue";
export { default as PagePreview } from "../components/PagePreview/index.vue";
export { default as ThemeProvider } from "../components/ThemeProvider/index.vue";

export { useAmisEnv } from "../composables/useAmisEnv";
export { useAmisLocale, normalizeAmisLocale, type AmisLocaleValue } from "../composables/useAmisLocale";
export { useSchemaHistory, type SchemaHistoryReturn } from "../composables/useSchemaHistory";

export { registerReactComponent, createReactEditorPlugin } from "../plugins/register-react-component";
