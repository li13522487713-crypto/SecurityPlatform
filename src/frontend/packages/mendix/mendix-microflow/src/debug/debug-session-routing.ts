import type { MicroflowDebugSessionDto } from "./step-debug-api";

function normalizeMicroflowId(value: string | undefined): string | undefined {
  const normalized = String(value ?? "").trim();
  return normalized ? normalized : undefined;
}

export function collectDebugSessionMicroflowIds(
  session: MicroflowDebugSessionDto | undefined,
  fallbackMicroflowId?: string,
): string[] {
  const ids = new Set<string>();
  const add = (value: string | undefined) => {
    const normalized = normalizeMicroflowId(value);
    if (normalized) {
      ids.add(normalized);
    }
  };
  add(fallbackMicroflowId);
  add(session?.microflowId);
  for (const frame of session?.callStack ?? []) {
    add(frame.microflowId);
  }
  return [...ids];
}

export function resolveDeepestDebugMicroflowId(
  session: MicroflowDebugSessionDto | undefined,
  fallbackMicroflowId?: string,
): string | undefined {
  const stack = session?.callStack ?? [];
  for (let index = stack.length - 1; index >= 0; index -= 1) {
    const resolved = normalizeMicroflowId(stack[index]?.microflowId);
    if (resolved) {
      return resolved;
    }
  }
  return normalizeMicroflowId(session?.microflowId) ?? normalizeMicroflowId(fallbackMicroflowId);
}
