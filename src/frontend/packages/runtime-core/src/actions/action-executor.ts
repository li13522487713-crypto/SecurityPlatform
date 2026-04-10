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

async function evaluateCondition(
  expression: string | undefined,
  adapter: RuntimeActionContext,
): Promise<{ match: boolean; errorMessage?: string }> {
  if (!expression) {
    return { match: true };
  }
  if (!adapter.evaluateExpression) {
    return {
      match: false,
      errorMessage: "Runtime action condition evaluator is unavailable",
    };
  }

  const variables = adapter.getExpressionVariables?.() ?? {};
  try {
    const result = await adapter.evaluateExpression(expression, variables);
    return { match: normalizeBooleanResult(result) };
  } catch (error) {
    return {
      match: false,
      errorMessage: error instanceof Error ? error.message : "Runtime action condition evaluation failed",
    };
  }
}

function withExpressionScope(
  baseAdapter: RuntimeActionContext,
  scopedVariables: Record<string, unknown> | undefined,
): RuntimeActionContext {
  if (!scopedVariables || Object.keys(scopedVariables).length === 0) {
    return baseAdapter;
  }

  return {
    ...baseAdapter,
    getExpressionVariables: () => {
      const rootVariables = baseAdapter.getExpressionVariables?.() ?? {};
      return {
        ...rootVariables,
        ...scopedVariables,
      };
    },
  };
}

function normalizeItemsValue(value: unknown): unknown[] {
  if (!value) return [];
  if (Array.isArray(value)) return value;
  return [];
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
): Promise<{ match: boolean; errorMessage?: string }> {
  return evaluateCondition(action.when, adapter);
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
  if (!canRun.match) {
    if (canRun.errorMessage) {
      return actionFail(canRun.errorMessage, action.type);
    }
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

    case "branch": {
      if (!action.input) return actionFail("Branch action input is missing", action.type);

      const conditionResult = await evaluateCondition(action.input.condition, contextAdapter);
      if (conditionResult.errorMessage) {
        return actionFail(
          conditionResult.errorMessage || "Branch condition evaluation failed",
          action.type,
        );
      }

      const thenActions = Array.isArray(action.input.then) ? action.input.then : [];
      const elseActions = Array.isArray(action.input.else) ? action.input.else : [];
      const selectedActions = conditionResult.match ? thenActions : elseActions;
      const selectedBranch = conditionResult.match ? "then" : "else";

      const execution = await executeActions(selectedActions, contextAdapter);
      if (!execution.success && !action.continueOnError) {
        return {
          ...actionFail("Branch action execution failed", action.type),
          data: {
            branch: selectedBranch,
            results: execution.results,
          },
        };
      }

      return {
        ...actionOk(
          {
            branch: selectedBranch,
            results: execution.results,
          },
          action.type,
        ),
        success: execution.success || (action.continueOnError ?? false),
      };
    }

    case "foreach": {
      if (!action.input) {
        return actionFail("Foreach action input is missing", action.type);
      }
      if (!contextAdapter.evaluateExpression) {
        return actionFail("Foreach action requires expression evaluator", action.type);
      }

      const itemExpression = action.input.items;
      const variables = contextAdapter.getExpressionVariables?.() ?? {};
      let items: unknown;
      try {
        items = await contextAdapter.evaluateExpression(itemExpression, variables);
      } catch (error) {
        return actionFail(
          error instanceof Error ? error.message : "Foreach action items expression evaluation failed",
          action.type,
        );
      }

      const normalizedItems = normalizeItemsValue(items);
      if (!Array.isArray(items)) {
        return actionFail(
          `Foreach action items must be an array, got ${typeof items}`,
          action.type,
        );
      }

      const loopItemName = action.input.itemName?.trim() || "item";
      const childActions = Array.isArray(action.input.actions) ? action.input.actions : [];
      const loopResults: ActionResult[] = [];
      let allSuccess = true;

      for (const [index, item] of normalizedItems.entries()) {
        const loopAdapter = withExpressionScope(contextAdapter, {
          [loopItemName]: item,
          [`${loopItemName}Index`]: index,
        });

        const actionSummary = await executeActions(childActions, loopAdapter);
        loopResults.push(...actionSummary.results);
        if (!actionSummary.success) {
          allSuccess = false;
          if (!action.continueOnError) {
            break;
          }
        }
      }

      if (!allSuccess && !action.continueOnError) {
        return {
          ...actionFail("Foreach action execution failed", action.type),
          data: {
            itemName: loopItemName,
            itemsCount: normalizedItems.length,
            results: loopResults,
          },
        };
      }

      return actionOk(
        {
          itemName: loopItemName,
          itemsCount: normalizedItems.length,
          results: loopResults,
        },
        action.type,
      );
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
