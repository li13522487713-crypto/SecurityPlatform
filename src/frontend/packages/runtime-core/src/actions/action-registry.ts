import type { RuntimeAction } from "./action-types";
import type { ActionResult } from "./action-result";

export type RuntimeContextRunner = (context: unknown) => unknown;

export type ActionHandler = (
  action: RuntimeAction,
  context: unknown,
  adapter: RuntimeActionContext,
) => Promise<ActionResult>;

export interface RuntimeActionContext {
  getContext: () => unknown;
  getExpressionVariables?: () => Record<string, unknown>;
  evaluateExpression?: (expression: string, vars: Record<string, unknown>) => Promise<unknown>;
  navigate?: (action: Extract<RuntimeAction, { type: "navigate" }>) => Promise<ActionResult>;
  submitForm?: (action: Extract<RuntimeAction, { type: "submitForm" }>) => Promise<ActionResult>;
  callApi?: (action: Extract<RuntimeAction, { type: "callApi" }>) => Promise<ActionResult>;
  openDialog?: (action: Extract<RuntimeAction, { type: "openDialog" }>) => Promise<ActionResult>;
  refresh?: (action: Extract<RuntimeAction, { type: "refresh" }>) => Promise<ActionResult>;
  setGlobalVar?: (name: string, value: unknown) => void | Promise<void>;
}

const handlers = new Map<string, ActionHandler>();

export function registerActionHandler(type: string, handler: ActionHandler): void {
  handlers.set(type, handler);
}

export function unregisterActionHandler(type: string): void {
  handlers.delete(type);
}

export function getActionHandler(type: string): ActionHandler | undefined {
  return handlers.get(type);
}

export function hasActionHandler(type: string): boolean {
  return handlers.has(type);
}
