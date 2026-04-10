/**
 * Phase 3 内置动作处理器注册。
 *
 * 将 runWorkflow / runApproval / runAgent 等动作类型
 * 注册到 ActionRegistry。
 */

import type { ApiResponse } from "@atlas/shared-core";
import { requestApi, resolveAppHostPrefix, isDirectRuntimeMode } from "@/services/api-core";
import { registerActionHandler } from "./action-registry";
import { actionOk, actionFail } from "./action-result";
import type { RuntimeAction } from "./action-types";
import type { RuntimeContext } from "../context/runtime-context-types";

function resolvePrefix(appKey: string): string {
  return isDirectRuntimeMode() ? "" : resolveAppHostPrefix(appKey);
}

export function registerBuiltinHandlers(): void {
  registerActionHandler("runWorkflow", handleRunWorkflow);
  registerActionHandler("runApproval", handleRunApproval);
  registerActionHandler("runAgent", handleRunAgent);
  registerActionHandler("runFlow", handleRunFlow);
}

async function handleRunWorkflow(
  action: RuntimeAction,
  context: unknown,
) {
  if (action.type !== "runWorkflow") return actionFail("Invalid action type", "runWorkflow");
  const runtimeContext = context as RuntimeContext;

  const input = action.input;
  const prefix = resolvePrefix(runtimeContext.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/workflows/${encodeURIComponent(input?.workflowKey ?? "")}/execute`,
      {
        method: "POST",
        body: JSON.stringify({
          input: input?.input ?? {},
          executionId: runtimeContext.env.runtimeExecutionId,
        }),
      },
    );
    return actionOk(resp.data, "runWorkflow");
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Workflow execution failed", "runWorkflow");
  }
}

async function handleRunApproval(
  action: RuntimeAction,
  context: unknown,
) {
  if (action.type !== "runApproval") return actionFail("Invalid action type", "runApproval");
  const runtimeContext = context as RuntimeContext;

  const input = action.input;
  const prefix = resolvePrefix(runtimeContext.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/approval/instances`,
      {
        method: "POST",
        body: JSON.stringify({
          ...(typeof input === "object" ? input : {}),
          executionId: runtimeContext.env.runtimeExecutionId,
        }),
      },
    );
    return actionOk(resp.data, "runApproval");
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Approval submission failed", "runApproval");
  }
}

async function handleRunAgent(
  action: RuntimeAction,
  context: unknown,
) {
  if (action.type !== "runAgent") return actionFail("Invalid action type", "runAgent");
  const runtimeContext = context as RuntimeContext;

  const input = action.input;
  const prefix = resolvePrefix(runtimeContext.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/agents/${encodeURIComponent(input?.agentKey ?? "")}/invoke`,
      {
        method: "POST",
        body: JSON.stringify({
          input: input?.input ?? {},
          context: {
            executionId: runtimeContext.env.runtimeExecutionId,
            pageKey: runtimeContext.page.pageKey,
            userId: runtimeContext.user.id,
          },
        }),
      },
    );
    return actionOk(resp.data, "runAgent");
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Agent invocation failed", "runAgent");
  }
}

async function handleRunFlow(
  action: RuntimeAction,
  context: unknown,
) {
  if (action.type !== "runFlow") return actionFail("Invalid action type", "runFlow");
  const runtimeContext = context as RuntimeContext;

  const input = action.input;
  const prefix = resolvePrefix(runtimeContext.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/flows/${encodeURIComponent(input?.flowKey ?? "")}/execute`,
      {
        method: "POST",
        body: JSON.stringify({
          input: input?.input ?? {},
          executionId: runtimeContext.env.runtimeExecutionId,
        }),
      },
    );
    return actionOk(resp.data, "runFlow");
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Flow execution failed", "runFlow");
  }
}
