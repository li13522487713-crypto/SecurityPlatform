import type {
  MicroflowAction,
  MicroflowCallMicroflowAction,
  MicroflowChangeMembersAction,
  MicroflowChangeVariableAction,
  MicroflowCommitAction,
  MicroflowCreateObjectAction,
  MicroflowCreateVariableAction,
  MicroflowDeleteAction,
  MicroflowLogMessageAction,
  MicroflowRetrieveAction,
  MicroflowRollbackAction,
  MicroflowRestCallAction,
} from "../types";

const P0 = new Set<MicroflowAction["kind"]>([
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

export function isP0ActionKind(kind: string): kind is MicroflowAction["kind"] {
  return P0.has(kind as MicroflowAction["kind"]);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === "object" && !Array.isArray(value);
}

function hasString(obj: Record<string, unknown>, key: string): boolean {
  return typeof obj[key] === "string" && String(obj[key]).trim().length > 0;
}

export function isMicroflowP0ActionStronglyTyped(action: MicroflowAction): action is
  | MicroflowRetrieveAction
  | MicroflowCreateObjectAction
  | MicroflowChangeMembersAction
  | MicroflowCommitAction
  | MicroflowDeleteAction
  | MicroflowRollbackAction
  | MicroflowCreateVariableAction
  | MicroflowChangeVariableAction
  | MicroflowCallMicroflowAction
  | MicroflowRestCallAction
  | MicroflowLogMessageAction {
  if (!isP0ActionKind(action.kind)) {
    return false;
  }
  switch (action.kind) {
    case "retrieve": {
      const a = action as MicroflowRetrieveAction;
      if (!hasString(a as unknown as Record<string, unknown>, "outputVariableName")) {
        return false;
      }
      if (!a.retrieveSource || typeof a.retrieveSource !== "object") {
        return false;
      }
      if (a.retrieveSource.kind === "database") {
        return hasString(a.retrieveSource as unknown as Record<string, unknown>, "entityQualifiedName");
      }
      if (a.retrieveSource.kind === "association") {
        return (
          hasString(a.retrieveSource as unknown as Record<string, unknown>, "startVariableName")
          && hasString(a.retrieveSource as unknown as Record<string, unknown>, "associationQualifiedName")
        );
      }
      return false;
    }
    case "createObject": {
      const a = action as MicroflowCreateObjectAction;
      return hasString(a as unknown as Record<string, unknown>, "entityQualifiedName")
        && hasString(a as unknown as Record<string, unknown>, "outputVariableName")
        && Array.isArray(a.memberChanges)
        && typeof a.commit === "object";
    }
    case "changeMembers": {
      const a = action as MicroflowChangeMembersAction;
      return hasString(a as unknown as Record<string, unknown>, "changeVariableName")
        && Array.isArray(a.memberChanges)
        && typeof a.commit === "object"
        && typeof a.validateObject === "boolean";
    }
    case "commit":
    case "delete":
    case "rollback": {
      const a = action as MicroflowCommitAction | MicroflowDeleteAction | MicroflowRollbackAction;
      return hasString(a as unknown as Record<string, unknown>, "objectOrListVariableName");
    }
    case "createVariable": {
      const a = action as MicroflowCreateVariableAction;
      return hasString(a as unknown as Record<string, unknown>, "variableName")
        && a.dataType != null
        && typeof a.dataType === "object"
        && (a.readonly === undefined || typeof a.readonly === "boolean");
    }
    case "changeVariable": {
      const a = action as MicroflowChangeVariableAction;
      return (
        hasString(a as unknown as Record<string, unknown>, "targetVariableName")
        && a.newValueExpression != null
        && typeof a.newValueExpression === "object"
      );
    }
    case "callMicroflow": {
      const a = action as MicroflowCallMicroflowAction;
      return hasString(a as unknown as Record<string, unknown>, "targetMicroflowId")
        && Array.isArray(a.parameterMappings)
        && a.returnValue != null
        && typeof a.returnValue === "object"
        && (a.callMode === "sync" || a.callMode === "asyncReserved");
    }
    case "restCall": {
      const a = action as MicroflowRestCallAction;
      return (
        a.request != null
        && typeof a.request === "object"
        && a.response != null
        && typeof a.request.urlExpression === "object"
        && typeof a.timeoutSeconds === "number"
      );
    }
    case "logMessage": {
      const a = action as MicroflowLogMessageAction;
      return (
        typeof a.level === "string"
        && hasString(a as unknown as Record<string, unknown>, "logNodeName")
        && a.template != null
        && typeof a.template === "object"
        && typeof (a.template as { text?: unknown }).text === "string"
        && Array.isArray((a.template as { arguments?: unknown }).arguments)
      );
    }
    default:
      return false;
  }
}

/**
 * 若 JSON 中误将 P0 存成松散对象（例如缺少 targetVariableName / memberChanges），则视为非强类型，供 Validator 使用。
 * GenericAction 的 kind 在类型层已排除 P0，此处不检测 Generic 分支。
 */
export function p0ActionLooksMalformed(action: MicroflowAction): boolean {
  if (!isP0ActionKind(action.kind)) {
    return false;
  }
  if ("legacyConfig" in action && isRecord((action as { legacyConfig?: unknown }).legacyConfig)) {
    return true;
  }
  return !isMicroflowP0ActionStronglyTyped(action);
}

/** 非 P0 kind，对应 P1/P2 或 generic modeledOnly 路径（不能当作 Runtime P0 执行）。 */
export function isGenericModeledOnlyActionKind(kind: string): boolean {
  return !isP0ActionKind(kind);
}
