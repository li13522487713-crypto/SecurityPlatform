/**
 * 动作处理器注册表。
 *
 * 每种 RuntimeAction.type 对应一个 ActionHandler。
 */

import type { RuntimeAction } from "./action-types";
import type { ActionResult } from "./action-result";
import type { RuntimeContext } from "../context/runtime-context-types";

export type ActionHandler = (
  action: RuntimeAction,
  context: RuntimeContext,
) => Promise<ActionResult>;

const handlers = new Map<string, ActionHandler>();

export function registerActionHandler(type: string, handler: ActionHandler): void {
  handlers.set(type, handler);
}

export function getActionHandler(type: string): ActionHandler | undefined {
  return handlers.get(type);
}

export function hasActionHandler(type: string): boolean {
  return handlers.has(type);
}
