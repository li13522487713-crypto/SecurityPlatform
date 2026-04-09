export interface RuntimeContext {
  appKey: string;
  tenantId?: string;
  userId?: string;
}

let runtimeContext: RuntimeContext | null = null;

export function setRuntimeContext(context: RuntimeContext) {
  runtimeContext = context;
}

export function getRuntimeContext() {
  return runtimeContext;
}
