import type {
  MicroflowAction,
  MicroflowDiscriminatedRuntimeP0ActionDto,
} from "../schema/types";

function recordOf(action: MicroflowAction): Record<string, unknown> {
  return action as unknown as Record<string, unknown>;
}

function dto(
  action: MicroflowAction,
  actionKind: string,
  config: Record<string, unknown>,
): MicroflowDiscriminatedRuntimeP0ActionDto {
  return {
    actionId: action.id,
    officialType: action.officialType,
    supportLevel: "supported",
    actionKind,
    errorHandlingType: action.errorHandlingType,
    config,
  } as unknown as MicroflowDiscriminatedRuntimeP0ActionDto;
}

/** Maps direct design-schema node action data to the runtime discriminated DTO. */
export function tryMapP0ActionToDiscriminatedDto(action: MicroflowAction): MicroflowDiscriminatedRuntimeP0ActionDto | null {
  const record = recordOf(action);
  switch (String(action.kind)) {
    case "retrieve":
      return dto(action, "retrieve", {
        outputVariableName: record.outputVariableName,
        retrieveSource: record.retrieveSource,
      });
    case "createObject":
      return dto(action, "createObject", {
        entityQualifiedName: record.entityQualifiedName,
        outputVariableName: record.outputVariableName,
        memberChanges: record.memberChanges,
        commit: record.commit,
      });
    case "changeMembers":
      return dto(action, "changeMembers", {
        changeVariableName: record.changeVariableName,
        memberChanges: record.memberChanges,
        commit: record.commit,
        validateObject: record.validateObject,
      });
    case "commit":
      return dto(action, "commit", {
        objectOrListVariableName: record.objectOrListVariableName,
        withEvents: record.withEvents,
        refreshInClient: record.refreshInClient,
      });
    case "delete":
      return dto(action, "delete", {
        objectOrListVariableName: record.objectOrListVariableName,
        withEvents: record.withEvents,
        deleteBehavior: record.deleteBehavior,
      });
    case "rollback":
      return dto(action, "rollback", {
        objectOrListVariableName: record.objectOrListVariableName,
        refreshInClient: record.refreshInClient,
      });
    case "cast":
      return dto(action, "cast", {
        sourceVariable: record.sourceVariable ?? record.sourceObjectVariableName,
        sourceObjectVariableName: record.sourceObjectVariableName ?? record.sourceVariable,
        targetEntity: record.targetEntity ?? record.targetEntityQualifiedName,
        targetEntityQualifiedName: record.targetEntityQualifiedName ?? record.targetEntity,
        outputVariable: record.outputVariable ?? record.outputVariableName,
        outputVariableName: record.outputVariableName ?? record.outputVariable,
        castMode: record.castMode,
      });
    case "createList":
      return dto(action, "createList", {
        outputListVariableName: record.outputListVariableName ?? record.outputVariableName ?? record.listVariableName,
        listVariableName: record.listVariableName,
        itemType: record.itemType,
        elementType: record.elementType,
        listType: record.listType,
        initialItemsExpression: record.initialItemsExpression,
      });
    case "changeList":
      return dto(action, "changeList", {
        targetListVariableName: record.targetListVariableName ?? record.targetVariableName ?? record.listVariableName,
        sourceListVariableName: record.sourceListVariableName,
        operation: record.operation,
        objectVariableName: record.objectVariableName,
        itemExpression: record.itemExpression,
        itemsExpression: record.itemsExpression,
        conditionExpression: record.conditionExpression,
        indexExpression: record.indexExpression,
      });
    case "aggregateList":
      return dto(action, "aggregateList", {
        sourceListVariableName: record.sourceListVariableName ?? record.listVariableName,
        listVariableName: record.listVariableName,
        aggregateFunction: record.aggregateFunction ?? record.aggregate ?? record.operation ?? "count",
        attributeQualifiedName: record.attributeQualifiedName,
        member: record.member,
        aggregateExpression: record.aggregateExpression,
        outputVariableName: record.outputVariableName ?? record.resultVariableName,
        resultVariableName: record.resultVariableName,
        resultType: record.resultType,
        emptyListBehavior: record.emptyListBehavior,
      });
    case "filterList":
      return dto(action, "filterList", {
        sourceListVariableName: record.sourceListVariableName ?? record.listVariableName,
        outputVariableName: record.outputVariableName ?? record.outputListVariableName,
        outputListVariableName: record.outputListVariableName ?? record.outputVariableName,
        itemVariableName: record.itemVariableName,
        conditionExpression: record.conditionExpression ?? record.filterExpression,
        filterExpression: record.filterExpression ?? record.conditionExpression,
        itemType: record.itemType,
      });
    case "sortList":
      return dto(action, "sortList", {
        sourceListVariableName: record.sourceListVariableName ?? record.listVariableName,
        outputVariableName: record.outputVariableName ?? record.outputListVariableName,
        outputListVariableName: record.outputListVariableName ?? record.outputVariableName,
        direction: record.direction,
        sortExpression: record.sortExpression,
        sortKeys: record.sortKeys,
      });
    case "listOperation":
      return dto(action, "listOperation", {
        leftListVariableName: record.leftListVariableName ?? record.sourceListVariableName ?? record.listVariableName,
        sourceListVariableName: record.sourceListVariableName ?? record.leftListVariableName ?? record.listVariableName,
        rightListVariableName: record.rightListVariableName ?? record.otherListVariableName,
        operation: record.operation,
        objectVariableName: record.objectVariableName,
        expression: record.expression,
        filterExpression: record.filterExpression,
        sortExpression: record.sortExpression,
        sortKeys: record.sortKeys,
        outputListVariableName: record.outputListVariableName ?? record.outputVariableName,
        outputVariableName: record.outputVariableName ?? record.outputListVariableName,
        outputElementType: record.outputElementType,
        limit: record.limit,
        offset: record.offset,
      });
    case "counter":
    case "gauge":
      return dto(action, String(action.kind), {
        metricName: record.metricName,
        valueExpression: record.valueExpression,
        tags: record.tags,
      });
    case "incrementCounter":
      return dto(action, "incrementCounter", {
        metricName: record.metricName,
        tags: record.tags,
      });
    case "createVariable":
      return dto(action, "createVariable", {
        variableName: record.variableName,
        dataType: record.dataType,
        initialValue: record.initialValue,
        readonly: record.readonly ?? false,
      });
    case "changeVariable":
      return dto(action, "changeVariable", {
        targetVariableName: record.targetVariableName,
        newValueExpression: record.newValueExpression,
      });
    case "callMicroflow":
      return dto(action, "callMicroflow", {
        targetMicroflowId: record.targetMicroflowId,
        targetMicroflowName: record.targetMicroflowName,
        targetMicroflowDisplayName: record.targetMicroflowDisplayName,
        targetMicroflowQualifiedName: record.targetMicroflowQualifiedName,
        targetModuleId: record.targetModuleId,
        targetVersion: record.targetVersion,
        targetSchemaId: record.targetSchemaId,
        parameterMappings: record.parameterMappings,
        returnValue: record.returnValue,
        callMode: record.callMode,
      });
    case "restCall":
      return dto(action, "restCall", {
        request: record.request,
        response: record.response,
        timeoutSeconds: record.timeoutSeconds,
      });
    case "logMessage":
      return dto(action, "logMessage", {
        level: record.level,
        logNodeName: record.logNodeName,
        template: record.template,
        includeContextVariables: record.includeContextVariables,
        includeTraceId: record.includeTraceId,
      });
    default:
      return null;
  }
}
