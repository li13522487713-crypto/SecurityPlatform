export type RuntimeLifecyclePhase = "init" | "ready" | "destroyed";

let currentPhase: RuntimeLifecyclePhase = "init";

export function setRuntimeLifecyclePhase(phase: RuntimeLifecyclePhase) {
  currentPhase = phase;
}

export function getRuntimeLifecyclePhase() {
  return currentPhase;
}
