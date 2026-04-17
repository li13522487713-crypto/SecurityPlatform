import type { ActionKind, ScopeRoot } from '../shared/enums';
import type { BindingSchema } from './binding';
import type { JsonObject, JsonValue } from '../shared/json';

/**
 * ActionSchema —— 动作 7 子类型 union（docx §10.2.5）。
 *
 * 强约束（PLAN.md §1.3）：
 * - 写动作 set_variable 仅可写 page / app 作用域；M02 + M03 双层校验。
 * - call_workflow / call_chatflow 必须经 dispatch 路由（M13），禁止直接 fetch。
 */
export type ActionSchema =
  | SetVariableAction
  | CallWorkflowAction
  | CallChatflowAction
  | NavigateAction
  | OpenExternalLinkAction
  | ShowToastAction
  | UpdateComponentAction;

export interface ActionBase {
  kind: ActionKind;
  /** 动作 ID（事件链中唯一），用于 trace 标识。*/
  id?: string;
  /** 仅当满足 when 表达式时执行（动作链条件分支，PLAN.md §M03 C03-2）。*/
  when?: string;
  /** 异常分支：本动作失败时执行的备选动作链。*/
  onError?: ActionSchema[];
  /** 弹性策略，docx §10.8 + PLAN.md §M03/M09。*/
  resilience?: ResiliencePolicy;
  /** 是否参与并行编排（同一 chain 中标记 parallel=true 的动作并发执行）。*/
  parallel?: boolean;
}

/** 弹性策略（M09/M11/M19 落地，详见 docs/lowcode-resilience-spec.md）。*/
export interface ResiliencePolicy {
  timeoutMs?: number;
  retry?: { maxAttempts: number; backoff: 'fixed' | 'exponential'; initialDelayMs?: number };
  circuitBreaker?: { failuresThreshold: number; windowMs: number; openMs: number };
  fallback?: { kind: 'workflow' | 'static'; workflowId?: string; staticValue?: JsonValue };
}

export interface SetVariableAction extends ActionBase {
  kind: 'set_variable';
  /**
   * 目标变量路径（必须以 page / app 开头）。
   * 跨作用域写入将在 M02 表达式引擎与 M03 action-runtime 双层抛出 ScopeViolationError。
   */
  targetPath: string;
  /** 写入作用域根（冗余字段，便于 zod 校验）。*/
  scopeRoot: Extract<ScopeRoot, 'page' | 'app'>;
  /** 写入值的 binding（任何 BindingSchema 子类型均可）。*/
  value: BindingSchema;
}

export interface CallWorkflowAction extends ActionBase {
  kind: 'call_workflow';
  workflowId: string;
  /** invoke 模式：sync（同步等待）/ async（提交即返回 jobId）/ batch（批量）。*/
  mode?: 'sync' | 'async' | 'batch';
  /** 入参映射（key=workflow inputName, value=BindingSchema）。*/
  inputMapping?: Record<string, BindingSchema>;
  /** 出参映射（key=workflow outputPath jsonata, value=目标变量路径或组件 prop 路径）。*/
  outputMapping?: Record<string, string>;
  /** 调用期间挂载 loading 状态的目标组件 ID 列表（自动渲染骨架屏，PLAN.md §M03 C03-4）。*/
  loadingTargets?: string[];
  /** 调用失败时挂载 error 状态的目标组件 ID 列表。*/
  errorTargets?: string[];
}

export interface CallChatflowAction extends ActionBase {
  kind: 'call_chatflow';
  chatflowId: string;
  sessionId?: string;
  inputMapping?: Record<string, BindingSchema>;
  /**
   * 流式增量输出的目标 AI 组件 ID。
   * Chatflow 强制经 SSE，由 M11 lowcode-chatflow-adapter 落地。
   */
  streamTarget: string;
}

export interface NavigateAction extends ActionBase {
  kind: 'navigate';
  /** 内部路由 path（页面 code 或绝对路径）。*/
  to: string;
  /** route params。*/
  params?: JsonObject;
  replace?: boolean;
}

export interface OpenExternalLinkAction extends ActionBase {
  kind: 'open_external_link';
  /** 外链 URL（运行时受 webview 白名单约束，M12 + M17）。*/
  url: string;
  target?: '_blank' | '_self';
}

export interface ShowToastAction extends ActionBase {
  kind: 'show_toast';
  message: BindingSchema;
  toastType?: 'info' | 'success' | 'warning' | 'error';
  durationMs?: number;
}

export interface UpdateComponentAction extends ActionBase {
  kind: 'update_component';
  /** 目标组件 ID。*/
  componentId: string;
  /** 要更新的 props（key=prop 名, value=BindingSchema 重写）。*/
  patchProps: Record<string, BindingSchema>;
}
