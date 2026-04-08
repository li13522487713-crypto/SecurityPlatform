/**
 * 统一动作执行器。
 *
 * 接收 RuntimeAction，从 ActionRegistry 查找对应处理器并执行。
 * 执行前后更新 RuntimeContextStore。
 */

import { router } from "@/router";
import { useRuntimeContextStore } from "../context/runtime-context-store";
import type { RuntimeAction } from "./action-types";
import type { ActionResult } from "./action-result";
import { actionOk, actionFail } from "./action-result";
import { getActionHandler } from "./action-registry";

export async function executeAction(action: RuntimeAction): Promise<ActionResult> {
  const store = useRuntimeContextStore();
  const context = store.context;

  const customHandler = getActionHandler(action.type);
  if (customHandler) {
    return customHandler(action, context);
  }

  switch (action.type) {
    case "navigate":
      return handleNavigate(action, context);
    case "setVar":
      return handleSetVar(action, store);
    case "refresh":
      return actionOk();
    case "submitForm":
      return actionOk();
    case "callApi":
      return actionOk();
    case "openDialog":
      return actionOk();
    default:
      return actionFail(`Unsupported action type: ${(action as RuntimeAction).type}`);
  }
}

async function handleNavigate(
  action: Extract<RuntimeAction, { type: "navigate" }>,
  context: import("../context/runtime-context-types").RuntimeContext,
): Promise<ActionResult> {
  try {
    const appKey = context.app.appKey;
    const path = `/apps/${encodeURIComponent(appKey)}/r/${encodeURIComponent(action.pageKey)}`;
    await router.push(path);
    return actionOk();
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Navigation failed");
  }
}

function handleSetVar(
  action: Extract<RuntimeAction, { type: "setVar" }>,
  store: ReturnType<typeof useRuntimeContextStore>,
): Promise<ActionResult> {
  store.setGlobalVar(action.name, action.value);
  return Promise.resolve(actionOk());
}

export async function executeActions(actions: RuntimeAction[]): Promise<ActionResult[]> {
  const results: ActionResult[] = [];
  for (const action of actions) {
    const result = await executeAction(action);
    results.push(result);
    if (!result.success) break;
  }
  return results;
}
