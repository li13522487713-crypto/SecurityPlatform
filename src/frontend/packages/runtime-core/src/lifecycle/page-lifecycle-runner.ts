import { executeActions } from "../actions/action-executor";
import type { PageLifecycleHooks } from "./lifecycle-types";
import type { ActionExecutionSummary } from "../actions/action-result";
import type { RuntimeActionContext } from "../actions/action-registry";

export async function runLifecycleHook(
  hooks: PageLifecycleHooks | undefined,
  hookName: keyof PageLifecycleHooks,
  adapter?: RuntimeActionContext,
): Promise<ActionExecutionSummary> {
  if (!hooks) return { success: true, results: [] };
  const actions = hooks[hookName];
  if (!actions || actions.length === 0) return { success: true, results: [] };
  return executeActions(actions, adapter);
}
