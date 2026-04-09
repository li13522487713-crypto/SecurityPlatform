import type { RuntimeAction } from "./action-types";
import type { ActionResult, ActionExecutionSummary } from "./action-result";
import { actionFail, actionOk } from "./action-result";
import { evaluateExpression } from "../expressions/cel-preview-client";
import { executeAction as executeActionCore, executeActions as executeActionsCore, configureRuntimeActionContext } from "@atlas/runtime-core";
import { router } from "@/router";
import { useRuntimeContextStore } from "../context/runtime-context-store";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api-core";
import type { ApiResponse, JsonValue } from "@atlas/shared-core";
import type { RuntimeActionContext } from "./action-registry";
import { runLifecycleHook } from "../lifecycle/page-lifecycle-runner";
import type { PageLifecycleHooks } from "../lifecycle/lifecycle-types";

let currentLifecycleHooks: PageLifecycleHooks | null = null;

export function setRuntimeLifecycleHooks(hooks: PageLifecycleHooks | null): void {
  currentLifecycleHooks = hooks;
}

export function clearRuntimeLifecycleHooks(): void {
  currentLifecycleHooks = null;
}

async function runSubmitLifecycleHook(
  hookName: "beforeSubmit" | "afterSubmit",
): Promise<ActionExecutionSummary> {
  if (!currentLifecycleHooks) {
    return { success: true, results: [] };
  }

  return runLifecycleHook(currentLifecycleHooks, hookName);
}

function buildRuntimeActionContextAdapter(store: ReturnType<typeof useRuntimeContextStore>): RuntimeActionContext {
  const context = store.context;

  return {
    getContext: () => context,
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
    submitForm: async (action) => {
      const beforeResult = await runSubmitLifecycleHook("beforeSubmit");
      if (!beforeResult.success) {
        return actionFail("beforeSubmit hook failed", "submitForm");
      }

      const formKey = action.input?.formKey;
      if (typeof window !== "undefined") {
        window.dispatchEvent(new CustomEvent("atlas-runtime-submit", {
          detail: {
            kind: "submitForm",
            formKey,
            input: action.input,
            appKey: context.app.appKey,
            pageKey: context.page.pageKey,
          },
        }));
      }

      const afterResult = await runSubmitLifecycleHook("afterSubmit");
      if (!afterResult.success) {
        return actionFail("afterSubmit hook failed", "submitForm");
      }

      return actionOk({
        formKey,
      }, "submitForm");
    },
    refresh: async (action) => {
      if (typeof window !== "undefined") {
        window.dispatchEvent(new CustomEvent("atlas-runtime-refresh", {
          detail: {
            target: action.input?.target,
          },
        }));
      }
      return actionOk({
        target: action.input?.target ?? "self",
      }, "refresh");
    },
    callApi: async (action) => {
      const apiKey = action.input?.apiKey ?? action.input?.url;
      if (!apiKey) {
        return actionFail("callApi action requires apiKey or url", "callApi");
      }
      const method = (action.input?.method ?? "POST").toUpperCase();
      const body = action.input?.body;
      const requestBody = typeof body === "string" ? body : body === undefined ? undefined : JSON.stringify(body);
      const response = await requestApi<ApiResponse<JsonValue>>(apiKey, {
        method,
        body: requestBody,
      });
      if (response.success === false) {
        return actionFail(response.message || "Call API failed", "callApi");
      }
      return actionOk(response.data, "callApi");
    },
    openDialog: async (action) => {
      const dialogKey = action.input?.dialogKey;
      const payload = action.input?.payload;
      const title = action.input?.title || "Runtime Dialog";
      if (typeof payload === "string") {
        message.info(`${title}: ${payload}`);
      } else if (payload) {
        message.info(`${title}: ${JSON.stringify(payload)}`);
      } else {
        message.info(title);
      }
      return actionOk({
        dialogKey,
        payload,
      }, "openDialog");
    },
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
