/**
 * @atlas/lowcode-schema/types — 17 类完整类型聚合导出（M01）。
 *
 * 17 类清单（PLAN.md §M01 C01-1）：
 *   1) AppSchema
 *   2) PageSchema
 *   3) ComponentSchema
 *   4) BindingSchema（含 5 子类型 union）
 *   5) ContentParamSchema（独立于 BindingSchema，6 类）
 *   6) EventSchema
 *   7) ActionSchema（含 7 子类型 union）
 *   8) VariableSchema
 *   9) SlotSchema
 *  10) LifecycleSchema
 *  11) PropertyPanelSchema
 *  12) ComponentMeta
 *  13) ResourceRef
 *  14) PublishedArtifact
 *  15) VersionArchive
 *  16) RuntimeStatePatch
 *  17) RuntimeTrace
 */

export type { AppSchema, AppTheme } from './app';
export type { PageSchema } from './page';
export type {
  ComponentSchema,
  ComponentMeta,
  ChildPolicy,
  SlotSchema,
  PropertyPanelSchema,
  PropertyPanelField
} from './component';
export type {
  BindingSchema,
  BindingBase,
  StaticBinding,
  VariableBinding,
  ExpressionBinding,
  WorkflowOutputBinding,
  ChatflowOutputBinding
} from './binding';
export { BINDING_SOURCE_TYPES } from './binding';
export type {
  ContentParamSchema,
  ContentParamBase,
  TextContentParam,
  ImageContentParam,
  DataContentParam,
  LinkContentParam,
  MediaContentParam,
  AiContentParam
} from './content-param';
export type { EventSchema } from './event';
export type {
  ActionSchema,
  ActionBase,
  ResiliencePolicy,
  SetVariableAction,
  CallWorkflowAction,
  CallChatflowAction,
  NavigateAction,
  OpenExternalLinkAction,
  ShowToastAction,
  UpdateComponentAction
} from './action';
export type { VariableSchema } from './variable';
export type { LifecycleSchema } from './lifecycle';
export type { ResourceRef } from './resource';
export type { PublishedArtifact } from './published-artifact';
export type { VersionArchive, ResourceSnapshot, ResourceVersionRef } from './version-archive';
export type { RuntimeStatePatch, RuntimeTrace, RuntimeSpan } from './runtime';
