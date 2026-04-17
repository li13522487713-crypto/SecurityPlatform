/**
 * 事件分发（M08 C08-5 / C08-6）。
 *
 * 强约束（PLAN.md §1.3 #2）：
 * - 所有运行时事件必须经此入口 → ctx.dispatchClient.send；
 * - workflow / chatflow / asset / trigger / plugin / external link 等"对外"动作由 dispatch 内部路由。
 */

import type { ActionSchema, EventSchema } from '@atlas/lowcode-schema';
import type { RuntimeContext } from '../context';
import { useRuntimeStore } from '../store';

export interface DispatchOptions {
  /** 触发事件的组件 ID。*/
  componentId?: string;
  /** 事件名（与 EventSchema.name 对齐）。*/
  eventName?: string;
}

export async function dispatchEvent(event: EventSchema, ctx: RuntimeContext, options: DispatchOptions = {}): Promise<void> {
  if (!event.actions || event.actions.length === 0) return;
  await dispatchActions(event.actions, ctx, { ...options, eventName: event.name });
}

export async function dispatchActions(actions: ReadonlyArray<ActionSchema>, ctx: RuntimeContext, options: DispatchOptions = {}): Promise<void> {
  const r = await ctx.dispatchClient.send({
    appId: ctx.appId,
    pageId: ctx.pageId,
    versionId: ctx.versionId,
    componentId: options.componentId,
    eventName: options.eventName,
    actions: [...actions],
    ...(ctx.buildDispatchRequest ? ctx.buildDispatchRequest(actions[0]) : {})
  });
  if (r.statePatches && r.statePatches.length > 0) {
    useRuntimeStore.getState().applyPatches(r.statePatches);
  }
}
