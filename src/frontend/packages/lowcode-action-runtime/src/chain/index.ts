/**
 * 动作链编排（M03 C03-2）。
 *
 * - 顺序：默认按声明顺序串行
 * - 并行：相邻 parallel=true 的动作进入并行批次
 * - 条件：when 表达式评估为 falsy 时跳过
 * - 异常：动作失败时执行其 onError 子链；失败仍向上抛
 */

import type { ActionSchema, JsonValue, RuntimeStatePatch } from '@atlas/lowcode-schema';
import { evaluate as evalExpression } from '@atlas/lowcode-expression/jsonata';
import { getActionHandler, type ActionContext, type ActionResult } from '../extend';

export interface ChainExecutionResult {
  patches: RuntimeStatePatch[];
  outputs: Record<string, JsonValue>;
  messages: ActionResult['messages'];
  errors: ReadonlyArray<{ actionId?: string; kind: string; message: string }>;
}

async function executeSingle(action: ActionSchema, ctx: ActionContext): Promise<ActionResult> {
  const handler = getActionHandler(action.kind);
  if (!handler) {
    throw new Error(`未注册的动作 kind=${action.kind}（请先 registerActionKind 或注入 invokeDispatch）`);
  }
  return handler(action, ctx);
}

async function maybeRun(action: ActionSchema, ctx: ActionContext): Promise<ActionResult | null> {
  if (action.when) {
    const cond = await evalExpression(action.when, ctx.state as JsonValue);
    if (!cond) return null;
  }
  try {
    return await executeSingle(action, ctx);
  } catch (err) {
    if (action.onError && action.onError.length > 0) {
      // 失败 → 进入 onError 子链；子链结果合并；子链失败再向上抛
      const sub = await executeChain(action.onError, ctx);
      return {
        patches: sub.patches,
        outputs: sub.outputs,
        messages: sub.messages
      };
    }
    throw err;
  }
}

/**
 * 执行整条动作链。
 * 相邻 parallel=true 的动作进入同一个并行批次（Promise.all）。
 */
export async function executeChain(actions: ReadonlyArray<ActionSchema>, ctx: ActionContext): Promise<ChainExecutionResult> {
  const patches: RuntimeStatePatch[] = [];
  const outputs: Record<string, JsonValue> = {};
  const messages: NonNullable<ActionResult['messages']>[] = [];
  const errors: { actionId?: string; kind: string; message: string }[] = [];

  let i = 0;
  while (i < actions.length) {
    if (actions[i].parallel) {
      // 收集相邻并行批
      const batch: ActionSchema[] = [];
      while (i < actions.length && actions[i].parallel) {
        batch.push(actions[i]);
        i++;
      }
      const settled = await Promise.allSettled(batch.map((a) => maybeRun(a, ctx)));
      for (let k = 0; k < settled.length; k++) {
        const r = settled[k];
        if (r.status === 'fulfilled') {
          if (r.value) {
            if (r.value.patches) patches.push(...r.value.patches);
            if (r.value.outputs) Object.assign(outputs, r.value.outputs);
            if (r.value.messages) messages.push(r.value.messages);
          }
        } else {
          const reason = r.reason instanceof Error ? r.reason : new Error(String(r.reason));
          errors.push({ actionId: batch[k].id, kind: batch[k].kind, message: reason.message });
        }
      }
    } else {
      const a = actions[i];
      i++;
      try {
        const r = await maybeRun(a, ctx);
        if (r) {
          if (r.patches) patches.push(...r.patches);
          if (r.outputs) Object.assign(outputs, r.outputs);
          if (r.messages) messages.push(r.messages);
        }
      } catch (err) {
        const reason = err instanceof Error ? err : new Error(String(err));
        errors.push({ actionId: a.id, kind: a.kind, message: reason.message });
      }
    }
  }

  return { patches, outputs, messages: messages.flat(), errors };
}
