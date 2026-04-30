import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowAuthoringSchema,
  MicroflowDiscriminatedRuntimeP0ActionDto,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowRuntimeP0Block,
} from "../schema/types";
import { isMicroflowP0ActionStronglyTyped } from "../schema/authoring/p0-action-guards";

function flattenObjects(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object =>
    object.kind === "loopedActivity"
      ? [object, ...flattenObjects(object.objectCollection)]
      : [object]
  );
}

/** 将已校验的 P0 动作映射为运行时联合 DTO；无法映射时返回 null。 */
export function tryMapP0ActionToDiscriminatedDto(action: MicroflowAction): MicroflowDiscriminatedRuntimeP0ActionDto | null {
  if (!isMicroflowP0ActionStronglyTyped(action)) {
    return null;
  }
  const base = { actionId: action.id, officialType: action.officialType, supportLevel: "supported" as const };
  const eh = action.errorHandlingType;
  switch (action.kind) {
    case "retrieve":
      return {
        ...base,
        actionKind: "retrieve",
        errorHandlingType: eh,
        config: { outputVariableName: action.outputVariableName, retrieveSource: action.retrieveSource },
      };
    case "createObject":
      return {
        ...base,
        actionKind: "createObject",
        errorHandlingType: eh,
        config: {
          entityQualifiedName: action.entityQualifiedName,
          outputVariableName: action.outputVariableName,
          memberChanges: action.memberChanges,
          commit: action.commit,
        },
      };
    case "changeMembers":
      return {
        ...base,
        actionKind: "changeMembers",
        errorHandlingType: eh,
        config: {
          changeVariableName: action.changeVariableName,
          memberChanges: action.memberChanges,
          commit: action.commit,
          validateObject: action.validateObject,
        },
      };
    case "commit":
      return {
        ...base,
        actionKind: "commit",
        errorHandlingType: eh,
        config: {
          objectOrListVariableName: action.objectOrListVariableName,
          withEvents: action.withEvents,
          refreshInClient: action.refreshInClient,
        },
      };
    case "delete":
      return {
        ...base,
        actionKind: "delete",
        errorHandlingType: eh,
        config: {
          objectOrListVariableName: action.objectOrListVariableName,
          withEvents: action.withEvents,
          deleteBehavior: action.deleteBehavior,
        },
      };
    case "rollback":
      return {
        ...base,
        actionKind: "rollback",
        errorHandlingType: eh,
        config: { objectOrListVariableName: action.objectOrListVariableName, refreshInClient: action.refreshInClient },
      };
    case "createList":
      {
        const record = action as unknown as Record<string, unknown>;
      return {
        ...base,
        actionKind: "createList",
        errorHandlingType: eh,
        config: {
          outputListVariableName: String(record.outputListVariableName ?? record.outputVariableName ?? record.listVariableName ?? ""),
          listVariableName: action.listVariableName,
          itemType: action.itemType,
          elementType: action.elementType,
          listType: action.listType,
          initialItemsExpression: action.initialItemsExpression,
        },
      };
      }
    case "changeList":
      {
        const record = action as unknown as Record<string, unknown>;
      return {
        ...base,
        actionKind: "changeList",
        errorHandlingType: eh,
        config: {
          targetListVariableName: String(record.targetListVariableName ?? record.targetVariableName ?? record.listVariableName ?? ""),
          sourceListVariableName: action.sourceListVariableName,
          operation: action.operation,
          objectVariableName: action.objectVariableName,
          itemExpression: action.itemExpression,
          itemsExpression: action.itemsExpression,
          conditionExpression: action.conditionExpression,
          indexExpression: action.indexExpression,
        },
      };
      }
    case "aggregateList":
      {
        const record = action as unknown as Record<string, unknown>;
      return {
        ...base,
        actionKind: "aggregateList",
        errorHandlingType: eh,
        config: {
          sourceListVariableName: action.sourceListVariableName || action.listVariableName,
          listVariableName: action.listVariableName,
          aggregateFunction: String(record.aggregateFunction ?? record.aggregate ?? record.operation ?? "count"),
          attributeQualifiedName: action.attributeQualifiedName,
          member: action.member,
          aggregateExpression: action.aggregateExpression,
          outputVariableName: String(record.outputVariableName ?? record.resultVariableName ?? ""),
          resultVariableName: action.resultVariableName,
          resultType: action.resultType,
          emptyListBehavior: action.emptyListBehavior,
        },
      };
      }
    case "listOperation":
      {
        const record = action as unknown as Record<string, unknown>;
      return {
        ...base,
        actionKind: "listOperation",
        errorHandlingType: eh,
        config: {
          leftListVariableName: String(record.leftListVariableName ?? record.sourceListVariableName ?? record.listVariableName ?? ""),
          sourceListVariableName: String(record.sourceListVariableName ?? record.leftListVariableName ?? record.listVariableName ?? ""),
          rightListVariableName: typeof record.rightListVariableName === "string"
            ? record.rightListVariableName
            : typeof record.otherListVariableName === "string"
              ? record.otherListVariableName
              : undefined,
          operation: action.operation,
          objectVariableName: action.objectVariableName,
          expression: action.expression,
          filterExpression: action.filterExpression,
          sortExpression: action.sortExpression,
          sortKeys: (record.sortKeys as Array<Record<string, unknown>> | undefined),
          outputListVariableName: String(record.outputListVariableName ?? record.outputVariableName ?? ""),
          outputVariableName: typeof record.outputVariableName === "string"
            ? record.outputVariableName
            : typeof record.outputListVariableName === "string"
              ? record.outputListVariableName
              : undefined,
          outputElementType: record.outputElementType as typeof action.outputElementType,
          limit: typeof record.limit === "number" ? record.limit : undefined,
          offset: typeof record.offset === "number" ? record.offset : undefined,
        },
      };
      }
    case "createVariable":
      return {
        ...base,
        actionKind: "createVariable",
        errorHandlingType: eh,
        config: {
          variableName: action.variableName,
          dataType: action.dataType,
          initialValue: action.initialValue,
          readonly: action.readonly ?? false,
        },
      };
    case "changeVariable":
      return {
        ...base,
        actionKind: "changeVariable",
        errorHandlingType: eh,
        config: { targetVariableName: action.targetVariableName, newValueExpression: action.newValueExpression },
      };
    case "callMicroflow":
      return {
        ...base,
        actionKind: "callMicroflow",
        errorHandlingType: eh,
        config: {
          targetMicroflowId: action.targetMicroflowId,
          targetMicroflowName: action.targetMicroflowName,
          targetMicroflowDisplayName: action.targetMicroflowDisplayName,
          targetMicroflowQualifiedName: action.targetMicroflowQualifiedName,
          targetModuleId: action.targetModuleId,
          targetVersion: action.targetVersion,
          targetSchemaId: action.targetSchemaId,
          parameterMappings: action.parameterMappings,
          returnValue: action.returnValue,
          callMode: action.callMode,
        },
      };
    case "restCall":
      return {
        ...base,
        actionKind: "restCall",
        errorHandlingType: eh,
        config: { request: action.request, response: action.response, timeoutSeconds: action.timeoutSeconds },
      };
    case "logMessage":
      return {
        ...base,
        actionKind: "logMessage",
        errorHandlingType: eh,
        config: {
          level: action.level,
          logNodeName: action.logNodeName,
          template: action.template,
          includeContextVariables: action.includeContextVariables,
          includeTraceId: action.includeTraceId,
        },
      };
    default:
      return null;
  }
}

/**
 * 自 Authoring 图收集 P0 运行时 DTO；强类型校验失败时产生 error 块而非抛错。
 */
export function mapAuthoringP0ToRuntimeBlocks(schema: MicroflowAuthoringSchema): MicroflowRuntimeP0Block[] {
  const out: MicroflowRuntimeP0Block[] = [];
  for (const object of flattenObjects(schema.objectCollection)) {
    if (object.kind !== "actionActivity") {
      continue;
    }
    const act = (object as MicroflowActionActivity).action;
    const mapped = tryMapP0ActionToDiscriminatedDto(act);
    if (mapped) {
      out.push({ objectId: object.id, supportLevel: "supported", action: mapped });
    } else if (
      act.kind === "retrieve"
      || act.kind === "createObject"
      || act.kind === "changeMembers"
      || act.kind === "commit"
      || act.kind === "delete"
      || act.kind === "rollback"
      || act.kind === "createList"
      || act.kind === "changeList"
      || act.kind === "aggregateList"
      || act.kind === "listOperation"
      || act.kind === "createVariable"
      || act.kind === "changeVariable"
      || act.kind === "callMicroflow"
      || act.kind === "restCall"
      || act.kind === "logMessage"
    ) {
      out.push({
        objectId: object.id,
        supportLevel: "error",
        code: "MF_P0_MALFORMED",
        actionKind: act.kind,
        message: "P0 action failed strong-typing mapping; see validator MF_ACTION_P0_MUST_BE_STRONGLY_TYPED.",
      });
    }
  }
  return out;
}
