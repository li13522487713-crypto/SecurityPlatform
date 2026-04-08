/**
 * 动作执行结果。
 */

export interface ActionResult {
  success: boolean;
  data?: unknown;
  error?: string;
}

export function actionOk(data?: unknown): ActionResult {
  return { success: true, data };
}

export function actionFail(error: string): ActionResult {
  return { success: false, error };
}
