/**
 * AMIS 核心类型定义
 * 独立于 Atlas.WebApp，可被任何宿主项目使用
 */

/** AMIS JSON Schema 类型 */
export type AmisSchema = Record<string, unknown>;

/** AMIS fetcher 请求配置 */
export interface AmisFetcherConfig {
  url: string;
  method?: string;
  data?: unknown;
  headers?: Record<string, string>;
  config?: Record<string, unknown>;
}

/** AMIS fetcher 响应结果 */
export interface AmisFetcherResult {
  data: unknown;
  ok: boolean;
  status: number;
  msg?: string;
}

/** AMIS 通知类型 */
export type AmisNotifyType = "info" | "success" | "warning" | "error";

/** AMIS 环境配置（传递给 amis.render 的 env 对象） */
export interface AmisEnv {
  /** 数据请求器 */
  fetcher: (config: AmisFetcherConfig) => Promise<AmisFetcherResult>;
  /** 消息通知 */
  notify: (type: AmisNotifyType, msg: string) => void;
  /** 弹窗提示 */
  alert: (msg: string) => void;
  /** 确认对话框 */
  confirm: (msg: string) => Promise<boolean>;
  /** 路由跳转 */
  updateLocation?: (location: string, replace?: boolean) => void;
  /** 文本复制 */
  copy?: (content: string) => void;
  /** 当前 locale */
  locale?: string;
  /** 主题名称 */
  theme?: string;
  /** 全局数据 */
  data?: Record<string, unknown>;
}

/** AMIS 动作事件上下文 */
export interface AmisActionContext {
  actionType: string;
  api?: string | Record<string, unknown>;
  dialog?: AmisSchema;
  drawer?: AmisSchema;
  link?: string;
  url?: string;
  blank?: boolean;
  reload?: string;
  target?: string;
  args?: Record<string, unknown>;
}

/** AmisRenderer 组件 Props */
export interface AmisRendererProps {
  /** AMIS JSON Schema */
  schema: AmisSchema;
  /** 注入数据 */
  data?: Record<string, unknown>;
  /** AMIS 主题（默认 'cxd'） */
  theme?: string;
  /** 国际化 locale（默认 'zh-CN'） */
  locale?: string;
  /** 自定义环境配置（覆盖 useAmisEnv 生成的默认值） */
  env?: Partial<AmisEnv>;
  /** 是否延迟加载 amis（默认 false） */
  lazyLoad?: boolean;
  /** 是否启用调试模式 */
  debug?: boolean;
}

/** AmisRenderer 组件 Emits */
export interface AmisRendererEmits {
  (e: "action", context: AmisActionContext): void;
}

/** AmisDesigner 组件 Props */
export interface AmisDesignerProps {
  /** 画布 Schema（v-model 双向绑定） */
  modelValue: AmisSchema;
  /** 是否预览模式 */
  preview?: boolean;
  /** 是否移动端模式 */
  isMobile?: boolean;
  /** 主题 */
  theme?: string;
  /** 画布高度 */
  height?: string;
}

/** AmisDesigner 组件 Emits */
export interface AmisDesignerEmits {
  (e: "update:modelValue", schema: AmisSchema): void;
  (e: "save", schema: AmisSchema): void;
}

/** PagePreview 组件 Props */
export interface PagePreviewProps {
  /** 要预览的 Schema */
  schema: AmisSchema;
  /** 注入数据 */
  data?: Record<string, unknown>;
  /** 是否可见 */
  visible?: boolean;
}

/** SchemaJsonEditor 组件 Props */
export interface SchemaJsonEditorProps {
  /** 编辑的 Schema（v-model） */
  modelValue: AmisSchema;
  /** 编辑器高度 */
  height?: string;
  /** 是否只读 */
  readonly?: boolean;
}

/** 自定义组件注册配置（SDK 方式） */
export interface CustomSdkComponentDef {
  /** 组件类型名称 */
  type: string;
  /** 显示名称 */
  label: string;
  /** 分组标签 */
  tags?: string[];
  /** 图标 */
  icon?: string;
  /** 组件挂载回调 */
  onMount: (dom: HTMLElement, data: Record<string, unknown>, onChange: (value: unknown) => void) => void;
  /** 组件更新回调 */
  onUpdate?: (dom: HTMLElement, data: Record<string, unknown>) => void;
  /** 组件卸载回调 */
  onUnmount?: (dom: HTMLElement) => void;
}

/** 自定义组件注册配置（React 方式） */
export interface CustomReactComponentDef {
  /** 组件类型名称 */
  type: string;
  /** 显示名称 */
  label: string;
  /** 分组标签 */
  tags?: string[];
  /** 图标 */
  icon?: string;
  /** React 组件 */
  component: React.ComponentType<Record<string, unknown>>;
  /** 编辑器预览 Schema */
  previewSchema?: AmisSchema;
  /** 编辑器脚手架 Schema */
  scaffold?: AmisSchema;
}

/** useAmisEnv 配置选项 */
export interface AmisEnvOptions {
  /** 自定义 fetcher（数据请求函数） */
  fetcher?: (config: AmisFetcherConfig) => Promise<AmisFetcherResult>;
  /** 自定义通知函数 */
  notify?: (type: AmisNotifyType, msg: string) => void;
  /** 自定义弹窗函数 */
  alert?: (msg: string) => void;
  /** 自定义确认函数 */
  confirm?: (msg: string) => Promise<boolean>;
  /** 自定义路由跳转 */
  updateLocation?: (location: string, replace?: boolean) => void;
  /** 自定义复制函数 */
  copy?: (content: string) => void;
  /** locale */
  locale?: string;
  /** 主题 */
  theme?: string;
  /** 全局注入数据 */
  data?: Record<string, unknown>;
}

/** Schema 历史记录条目 */
export interface SchemaHistoryEntry {
  schema: AmisSchema;
  timestamp: number;
}

/** useSchemaHistory 配置 */
export interface SchemaHistoryOptions {
  /** 最大历史栈深度（默认 50） */
  maxDepth?: number;
}
