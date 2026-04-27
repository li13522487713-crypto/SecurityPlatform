import type { MicroflowActionKind } from "@atlas/microflow/schema/types";
import type {
  MicroflowRuntimeSupportLevel,
  MicroflowUnsupportedActionReason,
} from "./runtime-execution-plan";

/** P0 后端必须实现的动作类型（与 runtime-action-support-matrix.md 一致）。 */
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
  "logMessage"
]);

/**
 * 将动作类型映射为运行期支持级；P0 集合优先为 supported。
 * Validator 的 publish / testRun 策略以文档为准，本函数供 ExecutionPlan 与测试引用。
 * 注意：这里不导入 UI 节点注册表，避免纯契约测试加载 Semi 图标/CSS。
 */
export function resolveActionRuntimeSupportLevel(
  actionKind: MicroflowActionKind
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
    message: "P1/P2 动作：可建模，运行期默认 UnsupportedAction，除非后端已实现。"
  };
}

export function isP0ActionKind(kind: MicroflowActionKind): boolean {
  return MICROFLOW_P0_ACTION_KINDS.has(kind);
}
