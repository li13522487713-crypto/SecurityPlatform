import type { MicroflowActionKind } from "../schema/types";

export type MicroflowRuntimeSupportLevel =
  | "supported"
  | "modeledOnly"
  | "unsupported"
  | "requiresConnector"
  | "nanoflowOnly"
  | "deprecated";

export type MicroflowUnsupportedActionReason =
  | "unsupported"
  | "modeledOnly"
  | "requiresConnector"
  | "deprecated"
  | "nanoflowOnly"
  | "notImplemented";

export const MICROFLOW_P0_ACTION_KINDS: ReadonlySet<MicroflowActionKind> = new Set<MicroflowActionKind>([
  "retrieve",
  "createObject",
  "changeMembers",
  "commit",
  "delete",
  "rollback",
  "createVariable",
  "changeVariable",
  "callMicroflow",
  "restCall",
  "logMessage",
]);

export function isP0ActionKind(kind: MicroflowActionKind): boolean {
  return MICROFLOW_P0_ACTION_KINDS.has(kind);
}

export function resolveActionRuntimeSupportLevel(
  actionKind: MicroflowActionKind,
): {
  supportLevel: MicroflowRuntimeSupportLevel;
  reason?: MicroflowUnsupportedActionReason;
  message: string;
} {
  if (MICROFLOW_P0_ACTION_KINDS.has(actionKind)) {
    return { supportLevel: "supported", message: "P0 supported action." };
  }
  return {
    supportLevel: "modeledOnly",
    reason: "modeledOnly",
    message: "P1/P2 动作：可建模，运行期默认 UnsupportedAction，除非后端已实现。",
  };
}
