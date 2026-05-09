import type {
  MicroflowAggregateListAction,
  MicroflowAction,
  MicroflowCallMicroflowAction,
  MicroflowChangeMembersAction,
  MicroflowChangeListAction,
  MicroflowChangeVariableAction,
  MicroflowCommitAction,
  MicroflowCreateListAction,
  MicroflowCreateObjectAction,
  MicroflowCreateVariableAction,
  MicroflowDeleteAction,
  MicroflowListOperationAction,
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
  "createList",
  "changeList",
  "aggregateList",
  "listOperation",
  "counter",
  "incrementCounter",
  "gauge",
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
  | MicroflowCreateListAction
  | MicroflowChangeListAction
  | MicroflowAggregateListAction
  | MicroflowListOperationAction
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
    case "createList": {
      const a = action as MicroflowCreateListAction;
      const record = a as unknown as Record<string, unknown>;
      return (hasString(record, "outputListVariableName") || hasString(record, "outputVariableName") || hasString(record, "listVariableName"))
        && a.itemType != null;
    }
    case "changeList": {
      const a = action as MicroflowChangeListAction;
      const record = a as unknown as Record<string, unknown>;
      if (!(hasString(record, "targetListVariableName") || hasString(record, "targetVariableName") || hasString(record, "listVariableName"))) {
        return false;
      }

      const operation = typeof record.operation === "string" ? String(record.operation) : "";
      if (!operation) {
        return false;
      }

      if (operation === "clear") {
        return true;
      }

      if (operation === "removeWhere") {
        return a.conditionExpression != null && typeof a.conditionExpression === "object";
      }

      if (operation === "addAll" || operation === "addRange" || operation === "removeAll") {
        return (a.itemsExpression != null && typeof a.itemsExpression === "object") || hasString(record, "sourceListVariableName");
      }

      if (operation === "set") {
        return ((a.indexExpression != null && typeof a.indexExpression === "object")
          && (a.itemExpression != null && typeof a.itemExpression === "object"))
          || (a.itemsExpression != null && typeof a.itemsExpression === "object")
          || hasString(record, "sourceListVariableName");
      }

      return (a.itemExpression != null && typeof a.itemExpression === "object") || hasString(record, "objectVariableName");
    }
    case "aggregateList": {
      const a = action as MicroflowAggregateListAction;
      const record = a as unknown as Record<string, unknown>;
      if (!(hasString(record, "sourceListVariableName") || hasString(record, "listVariableName"))) {
        return false;
      }
      if (!(hasString(record, "outputVariableName") || hasString(record, "resultVariableName"))) {
        return false;
      }

      const aggregateFunction = typeof record.aggregateFunction === "string"
        ? String(record.aggregateFunction)
        : typeof record.aggregate === "string"
          ? String(record.aggregate)
          : typeof record.operation === "string"
            ? String(record.operation)
            : "count";
      if (aggregateFunction === "count") {
        return true;
      }

      return hasString(record, "attributeQualifiedName")
        || hasString(record, "member")
        || (a.aggregateExpression != null && typeof a.aggregateExpression === "object");
    }
    case "listOperation": {
      const a = action as MicroflowListOperationAction;
      const record = a as unknown as Record<string, unknown>;
      if (!(hasString(record, "leftListVariableName") || hasString(record, "sourceListVariableName") || hasString(record, "listVariableName"))) {
        return false;
      }
      if (!(hasString(record, "outputListVariableName") || hasString(record, "outputVariableName") || hasString(record, "resultVariableName"))) {
        return false;
      }

      const operation = typeof record.operation === "string" ? String(record.operation) : "";
      if (!operation) {
        return false;
      }

      if (operation === "filter") {
        return (a.filterExpression != null && typeof a.filterExpression === "object")
          || (a.expression != null && typeof a.expression === "object");
      }

      if (operation === "sort") {
        return Array.isArray((record as { sortKeys?: unknown[] }).sortKeys) || hasString(record, "sortExpression");
      }

      if (operation === "map") {
        return a.expression != null && typeof a.expression === "object";
      }

      if (operation === "take" || operation === "skip") {
        return typeof record.limit === "number" || typeof record.offset === "number";
      }

      return true;
    }
    case "counter":
    case "gauge": {
      const record = action as unknown as Record<string, unknown>;
      return hasString(record, "metricName")
        && record.valueExpression != null
        && typeof record.valueExpression === "object";
    }
    case "incrementCounter": {
      const record = action as unknown as Record<string, unknown>;
      return hasString(record, "metricName");
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
  if ("sourceConfig" in action && isRecord((action as { sourceConfig?: unknown }).sourceConfig)) {
    return true;
  }
  return !isMicroflowP0ActionStronglyTyped(action);
}

/** 非 P0 kind，对应 P1/P2 或 generic modeledOnly 路径（不能当作 Runtime P0 执行）。 */
export function isGenericModeledOnlyActionKind(kind: string): boolean {
  return !isP0ActionKind(kind);
}
