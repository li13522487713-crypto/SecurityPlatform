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
  context: RuntimeContext,
) {
  if (action.type !== "runWorkflow") return actionFail("Invalid action type");

  const prefix = resolvePrefix(context.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/workflows/${encodeURIComponent(action.workflowKey)}/execute`,
      {
        method: "POST",
        body: JSON.stringify({
          input: action.input ?? {},
          executionId: context.env.runtimeExecutionId,
        }),
      },
    );
    return actionOk(resp.data);
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Workflow execution failed");
  }
}

async function handleRunApproval(
  action: RuntimeAction,
  context: RuntimeContext,
) {
  if (action.type !== "runWorkflow") return actionFail("Invalid action type");

  const prefix = resolvePrefix(context.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/approval/instances`,
      {
        method: "POST",
        body: JSON.stringify({
          ...(typeof action.input === "object" ? action.input : {}),
          executionId: context.env.runtimeExecutionId,
        }),
      },
    );
    return actionOk(resp.data);
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Approval submission failed");
  }
}

async function handleRunAgent(
  action: RuntimeAction,
  context: RuntimeContext,
) {
  if (action.type !== "runAgent") return actionFail("Invalid action type");

  const prefix = resolvePrefix(context.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/agents/${encodeURIComponent(action.agentKey)}/invoke`,
      {
        method: "POST",
        body: JSON.stringify({
          input: action.input ?? {},
          context: {
            executionId: context.env.runtimeExecutionId,
            pageKey: context.page.pageKey,
            userId: context.user.id,
          },
        }),
      },
    );
    return actionOk(resp.data);
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Agent invocation failed");
  }
}

async function handleRunFlow(
  action: RuntimeAction,
  context: RuntimeContext,
) {
  if (action.type !== "runFlow") return actionFail("Invalid action type");

  const prefix = resolvePrefix(context.app.appKey);
  try {
    const resp = await requestApi<ApiResponse<unknown>>(
      `${prefix}/api/app/flows/${encodeURIComponent(action.flowKey)}/execute`,
      {
        method: "POST",
        body: JSON.stringify({
          input: action.input ?? {},
          executionId: context.env.runtimeExecutionId,
        }),
      },
    );
    return actionOk(resp.data);
  } catch (error) {
    return actionFail(error instanceof Error ? error.message : "Flow execution failed");
  }
}
