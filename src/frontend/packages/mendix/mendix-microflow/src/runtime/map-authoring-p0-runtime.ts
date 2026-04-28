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
