import type { RuntimeAction } from "./action-types";

export interface ActionResult {
  actionId?: string | number;
  actionType?: RuntimeAction["type"];
  success: boolean;
  data?: unknown;
  message?: string;
  error?: string;
  errorCode?: string;
}

export interface ActionExecutionSummary {
  success: boolean;
  results: ActionResult[];
}

export function actionOk(data?: unknown, actionType?: RuntimeAction["type"]): ActionResult {
  return { success: true, data, actionType };
}

export function actionFail(error: string, actionType?: RuntimeAction["type"]): ActionResult {
  return { success: false, error, actionType };
}
