/**
 * @atlas/lowcode-schema/shared — 通用枚举与常量（M01）。
 *
 * 这些枚举被 lowcode-schema 自身、各 Adapter、Studio、Runtime、collab 等多包共用。
 * 任何低代码包对枚举值的引用必须通过本子模块导入，禁止散落字面量。
 *
 * 严格遵守 PLAN.md §1.3 的 4 条强约束（API 双前缀 / dispatch 唯一桥梁 / 作用域隔离 / 元数据驱动）。
 */

/** Schema 主版本（向后兼容由 migrate 模块负责）。*/
export const SCHEMA_VERSION = 'v1' as const;
export type SchemaVersion = typeof SCHEMA_VERSION;

/** 渲染器类型（与后端 RuntimeSchemaController 的 ?renderer= 参数一致）。*/
export const RENDERER_TYPES = ['web', 'mini-wx', 'mini-douyin', 'h5'] as const;
export type RendererType = (typeof RENDERER_TYPES)[number];

/** 多端类型（PageSchema / AppSchema 共用）。*/
export const TARGET_TYPES = ['web', 'mini_program', 'hybrid'] as const;
export type TargetType = (typeof TARGET_TYPES)[number];

/** 页面布局策略（docx §10.2.2 PageSchema.layout）。*/
export const PAGE_LAYOUTS = ['free', 'flow', 'responsive'] as const;
export type PageLayout = (typeof PAGE_LAYOUTS)[number];

/** 9 种值类型（VariableSchema.valueType + binding.valueType 共用）。*/
export const VALUE_TYPES = [
  'string',
  'number',
  'boolean',
  'date',
  'array',
  'object',
  'file',
  'image',
  'any'
] as const;
export type ValueType = (typeof VALUE_TYPES)[number];

/**
 * 7 种作用域根（M02 表达式引擎 + M03 action-runtime 双层校验时使用）。
 *
 * 写访问：仅 page / app 可写（system 是只读）。
 * 只读访问：component / event / workflow.outputs / chatflow.outputs 永远只读。
 */
export const SCOPE_ROOTS = [
  'page',
  'app',
  'system',
  'component',
  'event',
  'workflow.outputs',
  'chatflow.outputs'
] as const;
export type ScopeRoot = (typeof SCOPE_ROOTS)[number];

/** 仅可写的作用域子集（M02/M03 校验用）。*/
export const WRITABLE_SCOPES = ['page', 'app'] as const;
export type WritableScope = (typeof WRITABLE_SCOPES)[number];

/** 5 种值源（PLAN.md §M05 C05-4）。*/
export const VALUE_SOURCE_TYPES = [
  'static',
  'variable',
  'expression',
  'workflow_output',
  'chatflow_output'
] as const;
export type ValueSourceType = (typeof VALUE_SOURCE_TYPES)[number];

/** 7 种内置动作（PLAN.md §M03 C03-1）。*/
export const ACTION_KINDS = [
  'set_variable',
  'call_workflow',
  'call_chatflow',
  'navigate',
  'open_external_link',
  'show_toast',
  'update_component'
] as const;
export type ActionKind = (typeof ACTION_KINDS)[number];

/** 6 类内容参数（docx §U11，PLAN.md §M05/M06）。*/
export const CONTENT_PARAM_KINDS = ['text', 'image', 'data', 'link', 'media', 'ai'] as const;
export type ContentParamKind = (typeof CONTENT_PARAM_KINDS)[number];

/** 应用状态。*/
export const APP_STATUSES = ['draft', 'published', 'archived'] as const;
export type AppStatus = (typeof APP_STATUSES)[number];

/** 8+ 种事件类型（PLAN.md §M08 C08-5）。*/
export const EVENT_NAMES = [
  'onClick',
  'onChange',
  'onSubmit',
  'onUploadSuccess',
  'onUploadError',
  'onPageLoad',
  'onItemClick',
  'onLoad',
  'onError',
  'onScrollEnd'
] as const;
export type EventName = (typeof EVENT_NAMES)[number];

/** 渠道类型（M18 智能体多渠道发布）。*/
export const CHANNEL_TYPES = ['feishu', 'wechat', 'douyin', 'doubao'] as const;
export type ChannelType = (typeof CHANNEL_TYPES)[number];
