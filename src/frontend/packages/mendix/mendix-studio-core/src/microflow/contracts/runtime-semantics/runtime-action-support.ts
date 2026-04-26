import { microflowActionRegistryByKind, type MicroflowRegistryAvailability } from "@atlas/microflow/node-registry";
import type { MicroflowActionKind } from "@atlas/microflow/schema";
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
 * 将注册表能力映射为运行期支持级；P0 集合优先为 supported。
 * Validator 的 publish / testRun 策略以文档为准，本函数供 ExecutionPlan 与测试引用。
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

  const reg = microflowActionRegistryByKind.get(actionKind);
  if (!reg) {
    return {
      supportLevel: "unsupported",
      reason: "notImplemented",
      message: `Action kind "${actionKind}" is not in the default registry.`
    };
  }

  const av = reg.availability as MicroflowRegistryAvailability;
  if (av === "hidden") {
    return {
      supportLevel: "modeledOnly",
      reason: "modeledOnly",
      message: "内部/隐藏能力，运行期默认不执行。"
    };
  }
  if (av === "nanoflowOnlyDisabled") {
    return {
      supportLevel: "nanoflowOnly",
      reason: "nanoflowOnly",
      message: reg.availabilityReason ?? "Microflow 不支持此 Nanoflow 专用动作。"
    };
  }
  if (av === "requiresConnector") {
    return {
      supportLevel: "requiresConnector",
      reason: "requiresConnector",
      message: reg.availabilityReason ?? "需要 Connector。"
    };
  }
  if (av === "deprecated") {
    return {
      supportLevel: "deprecated",
      reason: "deprecated",
      message: reg.availabilityReason ?? "已弃用，运行期按产品策略可拒绝。"
    };
  }
  if (av === "beta") {
    return {
      supportLevel: "modeledOnly",
      reason: "modeledOnly",
      message: "Beta：默认 modeledOnly，直至后端显式实现。"
    };
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
