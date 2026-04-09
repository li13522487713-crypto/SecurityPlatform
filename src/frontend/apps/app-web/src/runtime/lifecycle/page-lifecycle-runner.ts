import type { PageLifecycleHooks } from "./lifecycle-types";
import type { ActionExecutionSummary } from "../actions/action-result";
import { runLifecycleHook as runLifecycleHookCore } from "@atlas/runtime-core";
import { evaluateExpression } from "../expressions/cel-preview-client";
import { useRuntimeContextStore } from "../context/runtime-context-store";
import type { RuntimeActionContext } from "../actions/action-registry";

function buildRuntimeActionContextAdapter(): RuntimeActionContext {
  const store = useRuntimeContextStore();
  return {
    getContext: () => store.context,
    getExpressionVariables: () => store.getExpressionVariables(),
    evaluateExpression: async (expression: string, vars: Record<string, unknown>) => {
      return evaluateExpression(expression, vars);
    },
    setGlobalVar: (name: string, value: unknown) => {
      store.setGlobalVar(name, value);
    },
  };
}

export async function runLifecycleHook(
  hooks: PageLifecycleHooks | undefined,
  hookName: keyof PageLifecycleHooks,
): Promise<ActionExecutionSummary> {
  if (!hooks) return { success: true, results: [] };
  const actions = hooks[hookName];
  if (!actions || actions.length === 0) return { success: true, results: [] };
  const adapter = buildRuntimeActionContextAdapter();
  return runLifecycleHookCore(hooks, hookName, adapter);
}
