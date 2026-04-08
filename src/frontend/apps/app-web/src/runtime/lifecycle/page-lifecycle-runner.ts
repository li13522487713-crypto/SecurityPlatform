/**
 * 页面生命周期钩子执行器。
 *
 * Phase 1 提供基础骨架，Phase 3 扩展完整实现。
 */

import { executeActions } from "../actions/action-executor";
import type { PageLifecycleHooks } from "./lifecycle-types";
import type { ActionResult } from "../actions/action-result";

export async function runLifecycleHook(
  hooks: PageLifecycleHooks | undefined,
  hookName: keyof PageLifecycleHooks,
): Promise<ActionResult[]> {
  if (!hooks) return [];
  const actions = hooks[hookName];
  if (!actions || actions.length === 0) return [];
  return executeActions(actions);
}
