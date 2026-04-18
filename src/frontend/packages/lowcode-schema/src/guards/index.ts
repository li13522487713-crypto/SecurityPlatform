/**
 * @atlas/lowcode-schema/guards — 类型守卫与作用域守卫（M01）。
 *
 * 设计要点：
 * - 类型守卫覆盖 BindingSchema 5 子类型 + ContentParamSchema 6 子类型 + ActionSchema 7 子类型。
 * - 作用域守卫识别可写 / 只读作用域，配合 M02/M03 双层校验。
 */

import type {
  BindingSchema,
  StaticBinding,
  VariableBinding,
  ExpressionBinding,
  WorkflowOutputBinding,
  ChatflowOutputBinding
} from '../types/binding';
import type {
  ContentParamSchema,
  TextContentParam,
  ImageContentParam,
  DataContentParam,
  LinkContentParam,
  MediaContentParam,
  AiContentParam
} from '../types/content-param';
import type {
  ActionSchema,
  SetVariableAction,
  CallWorkflowAction,
  CallChatflowAction,
  NavigateAction,
  OpenExternalLinkAction,
  ShowToastAction,
  UpdateComponentAction
} from '../types/action';
import type { ScopeRoot, WritableScope } from '../shared/enums';

// ---------- Binding ----------
export const isStaticBinding = (b: BindingSchema): b is StaticBinding => b.sourceType === 'static';
export const isVariableBinding = (b: BindingSchema): b is VariableBinding => b.sourceType === 'variable';
export const isExpressionBinding = (b: BindingSchema): b is ExpressionBinding => b.sourceType === 'expression';
export const isWorkflowOutputBinding = (b: BindingSchema): b is WorkflowOutputBinding => b.sourceType === 'workflow_output';
export const isChatflowOutputBinding = (b: BindingSchema): b is ChatflowOutputBinding => b.sourceType === 'chatflow_output';

// ---------- ContentParam ----------
export const isTextContentParam = (p: ContentParamSchema): p is TextContentParam => p.kind === 'text';
export const isImageContentParam = (p: ContentParamSchema): p is ImageContentParam => p.kind === 'image';
export const isDataContentParam = (p: ContentParamSchema): p is DataContentParam => p.kind === 'data';
export const isLinkContentParam = (p: ContentParamSchema): p is LinkContentParam => p.kind === 'link';
export const isMediaContentParam = (p: ContentParamSchema): p is MediaContentParam => p.kind === 'media';
export const isAiContentParam = (p: ContentParamSchema): p is AiContentParam => p.kind === 'ai';

// ---------- Action ----------
export const isSetVariableAction = (a: ActionSchema): a is SetVariableAction => a.kind === 'set_variable';
export const isCallWorkflowAction = (a: ActionSchema): a is CallWorkflowAction => a.kind === 'call_workflow';
export const isCallChatflowAction = (a: ActionSchema): a is CallChatflowAction => a.kind === 'call_chatflow';
export const isNavigateAction = (a: ActionSchema): a is NavigateAction => a.kind === 'navigate';
export const isOpenExternalLinkAction = (a: ActionSchema): a is OpenExternalLinkAction => a.kind === 'open_external_link';
export const isShowToastAction = (a: ActionSchema): a is ShowToastAction => a.kind === 'show_toast';
export const isUpdateComponentAction = (a: ActionSchema): a is UpdateComponentAction => a.kind === 'update_component';

// ---------- Scope ----------
const READONLY_SCOPES: ReadonlySet<ScopeRoot> = new Set([
  'system',
  'component',
  'event',
  'workflow.outputs',
  'chatflow.outputs'
]);

/** 判断作用域是否为可写（page / app）。*/
export const isWritableScope = (scope: ScopeRoot): scope is WritableScope =>
  scope === 'page' || scope === 'app';

/** 判断作用域是否为只读（system / component / event / workflow.outputs / chatflow.outputs）。*/
export const isReadonlyScope = (scope: ScopeRoot): boolean => READONLY_SCOPES.has(scope);

/**
 * 从路径推断作用域根。
 * 'app.foo.bar' → 'app'
 * 'workflow.outputs.foo' → 'workflow.outputs'
 * 'unknown.foo' → undefined
 */
export const inferScopeRoot = (path: string): ScopeRoot | undefined => {
  if (path.startsWith('workflow.outputs')) return 'workflow.outputs';
  if (path.startsWith('chatflow.outputs')) return 'chatflow.outputs';
  if (path.startsWith('component.')) return 'component';
  if (path.startsWith('event.')) return 'event';
  if (path.startsWith('page.')) return 'page';
  if (path.startsWith('app.')) return 'app';
  if (path.startsWith('system.')) return 'system';
  return undefined;
};

/** 作用域违规错误：跨作用域写入或写入只读作用域。*/
export class ScopeViolationError extends Error {
  public readonly path: string;
  public readonly attemptedScope: ScopeRoot | undefined;
  public readonly currentScope: ScopeRoot | undefined;

  constructor(message: string, options: { path: string; attemptedScope?: ScopeRoot; currentScope?: ScopeRoot }) {
    super(message);
    this.name = 'ScopeViolationError';
    this.path = options.path;
    this.attemptedScope = options.attemptedScope;
    this.currentScope = options.currentScope;
  }
}

/**
 * 校验某个写入路径是否合法：
 * - 路径前缀作用域必须为可写（仅 page / app）。
 * - 写入路径作用域允许与当前作用域不同（如 page 动作写 app 是允许的，跨作用域写入由调用方按 M02/M03 强约束决定是否豁免）。
 */
export const assertWritable = (path: string): void => {
  const target = inferScopeRoot(path);
  if (!target) {
    throw new ScopeViolationError(`无法识别写入路径的作用域：${path}`, { path });
  }
  if (!isWritableScope(target)) {
    throw new ScopeViolationError(
      `禁止写入只读作用域：path=${path} scope=${target}（仅允许 page / app 可写）`,
      { path, attemptedScope: target }
    );
  }
};
