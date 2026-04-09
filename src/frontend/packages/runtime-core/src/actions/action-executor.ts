import { actionFail, actionOk, type ActionResult, type ActionExecutionSummary } from "./action-result";
import { getActionHandler, type RuntimeActionContext } from "./action-registry";
import type { RuntimeAction } from "./action-types";

let runtimeActionContext: RuntimeActionContext | null = null;

function createEmptyResult(actionType: RuntimeAction["type"]): ActionResult {
  return actionOk(undefined, actionType);
}

function normalizeBooleanResult(result: unknown): boolean {
  if (typeof result === "boolean") {
    return result;
  }
  if (result && typeof result === "object") {
    const value = result as { resultBool?: unknown };
    if (typeof value.resultBool === "boolean") {
      return value.resultBool;
    }
  }
  return true;
}

function getContextAdapter(adapter?: RuntimeActionContext): RuntimeActionContext {
  return {
    getContext: () => runtimeActionContext?.getContext(),
    getExpressionVariables: undefined,
    evaluateExpression: undefined,
    navigate: undefined,
    submitForm: undefined,
    callApi: undefined,
    openDialog: undefined,
    refresh: undefined,
    setGlobalVar: undefined,
    ...runtimeActionContext,
    ...adapter,
  };
}

export function configureRuntimeActionContext(context: RuntimeActionContext): void {
  runtimeActionContext = context;
}

export function clearRuntimeActionContext(): void {
  runtimeActionContext = null;
}

async function shouldExecute(
  action: RuntimeAction,
  adapter: RuntimeActionContext,
): Promise<boolean> {
  if (!action.when) return true;
  if (!adapter.evaluateExpression) return true;

  const variables = adapter.getExpressionVariables?.() ?? {};
  try {
    const result = await adapter.evaluateExpression(action.when, variables);
    return normalizeBooleanResult(result);
  } catch {
    return true;
  }
}

export async function executeAction(
  action: RuntimeAction,
  adapter?: RuntimeActionContext,
): Promise<ActionResult> {
  const contextAdapter = getContextAdapter(adapter);
  const context = contextAdapter.getContext();

  if (!context) {
    return actionFail("Runtime action context missing", action.type);
  }

  const canRun = await shouldExecute(action, contextAdapter);
  if (!canRun) {
    return createEmptyResult(action.type);
  }

  const customHandler = getActionHandler(action.type);
  if (customHandler) {
    return customHandler(action, context, contextAdapter);
  }

  switch (action.type) {
    case "navigate": {
      if (contextAdapter.navigate) {
        return contextAdapter.navigate(action);
      }
      return createEmptyResult("navigate");
    }

    case "setVar": {
      if (contextAdapter.setGlobalVar && action.input?.name) {
        const target = action.input;
        await contextAdapter.setGlobalVar(target.name, target.value);
      }
      return createEmptyResult("setVar");
    }

    case "refresh": {
      return contextAdapter.refresh ? contextAdapter.refresh(action) : createEmptyResult("refresh");
    }

    case "submitForm": {
      return contextAdapter.submitForm ? contextAdapter.submitForm(action) : createEmptyResult("submitForm");
    }

    case "callApi": {
      return contextAdapter.callApi ? contextAdapter.callApi(action) : createEmptyResult("callApi");
    }

    case "openDialog": {
      return contextAdapter.openDialog ? contextAdapter.openDialog(action) : createEmptyResult("openDialog");
    }

    default:
      return actionFail(`Unsupported action type: ${action.type}`, action.type);
  }
}

export async function dispatchRuntimeAction(
  action: RuntimeAction,
  adapter?: RuntimeActionContext,
): Promise<ActionResult> {
  return executeAction(action, adapter);
}

export async function executeActions(
  actions: RuntimeAction[],
  adapter?: RuntimeActionContext,
): Promise<ActionExecutionSummary> {
  const contextAdapter = getContextAdapter(adapter);
  const context = contextAdapter.getContext();
  if (!context) {
    return { success: false, results: [actionFail("Runtime action context missing")] };
  }

  const results: ActionResult[] = [];
  let allSuccess = true;

  for (const action of actions) {
    const result = await executeAction(action, contextAdapter);
    results.push(result);
    if (!result.success) {
      allSuccess = false;
      if (!action.continueOnError) {
        break;
      }
    }
  }

  return { success: allSuccess, results };
}
