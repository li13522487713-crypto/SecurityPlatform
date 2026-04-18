/**
 * 两种编排哲学切换（M09 C09-8）。
 *
 * - explicit：显式节点编排（DAG 完整渲染，由 @coze-workflow/playground 维护）
 * - agentic：模型自决（LLM tool calling 协议，节点面板隐藏中间链路，仅展示 LLM + Tool 池）
 *
 * 编排层只是上层切换协议；真正落到 DAG 引擎的执行逻辑由 M20 后端 AgenticOrchestrator 实施。
 */

import type { JsonObject } from '@atlas/lowcode-schema';

export type OrchestrationMode = 'explicit' | 'agentic';

export interface OrchestrationContext {
  workflowId: string;
  mode: OrchestrationMode;
  /** agentic 模式下可用 tools 列表（tool name → 描述）。*/
  agenticTools?: ReadonlyArray<{ name: string; description: string }>;
}

/** 切换模式时构造默认配置（agentic 模式需注入空 tools）。*/
export function switchMode(prev: OrchestrationContext, next: OrchestrationMode): OrchestrationContext {
  if (next === 'agentic') {
    return { ...prev, mode: 'agentic', agenticTools: prev.agenticTools ?? [] };
  }
  // explicit 模式不持有 agenticTools
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { agenticTools: _drop, ...rest } = prev;
  return { ...rest, mode: 'explicit' };
}

/** 是否需要在节点面板上隐藏 LLM 中间节点（agentic 模式）。*/
export function shouldHideIntermediateNodes(mode: OrchestrationMode): boolean {
  return mode === 'agentic';
}

/** agentic 模式下 invokeWorkflow 请求附带的额外 metadata。*/
export function buildAgenticInvokeMetadata(ctx: OrchestrationContext): JsonObject {
  if (ctx.mode !== 'agentic') return {};
  return {
    orchestration: 'agentic',
    tools: (ctx.agenticTools ?? []).map((t) => ({ name: t.name, description: t.description })) as unknown as JsonObject
  };
}
