/**
 * 动作扩展机制（M03 C03-7）。
 *
 * Adapter 通过 registerActionKind 注入新动作执行器；7 种内置动作由 dispatcher 自身注册。
 * 此模块只提供注册表与查询接口，不耦合具体动作。
 */

import type { ActionSchema, RuntimeStatePatch, JsonValue } from '@atlas/lowcode-schema';

export interface ActionContext {
  /** 当前 page / app / component 状态快照（合并）。*/
  state: Record<string, JsonValue>;
  /** 事件 payload（来自触发事件，如 onClick.event）。*/
  eventPayload?: Record<string, JsonValue>;
  /** trace id（与 M13 dispatch 协议一致）。*/
  traceId?: string;
  /** 调用 dispatch 协议。各 Adapter 在执行 call_workflow 等动作时通过此入口委托。*/
  invokeDispatch?: (action: ActionSchema) => Promise<ActionResult>;
  /**
   * P4-4：副作用 patches 钩子。call_workflow 失败时即使要 throw 让 onError 处理，
   * 也需要先把 loading=false / errorTargets=error 状态 patches 提交，避免按钮卡 loading。
   * 默认为 undefined（兼容旧调用）；chain executor 注入此钩子让 dispatcher 可以"finally"语义提交副作用。
   */
  applySideEffectPatches?: (patches: RuntimeStatePatch[]) => Promise<void> | void;
}

/** 单动作执行结果。*/
export interface ActionResult {
  /** 该动作产生的 statePatches。*/
  patches?: RuntimeStatePatch[];
  /** 该动作产生的输出（供后续动作通过 event/workflow.outputs 引用）。*/
  outputs?: Record<string, JsonValue>;
  /** 自定义 toast / notification 消息。*/
  messages?: ReadonlyArray<{ kind: 'info' | 'success' | 'warning' | 'error'; text: string }>;
}

export type ActionHandler<T extends ActionSchema = ActionSchema> = (action: T, ctx: ActionContext) => Promise<ActionResult>;

const REGISTRY = new Map<string, ActionHandler>();

export function registerActionKind<T extends ActionSchema>(kind: T['kind'], handler: ActionHandler<T>): void {
  if (REGISTRY.has(kind)) {
    throw new Error(`动作 kind=${kind} 已注册，禁止覆盖`);
  }
  REGISTRY.set(kind, handler as ActionHandler);
}

export function getActionHandler(kind: string): ActionHandler | undefined {
  return REGISTRY.get(kind);
}

export function listRegisteredKinds(): string[] {
  return Array.from(REGISTRY.keys());
}

/** 仅供测试：清空注册表。*/
export function __resetActionRegistryForTesting(): void {
  REGISTRY.clear();
}
