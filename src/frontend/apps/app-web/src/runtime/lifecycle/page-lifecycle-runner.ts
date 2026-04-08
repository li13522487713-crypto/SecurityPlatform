/**
 * 页面生命周期钩子执行器。
 */

import { executeActions } from "../actions/action-executor";
import type { PageLifecycleHooks } from "./lifecycle-types";
import type { ActionExecutionSummary } from "../actions/action-result";

export async function runLifecycleHook(
  hooks: PageLifecycleHooks | undefined,
  hookName: keyof PageLifecycleHooks,
): Promise<ActionExecutionSummary> {
  if (!hooks) return { success: true, results: [] };
  const actions = hooks[hookName];
  if (!actions || actions.length === 0) return { success: true, results: [] };
  return executeActions(actions);
}
