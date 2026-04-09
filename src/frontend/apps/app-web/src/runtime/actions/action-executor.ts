import type { RuntimeAction } from "./action-types";
import type { ActionResult, ActionExecutionSummary } from "./action-result";
import { evaluateExpression } from "../expressions/cel-preview-client";
import { executeAction as executeActionCore, executeActions as executeActionsCore, configureRuntimeActionContext } from "@atlas/runtime-core";
import { router } from "@/router";
import { useRuntimeContextStore } from "../context/runtime-context-store";
import { actionOk } from "./action-result";
import type { RuntimeActionContext } from "./action-registry";

function buildRuntimeActionContextAdapter(store: ReturnType<typeof useRuntimeContextStore>): RuntimeActionContext {
  return {
    getContext: () => store.context,
    getExpressionVariables: () => store.getExpressionVariables(),
    evaluateExpression: async (expression: string, vars: Record<string, unknown>) => {
      const result = await evaluateExpression(expression, vars);
      return result;
    },
    navigate: async (action) => {
      const input = action.input;
      const pageKey = input?.pageKey ?? "";
      const appKey = (store.context.app?.appKey || "").trim();
      const path = `/apps/${encodeURIComponent(appKey)}/r/${encodeURIComponent(pageKey)}`;
      if (input?.replace) {
        await router.replace(path);
      } else {
        await router.push(path);
      }
      return actionOk(undefined, "navigate");
    },
    setGlobalVar: (name: string, value: unknown) => {
      store.setGlobalVar(name, value);
    },
    submitForm: async () => actionOk(undefined, "submitForm"),
    refresh: async () => actionOk(undefined, "refresh"),
    callApi: async () => actionOk(undefined, "callApi"),
    openDialog: async () => actionOk(undefined, "openDialog"),
  };
}

export async function executeAction(action: RuntimeAction): Promise<ActionResult> {
  const store = useRuntimeContextStore();
  const adapter = buildRuntimeActionContextAdapter(store);
  configureRuntimeActionContext(adapter);
  return executeActionCore(action, adapter);
}

export async function executeActions(actions: RuntimeAction[]): Promise<ActionExecutionSummary> {
  const store = useRuntimeContextStore();
  const adapter = buildRuntimeActionContextAdapter(store);
  configureRuntimeActionContext(adapter);
  return executeActionsCore(actions, adapter);
}

export { configureRuntimeActionContext } from "@atlas/runtime-core";
