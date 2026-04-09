export interface RuntimeAction {
  type: string;
  payload?: Record<string, unknown>;
}

export type RuntimeActionHandler = (action: RuntimeAction) => Promise<void>;

const handlers = new Map<string, RuntimeActionHandler>();

export function registerRuntimeAction(type: string, handler: RuntimeActionHandler) {
  handlers.set(type, handler);
}

export async function dispatchRuntimeAction(action: RuntimeAction) {
  const handler = handlers.get(action.type);
  if (!handler) return;
  await handler(action);
}
