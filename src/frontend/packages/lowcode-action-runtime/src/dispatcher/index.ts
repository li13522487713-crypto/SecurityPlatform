/**
 * ActionDispatcher（M03 C03-1）：注册 7 种内置动作 + 暴露 dispatch 入口。
 *
 * 强约束（PLAN.md §1.3 #2）：
 * - call_workflow / call_chatflow / call_plugin（M18）等"对外"动作必须委托给 ctx.invokeDispatch（→ M13 RuntimeEventsController.Dispatch）；
 *   本 dispatcher 内部内置实现仅做"无后端依赖"的纯前端动作（set_variable / navigate / open_external_link / show_toast / update_component）。
 * - call_workflow / call_chatflow 在 M03 占位为 invokeDispatch 委托模板；M09 / M11 适配器 attach 真实实现。
 */

import type { ActionSchema, JsonValue, RuntimeStatePatch, SetVariableAction, UpdateComponentAction, NavigateAction, OpenExternalLinkAction, ShowToastAction, CallWorkflowAction, CallChatflowAction, BindingSchema } from '@atlas/lowcode-schema';
import { isStaticBinding } from '@atlas/lowcode-schema';
import { evaluate as evalExpression } from '@atlas/lowcode-expression/jsonata';
import { ensureWritablePath } from '../scope-guard';
import { registerActionKind, type ActionContext, type ActionResult, type ActionHandler } from '../extend';
import { buildLoadingPatches, buildErrorPatches } from '../loading';
import { withResilience } from '../resilience';

/** 把 binding 解析为具体值。仅支持 static / variable / expression（call_workflow / call_chatflow 输出由 invokeDispatch 在外部解析）。*/
async function resolveBinding(binding: BindingSchema, ctx: ActionContext): Promise<JsonValue> {
  if (isStaticBinding(binding)) return binding.value;
  if (binding.sourceType === 'variable') {
    return (await evalExpression(binding.path, ctx.state as JsonValue)) as JsonValue;
  }
  if (binding.sourceType === 'expression') {
    return (await evalExpression(binding.expression, ctx.state as JsonValue)) as JsonValue;
  }
  // workflow_output / chatflow_output 在 dispatcher 内不直接解析；
  // 此类绑定通常应在 inputMapping → action 内传给 adapter。给个保守行为：返回 fallback 或 null。
  return (binding.fallback ?? null) as JsonValue;
}

const setVariableHandler: ActionHandler<SetVariableAction> = async (action, ctx) => {
  ensureWritablePath(action.targetPath);
  const value = await resolveBinding(action.value, ctx);
  return {
    patches: [
      {
        scope: action.scopeRoot,
        path: action.targetPath,
        op: 'set',
        value
      }
    ]
  };
};

const updateComponentHandler: ActionHandler<UpdateComponentAction> = async (action, ctx) => {
  const patches: RuntimeStatePatch[] = [];
  for (const [propKey, binding] of Object.entries(action.patchProps)) {
    const value = await resolveBinding(binding, ctx);
    patches.push({
      scope: 'component',
      componentId: action.componentId,
      path: `component.${action.componentId}.${propKey}`,
      op: 'set',
      value
    });
  }
  return { patches };
};

const navigateHandler: ActionHandler<NavigateAction> = async (action) => {
  return {
    outputs: { navigate: { to: action.to, params: action.params ?? {}, replace: !!action.replace } as unknown as JsonValue }
  };
};

const openExternalLinkHandler: ActionHandler<OpenExternalLinkAction> = async (action) => {
  // 外链白名单校验由 M12 lowcode-webview-policy-adapter 在执行前拦截；
  // 此处仅产出"待打开"指令，由 runtime-web (M08) 调用 window.open。
  return {
    outputs: { openExternalLink: { url: action.url, target: action.target ?? '_blank' } as unknown as JsonValue }
  };
};

const showToastHandler: ActionHandler<ShowToastAction> = async (action, ctx) => {
  const text = String((await resolveBinding(action.message, ctx)) ?? '');
  return {
    messages: [{ kind: action.toastType ?? 'info', text }]
  };
};

const callWorkflowHandler: ActionHandler<CallWorkflowAction> = async (action, ctx) => {
  if (!ctx.invokeDispatch) {
    throw new Error('call_workflow 动作要求 ActionContext.invokeDispatch 已注入（由 dispatch / Adapter 在 M13 / M09 提供）');
  }
  const loadingPatches = action.loadingTargets ? buildLoadingPatches(action.loadingTargets, true) : [];
  try {
    const result = await withResilience(
      () => ctx.invokeDispatch!(action),
      {
        policy: action.resilience,
        circuitKey: `workflow:${action.workflowId}`,
        context: { actionId: action.id }
      }
    );
    const finalPatches: RuntimeStatePatch[] = [
      ...loadingPatches,
      ...(action.loadingTargets ? buildLoadingPatches(action.loadingTargets, false) : []),
      ...(action.errorTargets ? buildErrorPatches(action.errorTargets, null) : []),
      ...(result?.patches ?? [])
    ];
    return { patches: finalPatches, outputs: result?.outputs, messages: result?.messages };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    const finalPatches: RuntimeStatePatch[] = [
      ...loadingPatches,
      ...(action.loadingTargets ? buildLoadingPatches(action.loadingTargets, false) : []),
      ...(action.errorTargets ? buildErrorPatches(action.errorTargets, { message, kind: 'workflow_error' }) : [])
    ];
    return { patches: finalPatches, messages: [{ kind: 'error', text: message }] };
  }
};

const callChatflowHandler: ActionHandler<CallChatflowAction> = async (action, ctx) => {
  if (!ctx.invokeDispatch) {
    throw new Error('call_chatflow 动作要求 ActionContext.invokeDispatch 已注入（由 M11 chatflow-adapter 提供）');
  }
  const result = await ctx.invokeDispatch(action);
  return result ?? {};
};

let installed = false;

/** 安装 7 种内置动作处理器（dispatcher 入口；外部 Adapter 可在此基础上 registerActionKind 扩展）。*/
export function installBuiltInActions(): void {
  if (installed) return;
  installed = true;
  registerActionKind<SetVariableAction>('set_variable', setVariableHandler);
  registerActionKind<UpdateComponentAction>('update_component', updateComponentHandler);
  registerActionKind<NavigateAction>('navigate', navigateHandler);
  registerActionKind<OpenExternalLinkAction>('open_external_link', openExternalLinkHandler);
  registerActionKind<ShowToastAction>('show_toast', showToastHandler);
  registerActionKind<CallWorkflowAction>('call_workflow', callWorkflowHandler);
  registerActionKind<CallChatflowAction>('call_chatflow', callChatflowHandler);
}

/** 仅供测试：重置已安装标志。*/
export function __resetInstalledForTesting(): void {
  installed = false;
}

export type { ActionContext, ActionResult } from '../extend';
export type { RuntimeStatePatch } from '@atlas/lowcode-schema';
