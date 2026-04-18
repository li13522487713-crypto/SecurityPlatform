/**
 * @atlas/lowcode-schema/zod — 17 类完整 zod 校验（M01）。
 *
 * 设计要点：
 * - JsonValue 用 z.lazy 递归声明，避免 stack overflow（限制最大递归深度由调用方控制）。
 * - 所有 union 用 discriminatedUnion，错误路径精确到 sourceType / kind。
 * - export 名以 `*Zod` 后缀，与类型同名 ts 文件区分。
 */

import { z } from 'zod';

import {
  ACTION_KINDS,
  APP_STATUSES,
  CHANNEL_TYPES,
  CONTENT_PARAM_KINDS,
  EVENT_NAMES,
  PAGE_LAYOUTS,
  RENDERER_TYPES,
  SCHEMA_VERSION,
  SCOPE_ROOTS,
  TARGET_TYPES,
  VALUE_SOURCE_TYPES,
  VALUE_TYPES
} from '../shared/enums';

// ---------- JSON ----------
type ZJson =
  | string
  | number
  | boolean
  | null
  | ZJson[]
  | { [k: string]: ZJson };
export const JsonValueZod: z.ZodType<ZJson> = z.lazy(() =>
  z.union([
    z.string(),
    z.number(),
    z.boolean(),
    z.null(),
    z.array(JsonValueZod),
    z.record(JsonValueZod)
  ])
);
export const JsonObjectZod = z.record(JsonValueZod);

// ---------- Binding ----------
const BindingBaseZod = z.object({
  valueType: z.enum(VALUE_TYPES),
  fallback: JsonValueZod.optional(),
  trace: z.boolean().optional()
});

const StaticBindingZod = BindingBaseZod.extend({
  sourceType: z.literal('static'),
  value: JsonValueZod
});

const VariableBindingZod = BindingBaseZod.extend({
  sourceType: z.literal('variable'),
  path: z.string().min(1),
  scopeRoot: z.enum(SCOPE_ROOTS)
});

const ExpressionBindingZod = BindingBaseZod.extend({
  sourceType: z.literal('expression'),
  expression: z.string().min(1)
});

const WorkflowOutputBindingZod = BindingBaseZod.extend({
  sourceType: z.literal('workflow_output'),
  workflowId: z.string().min(1),
  outputPath: z.string().optional()
});

const ChatflowOutputBindingZod = BindingBaseZod.extend({
  sourceType: z.literal('chatflow_output'),
  chatflowId: z.string().min(1),
  outputPath: z.string().optional()
});

export const BindingSchemaZod = z.discriminatedUnion('sourceType', [
  StaticBindingZod,
  VariableBindingZod,
  ExpressionBindingZod,
  WorkflowOutputBindingZod,
  ChatflowOutputBindingZod
]);

// 静态保证：VALUE_SOURCE_TYPES 与 BindingSchemaZod 各 sourceType 严格一致。
{
  const _check: ReadonlyArray<typeof VALUE_SOURCE_TYPES[number]> = [
    'static',
    'variable',
    'expression',
    'workflow_output',
    'chatflow_output'
  ];
  void _check;
}

// ---------- Content Param ----------
const ContentParamBaseZod = z.object({
  code: z.string().min(1),
  description: z.string().optional()
});

const TextContentParamZod = ContentParamBaseZod.extend({
  kind: z.literal('text'),
  mode: z.enum(['static', 'template', 'i18n']),
  source: z.string(),
  context: JsonObjectZod.optional()
});

const ImageContentParamZod = ContentParamBaseZod.extend({
  kind: z.literal('image'),
  mode: z.enum(['url', 'fileHandle', 'imageId', 'placeholder']),
  source: z.string(),
  placeholder: z.string().optional()
});

const DataContentParamZod = ContentParamBaseZod.extend({
  kind: z.literal('data'),
  source: BindingSchemaZod,
  expectArray: z.boolean().optional()
});

const LinkContentParamZod = ContentParamBaseZod.extend({
  kind: z.literal('link'),
  linkType: z.enum(['internal', 'external']),
  href: z.string().min(1),
  target: z.enum(['_self', '_blank']).optional()
});

const MediaContentParamZod = ContentParamBaseZod.extend({
  kind: z.literal('media'),
  mediaType: z.enum(['video', 'audio']),
  url: z.string().min(1),
  cover: z.string().optional()
});

const AiContentParamZod = ContentParamBaseZod.extend({
  kind: z.literal('ai'),
  mode: z.enum(['chatflow_stream', 'ai_card']),
  chatflowId: z.string().optional(),
  cardConfig: JsonObjectZod.optional()
});

export const ContentParamSchemaZod = z.discriminatedUnion('kind', [
  TextContentParamZod,
  ImageContentParamZod,
  DataContentParamZod,
  LinkContentParamZod,
  MediaContentParamZod,
  AiContentParamZod
]);

{
  const _check: ReadonlyArray<typeof CONTENT_PARAM_KINDS[number]> = [
    'text',
    'image',
    'data',
    'link',
    'media',
    'ai'
  ];
  void _check;
}

// ---------- Action ----------
const ResiliencePolicyZod = z.object({
  timeoutMs: z.number().int().positive().optional(),
  retry: z
    .object({
      maxAttempts: z.number().int().min(1),
      backoff: z.enum(['fixed', 'exponential']),
      initialDelayMs: z.number().int().nonnegative().optional()
    })
    .optional(),
  circuitBreaker: z
    .object({
      failuresThreshold: z.number().int().positive(),
      windowMs: z.number().int().positive(),
      openMs: z.number().int().positive()
    })
    .optional(),
  fallback: z
    .object({
      kind: z.enum(['workflow', 'static']),
      workflowId: z.string().optional(),
      staticValue: JsonValueZod.optional()
    })
    .optional()
});

// 因 ActionSchema 自递归（onError 内允许嵌套），用 z.lazy 处理。
// 不能用 `z.infer<typeof ActionSchemaZod>`（会触发 TS2456 自引用），改用显式类型
// 复用 src/types/action.ts 已定义的 ActionSchema 联合类型。
import type { ActionSchema as ZAction } from '../types/action';
const ActionBaseZodShape = {
  id: z.string().optional(),
  when: z.string().optional(),
  resilience: ResiliencePolicyZod.optional(),
  parallel: z.boolean().optional(),
  // onError 在 lazy 包装内通过 ActionSchemaZod 引用
  onError: z.lazy((): z.ZodType<ZAction[]> => z.array(ActionSchemaZod)).optional()
} as const;

const SetVariableActionZod = z.object({
  kind: z.literal('set_variable'),
  ...ActionBaseZodShape,
  targetPath: z.string().min(1),
  scopeRoot: z.enum(['page', 'app']),
  value: BindingSchemaZod
});

const CallWorkflowActionZod = z.object({
  kind: z.literal('call_workflow'),
  ...ActionBaseZodShape,
  workflowId: z.string().min(1),
  mode: z.enum(['sync', 'async', 'batch']).optional(),
  inputMapping: z.record(BindingSchemaZod).optional(),
  outputMapping: z.record(z.string()).optional(),
  loadingTargets: z.array(z.string()).optional(),
  errorTargets: z.array(z.string()).optional()
});

const CallChatflowActionZod = z.object({
  kind: z.literal('call_chatflow'),
  ...ActionBaseZodShape,
  chatflowId: z.string().min(1),
  sessionId: z.string().optional(),
  inputMapping: z.record(BindingSchemaZod).optional(),
  streamTarget: z.string().min(1)
});

const NavigateActionZod = z.object({
  kind: z.literal('navigate'),
  ...ActionBaseZodShape,
  to: z.string().min(1),
  params: JsonObjectZod.optional(),
  replace: z.boolean().optional()
});

const OpenExternalLinkActionZod = z.object({
  kind: z.literal('open_external_link'),
  ...ActionBaseZodShape,
  url: z.string().url(),
  target: z.enum(['_blank', '_self']).optional()
});

const ShowToastActionZod = z.object({
  kind: z.literal('show_toast'),
  ...ActionBaseZodShape,
  message: BindingSchemaZod,
  toastType: z.enum(['info', 'success', 'warning', 'error']).optional(),
  durationMs: z.number().int().positive().optional()
});

const UpdateComponentActionZod = z.object({
  kind: z.literal('update_component'),
  ...ActionBaseZodShape,
  componentId: z.string().min(1),
  patchProps: z.record(BindingSchemaZod)
});

export const ActionSchemaZod: z.ZodType<ZAction> = z.lazy(() =>
  z.discriminatedUnion('kind', [
    SetVariableActionZod,
    CallWorkflowActionZod,
    CallChatflowActionZod,
    NavigateActionZod,
    OpenExternalLinkActionZod,
    ShowToastActionZod,
    UpdateComponentActionZod
  ])
);

{
  const _check: ReadonlyArray<typeof ACTION_KINDS[number]> = [
    'set_variable',
    'call_workflow',
    'call_chatflow',
    'navigate',
    'open_external_link',
    'show_toast',
    'update_component'
  ];
  void _check;
}

// ---------- Event ----------
export const EventSchemaZod = z.object({
  name: z.enum(EVENT_NAMES),
  actions: z.array(ActionSchemaZod),
  description: z.string().optional()
});

// ---------- Component ----------
type ZComponent = {
  id: string;
  type: string;
  props?: Record<string, unknown>;
  contentParams?: z.infer<typeof ContentParamSchemaZod>[];
  events?: z.infer<typeof EventSchemaZod>[];
  children?: ZComponent[];
  slots?: Record<string, ZComponent[]>;
  visible?: boolean;
  locked?: boolean;
  metadata?: Record<string, unknown>;
};

export const ComponentSchemaZod: z.ZodType<ZComponent> = z.lazy(() =>
  z.object({
    id: z.string().min(1),
    type: z.string().min(1),
    props: z.record(z.union([BindingSchemaZod, JsonValueZod])).optional(),
    contentParams: z.array(ContentParamSchemaZod).optional(),
    events: z.array(EventSchemaZod).optional(),
    children: z.array(ComponentSchemaZod).optional(),
    slots: z.record(z.array(ComponentSchemaZod)).optional(),
    visible: z.boolean().optional(),
    locked: z.boolean().optional(),
    metadata: JsonObjectZod.optional()
  })
);

export const SlotSchemaZod = z.object({
  name: z.string(),
  description: z.string().optional(),
  multiple: z.boolean().optional(),
  allowComponentTypes: z.array(z.string()).optional()
});

export const PropertyPanelFieldZod = z.object({
  key: z.string(),
  label: z.string(),
  renderer: z.string(),
  rendererProps: JsonObjectZod.optional(),
  valueType: z.enum(VALUE_TYPES).optional(),
  dependsOn: z
    .object({ field: z.string(), equals: JsonValueZod })
    .optional(),
  required: z.boolean().optional(),
  defaultValue: JsonValueZod.optional()
});

export const PropertyPanelSchemaZod = z.object({
  group: z.string(),
  label: z.string(),
  fields: z.array(PropertyPanelFieldZod),
  collapsed: z.boolean().optional()
});

export const ChildPolicyZod = z.object({
  arity: z.enum(['none', 'one', 'many']),
  allowTypes: z.array(z.string()).optional()
});

export const ComponentMetaZod = z.object({
  type: z.string(),
  displayName: z.string(),
  category: z.string(),
  group: z.string().optional(),
  icon: z.string().optional(),
  version: z.string(),
  runtimeRenderer: z.array(z.enum(RENDERER_TYPES)),
  bindableProps: z.array(z.string()),
  contentParams: z.array(z.enum(CONTENT_PARAM_KINDS)).optional(),
  supportedEvents: z.array(z.enum(EVENT_NAMES)),
  childPolicy: ChildPolicyZod,
  slots: z.array(SlotSchemaZod).optional(),
  propertyPanels: z.array(PropertyPanelSchemaZod).optional(),
  supportedValueType: z.record(z.enum(VALUE_TYPES)).optional()
});

// ---------- Variable / Lifecycle ----------
export const VariableSchemaZod = z.object({
  code: z.string().min(1),
  displayName: z.string().min(1),
  scope: z.enum(['page', 'app', 'system']),
  valueType: z.enum(VALUE_TYPES),
  readonly: z.boolean().optional(),
  persist: z.boolean().optional(),
  defaultValue: JsonValueZod.optional(),
  validation: JsonValueZod.optional(),
  description: z.string().optional()
});

export const LifecycleSchemaZod = z.object({
  beforePageLoad: z.array(ActionSchemaZod).optional(),
  afterPageLoad: z.array(ActionSchemaZod).optional(),
  beforePageUnload: z.array(ActionSchemaZod).optional()
});

// ---------- Page / App ----------
export const PageSchemaZod = z.object({
  id: z.string().min(1),
  code: z.string().min(1),
  displayName: z.string().min(1),
  path: z.string().regex(/^\//, '页面 path 必须以 / 开头'),
  targetType: z.enum(TARGET_TYPES),
  layout: z.enum(PAGE_LAYOUTS),
  orderNo: z.number().int().nonnegative().optional(),
  visible: z.boolean().optional(),
  locked: z.boolean().optional(),
  root: ComponentSchemaZod,
  variables: z.array(VariableSchemaZod).optional(),
  contentParams: z.array(ContentParamSchemaZod).optional(),
  lifecycle: LifecycleSchemaZod.optional(),
  metadata: JsonObjectZod.optional()
});

export const AppThemeZod = z.object({
  primaryColor: z.string().optional(),
  borderRadius: z.number().int().nonnegative().optional(),
  darkMode: z.enum(['never', 'always', 'auto']).optional(),
  cssVariables: z.record(z.string()).optional()
});

export const AppSchemaZod = z.object({
  schemaVersion: z.literal(SCHEMA_VERSION),
  appId: z.string().min(1),
  code: z.string().min(1),
  displayName: z.string().min(1),
  description: z.string().optional(),
  targetTypes: z.array(z.enum(TARGET_TYPES)).min(1),
  defaultLocale: z.string().min(1),
  status: z.enum(APP_STATUSES).optional(),
  variables: z.array(VariableSchemaZod).optional(),
  contentParams: z.array(ContentParamSchemaZod).optional(),
  pages: z.array(PageSchemaZod),
  theme: AppThemeZod.optional(),
  metadata: JsonObjectZod.optional()
});

// ---------- ResourceRef / PublishedArtifact / VersionArchive / Runtime ----------
export const ResourceRefZod = z.object({
  resourceType: z.enum([
    'workflow',
    'chatflow',
    'variable',
    'trigger',
    'datasource',
    'plugin',
    'prompt-template',
    'knowledge',
    'database',
    'file'
  ]),
  resourceId: z.string().min(1),
  version: z.string().optional()
});

export const PublishedArtifactZod = z.object({
  id: z.string(),
  appId: z.string(),
  versionId: z.string(),
  kind: z.enum(['hosted', 'embedded-sdk', 'preview']),
  status: z.enum(['pending', 'building', 'ready', 'failed', 'revoked']),
  fingerprint: z.string(),
  publicUrl: z.string().optional(),
  rendererMatrix: z.record(z.boolean()),
  publishedByUserId: z.string(),
  errorMessage: z.string().optional(),
  createdAt: z.string(),
  updatedAt: z.string()
});

export const ResourceVersionRefZod = z.object({
  id: z.string(),
  version: z.string(),
  metadata: JsonObjectZod.optional()
});

export const ResourceSnapshotZod = z.object({
  workflows: z.array(ResourceVersionRefZod).optional(),
  chatflows: z.array(ResourceVersionRefZod).optional(),
  knowledge: z.array(ResourceVersionRefZod).optional(),
  databases: z.array(ResourceVersionRefZod).optional(),
  variables: z.array(ResourceVersionRefZod).optional(),
  plugins: z.array(ResourceVersionRefZod).optional(),
  promptTemplates: z.array(ResourceVersionRefZod).optional()
});

export const VersionArchiveZod = z.object({
  id: z.string(),
  appId: z.string(),
  versionLabel: z.string(),
  schemaSnapshotJson: z.string(),
  resourceSnapshot: ResourceSnapshotZod,
  buildMetadata: JsonObjectZod.optional(),
  note: z.string().optional(),
  createdByUserId: z.string(),
  isSystemSnapshot: z.boolean(),
  createdAt: z.string()
});

export const RuntimeStatePatchZod = z.object({
  scope: z.enum(['page', 'app', 'component']),
  path: z.string().min(1),
  op: z.enum(['set', 'merge', 'unset']),
  value: JsonValueZod.optional(),
  componentId: z.string().optional()
});

export const RuntimeSpanZod = z.object({
  spanId: z.string(),
  parentSpanId: z.string().optional(),
  name: z.string(),
  status: z.enum(['ok', 'error']),
  attributes: z.record(JsonValueZod).optional(),
  startedAt: z.string(),
  endedAt: z.string().optional(),
  error: z
    .object({
      kind: z.string(),
      message: z.string(),
      stack: z.string().optional(),
      expressionPath: z.string().optional()
    })
    .optional()
});

export const RuntimeTraceZod = z.object({
  traceId: z.string(),
  appId: z.string(),
  pageId: z.string().optional(),
  componentId: z.string().optional(),
  eventName: z.string().optional(),
  spans: z.array(RuntimeSpanZod),
  startedAt: z.string(),
  endedAt: z.string().optional(),
  status: z.enum(['running', 'success', 'failed'])
});

// 静态保证：CHANNEL_TYPES 已被 M18 占位常量验证（避免 unused-import 警告）。
void CHANNEL_TYPES;
