/**
 * 统一动作执行器。
 *
 * 接收 RuntimeAction，从 ActionRegistry 查找对应处理器并执行。
 * 支持 `when` 条件判断（CEL 表达式）和 `continueOnError` 容错。
 */

import { router } from "@/router";
import { useRuntimeContextStore } from "../context/runtime-context-store";
import type { RuntimeAction } from "./action-types";
import type { ActionResult, ActionExecutionSummary } from "./action-result";
import { actionOk, actionFail } from "./action-result";
import { getActionHandler } from "./action-registry";
import { evaluateExpression } from "../expressions/cel-preview-client";

async function shouldExecute(action: RuntimeAction): Promise<boolean> {
  if (!action.when) return true;

  const store = useRuntimeContextStore();
  try {
    const vars = store.getExpressionVariables();
    const result = await evaluateExpression(action.when, vars);
    return result.resultBool === true;
  } catch {
    return true;
  }
}

export async function executeAction(action: RuntimeAction): Promise<ActionResult> {
  const store = useRuntimeContextStore();
  const context = store.context;

  const canRun = await shouldExecute(action);
  if (!canRun) {
    return actionOk(undefined, action.type);
  }

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
      return actionOk(undefined, "refresh");
    case "submitForm":
      return actionOk(undefined, "submitForm");
    case "callApi":
      return actionOk(undefined, "callApi");
    case "openDialog":
      return actionOk(undefined, "openDialog");
    default:
      return actionFail(`Unsupported action type: ${(action as RuntimeAction).type}`, action.type);
  }
}

async function handleNavigate(
  action: Extract<RuntimeAction, { type: "navigate" }>,
  context: import("../context/runtime-context-types").RuntimeContext,
): Promise<ActionResult> {
  try {
    const input = action.input;
    const pageKey = input?.pageKey ?? "";
    const appKey = context.app.appKey;
    const path = `/apps/${encodeURIComponent(appKey)}/r/${encodeURIComponent(pageKey)}`;

    if (input?.replace) {
      await router.replace(path);
    } else {
      await router.push(path);
    }
    return actionOk(undefined, "navigate");
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Navigation failed", "navigate");
  }
}

function handleSetVar(
  action: Extract<RuntimeAction, { type: "setVar" }>,
  store: ReturnType<typeof useRuntimeContextStore>,
): Promise<ActionResult> {
  const input = action.input;
  if (input?.name) {
    store.setGlobalVar(input.name, input.value);
  }
  return Promise.resolve(actionOk(undefined, "setVar"));
}

export async function executeActions(actions: RuntimeAction[]): Promise<ActionExecutionSummary> {
  const results: ActionResult[] = [];
  let allSuccess = true;

  for (const action of actions) {
    const result = await executeAction(action);
    results.push(result);
    if (!result.success) {
      allSuccess = false;
      if (!action.continueOnError) break;
    }
  }

  return { success: allSuccess, results };
}
