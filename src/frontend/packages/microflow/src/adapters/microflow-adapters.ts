import type {
  MendixCompatMicroflow,
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowActivityCategory,
  MicroflowActivityType,
  MicroflowAnnotation,
  MicroflowAnnotationFlow,
  MicroflowAuthoringSchema,
  MicroflowBreakEvent,
  MicroflowCaseValue,
  MicroflowContinueEvent,
  MicroflowDataType,
  MicroflowEdge,
  MicroflowEdgeKind,
  MicroflowEditorGraph,
  MicroflowEditorGraphPatch,
  MicroflowEndEvent,
  MicroflowErrorEvent,
  MicroflowExclusiveMerge,
  MicroflowExclusiveSplit,
  MicroflowExpression,
  MicroflowFlow,
  MicroflowInheritanceSplit,
  MicroflowLine,
  MicroflowLegacyGraphSchema,
  MicroflowLoopedActivity,
  MicroflowNode,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowObjectKind,
  MicroflowParameterObject,
  MicroflowPoint,
  MicroflowPort,
  MicroflowRuntimeDto,
  MicroflowSchema,
  MicroflowSequenceFlow,
  MicroflowSize,
  MicroflowStartEvent,
  MicroflowTypeRef,
  MicroflowVariable,
  MicroflowVariableIndex
} from "../schema/types";

const defaultLineStyle: MicroflowLine["style"] = {
  strokeType: "solid",
  strokeWidth: 2,
  arrow: "target"
};

export function emptyVariableIndex(): MicroflowVariableIndex {
  return {
    parameters: {},
    localVariables: {},
    objectOutputs: {},
    listOutputs: {},
    loopVariables: {},
    errorVariables: {},
    systemVariables: {
      $currentUser: {
        name: "$currentUser",
        dataType: { kind: "object", entityQualifiedName: "System.User" },
        source: { kind: "system", name: "$currentUser" },
        scope: { collectionId: "root" },
        readonly: true
      }
    }
  };
}

function buildVariableIndex(parameters: MicroflowAuthoringSchema["parameters"], collection: MicroflowObjectCollection, flows: MicroflowFlow[]): MicroflowVariableIndex {
  const index = emptyVariableIndex();
  for (const parameter of parameters) {
    index.parameters[parameter.name] = {
      name: parameter.name,
      dataType: parameter.dataType ?? toMicroflowDataType(parameter.type),
      source: { kind: "parameter", parameterId: parameter.id },
      scope: { collectionId: collection.id },
      readonly: true
    };
  }
  for (const object of flattenObjectCollection(collection)) {
    if (object.kind === "loopedActivity" && object.loopSource.kind === "iterableList") {
      index.loopVariables[object.loopSource.iteratorVariableName] = {
        name: object.loopSource.iteratorVariableName,
        dataType: { kind: "unknown", reason: object.loopSource.listVariableName },
        source: { kind: "loopIterator", loopObjectId: object.id },
        scope: { collectionId: object.objectCollection.id, loopObjectId: object.id },
        readonly: true
      };
      index.systemVariables.$currentIndex = {
        name: "$currentIndex",
        dataType: { kind: "integer" },
        source: { kind: "system", name: "$currentIndex" },
        scope: { collectionId: object.objectCollection.id, loopObjectId: object.id },
        readonly: true
      };
    }
    if (object.kind === "actionActivity") {
      const action = object.action;
      if (action.kind === "retrieve") {
        const outputType = action.retrieveSource.kind === "database" && action.retrieveSource.range.kind !== "first"
          ? { kind: "list" as const, itemType: action.retrieveSource.entityQualifiedName ? { kind: "object" as const, entityQualifiedName: action.retrieveSource.entityQualifiedName } : { kind: "unknown" as const, reason: "retrieve entity missing" } }
          : action.retrieveSource.kind === "database" && action.retrieveSource.entityQualifiedName
            ? { kind: "object" as const, entityQualifiedName: action.retrieveSource.entityQualifiedName }
            : { kind: "unknown" as const, reason: "association retrieve" };
        const bucket = outputType.kind === "list" ? index.listOutputs : index.objectOutputs;
        bucket[action.outputVariableName] = {
          name: action.outputVariableName,
          dataType: outputType,
          source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
          scope: { collectionId: collection.id, startObjectId: object.id },
          readonly: false
        };
      }
      if (action.kind === "createObject") {
        index.objectOutputs[action.outputVariableName] = {
          name: action.outputVariableName,
          dataType: { kind: "object", entityQualifiedName: action.entityQualifiedName },
          source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
          scope: { collectionId: collection.id, startObjectId: object.id },
          readonly: false
        };
      }
      if (action.kind === "callMicroflow" && action.returnValue.storeResult && action.returnValue.outputVariableName) {
        index.localVariables[action.returnValue.outputVariableName] = {
          name: action.returnValue.outputVariableName,
          dataType: action.returnValue.dataType ?? { kind: "unknown", reason: "microflow return" },
          source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
          scope: { collectionId: collection.id, startObjectId: object.id },
          readonly: false
        };
      }
    }
  }
  for (const flow of flows.filter((item): item is MicroflowSequenceFlow => item.kind === "sequence" && item.isErrorHandler)) {
    index.errorVariables.$latestError = {
      name: "$latestError",
      dataType: { kind: "object", entityQualifiedName: "System.Error" },
      source: { kind: "errorContext", flowId: flow.id },
      scope: { collectionId: collection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
      readonly: true
    };
    index.errorVariables.$latestHttpResponse = {
      name: "$latestHttpResponse",
      dataType: { kind: "object", entityQualifiedName: "System.HttpResponse" },
      source: { kind: "errorContext", flowId: flow.id },
      scope: { collectionId: collection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
      readonly: true
    };
    index.errorVariables.$latestSoapFault = {
      name: "$latestSoapFault",
      dataType: { kind: "object", entityQualifiedName: "System.SoapFault" },
      source: { kind: "errorContext", flowId: flow.id },
      scope: { collectionId: collection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
      readonly: true
    };
  }
  return index;
}

export function toMicroflowDataType(type?: MicroflowTypeRef): MicroflowDataType {
  if (!type) {
    return { kind: "unknown", reason: "missing legacy type" };
  }
  if (type.kind === "void") {
    return { kind: "void" };
  }
  if (type.kind === "entity" || type.kind === "object") {
    return { kind: "object", entityQualifiedName: type.entity ?? type.name };
  }
  if (type.kind === "list") {
    return { kind: "list", itemType: toMicroflowDataType(type.itemType) };
  }
  const normalized = type.name.toLowerCase();
  if (normalized === "boolean") {
    return { kind: "boolean" };
  }
  if (normalized === "integer") {
    return { kind: "integer" };
  }
  if (normalized === "long") {
    return { kind: "long" };
  }
  if (normalized === "decimal") {
    return { kind: "decimal" };
  }
  if (normalized === "datetime") {
    return { kind: "dateTime" };
  }
  if (normalized === "string") {
    return { kind: "string" };
  }
  return { kind: "unknown", reason: type.name };
}

export function toLegacyTypeRef(type: MicroflowDataType): MicroflowTypeRef {
  if (type.kind === "void") {
    return { kind: "void", name: "Void" };
  }
  if (type.kind === "object") {
    return { kind: "entity", name: type.entityQualifiedName, entity: type.entityQualifiedName };
  }
  if (type.kind === "list") {
    return { kind: "list", name: "List", itemType: toLegacyTypeRef(type.itemType) };
  }
  if (type.kind === "enumeration") {
    return { kind: "primitive", name: type.enumerationQualifiedName };
  }
  const names: Record<string, string> = {
    boolean: "Boolean",
    integer: "Integer",
    long: "Long",
    decimal: "Decimal",
    string: "String",
    dateTime: "DateTime",
    binary: "Binary",
    json: "Json",
    fileDocument: "System.FileDocument",
    unknown: "Unknown"
  };
  return { kind: type.kind === "unknown" ? "unknown" : "primitive", name: names[type.kind] ?? type.kind };
}

function expression(raw: string, inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    id: `expr-${Math.abs(hash(raw))}`,
    language: "mendix",
    text: raw,
    raw,
    inferredType,
    references: {
      variables: [...raw.matchAll(/\$[A-Za-z_][\w]*/g)].map(match => match[0]),
      entities: [],
      attributes: [],
      associations: [],
      enumerations: [],
      functions: []
    },
    diagnostics: []
  };
}

function hash(value: string): number {
  return [...value].reduce((total, char) => ((total << 5) - total + char.charCodeAt(0)) | 0, 0);
}

function defaultLine(points: MicroflowPoint[] = []): MicroflowLine {
  return {
    kind: "orthogonal",
    points,
    routing: {
      mode: "auto",
      bendPoints: []
    },
    style: defaultLineStyle
  };
}

function sizeFromNode(node: MicroflowNode): MicroflowSize {
  return {
    width: node.render.width ?? 160,
    height: node.render.height ?? 72
  };
}

function objectBase(node: MicroflowNode, kind: MicroflowObjectKind, officialType: string) {
  return {
    id: node.id,
    stableId: node.id,
    kind,
    officialType,
    caption: node.title,
    documentation: node.documentation?.summary,
    relativeMiddlePoint: node.position,
    size: sizeFromNode(node),
    disabled: node.enabled === false,
    editor: {
      selected: false,
      collapsed: false,
      iconKey: node.render.iconKey
    }
  };
}

function actionCategory(activityType: MicroflowActivityType): MicroflowActivityCategory {
  if (activityType.startsWith("object")) {
    return "object";
  }
  if (activityType.startsWith("list")) {
    return "list";
  }
  if (activityType.startsWith("variable")) {
    return "variable";
  }
  if (activityType.startsWith("call") && !["callRest", "callWebService", "callExternalAction"].includes(activityType)) {
    return "call";
  }
  if (["showPage", "closePage", "downloadFile", "showHomePage", "showMessage", "validationFeedback", "synchronize"].includes(activityType)) {
    return "client";
  }
  if (["callRest", "callWebService", "callExternalAction", "importWithMapping", "exportWithMapping", "queryExternalDatabase", "sendRestRequestBeta"].includes(activityType)) {
    return "integration";
  }
  if (activityType === "logMessage") {
    return "logging";
  }
  if (activityType === "generateDocument") {
    return "documentGeneration";
  }
  if (["counter", "incrementCounter", "gauge"].includes(activityType)) {
    return "metrics";
  }
  if (activityType === "callMlModel") {
    return "mlKit";
  }
  if (activityType.includes("Workflow") || activityType.includes("UserTask") || activityType.includes("JumpTo")) {
    return "workflow";
  }
  return "externalObject";
}

function makeAction(node: Extract<MicroflowNode, { type: "activity" }>): MicroflowAction {
  const config = node.config;
  const category = actionCategory(config.activityType);
  const base = {
    id: `${node.id}-action`,
    errorHandlingType: config.errorHandling?.mode ?? "rollback",
    documentation: node.documentation?.summary,
    editor: {
      category,
      iconKey: config.activityType,
      availability: node.availability ?? "supported",
      availabilityReason: node.availabilityReason
    }
  };
  if (config.activityType === "objectRetrieve") {
    return {
      ...base,
      kind: "retrieve",
      officialType: "Microflows$RetrieveAction",
      outputVariableName: config.resultVariableName ?? config.objectVariableName ?? config.listVariableName ?? "retrievedObject",
      retrieveSource: config.retrieveMode === "association" || config.association
        ? {
            kind: "association",
            officialType: "Microflows$AssociationRetrieveSource",
            associationQualifiedName: config.association ?? null,
            startVariableName: config.objectVariableName ?? "sourceObject"
          }
        : {
            kind: "database",
            officialType: "Microflows$DatabaseRetrieveSource",
            entityQualifiedName: config.entity ?? null,
            xPathConstraint: config.valueExpression ?? null,
            sortItemList: {
              items: (config.sortRules ?? []).map(item => ({ attributeQualifiedName: item.attribute, direction: item.direction }))
            },
            range: config.range === "first"
              ? { kind: "first", officialType: "Microflows$ConstantRange", value: "first" }
              : config.range === "limit"
                ? { kind: "custom", officialType: "Microflows$CustomRange", limitExpression: expression(String(config.limit ?? 10)) }
                : { kind: "all", officialType: "Microflows$ConstantRange", value: "all" }
          }
    };
  }
  if (config.activityType === "objectCreate") {
    return {
      ...base,
      kind: "createObject",
      officialType: "Microflows$CreateObjectAction",
      entityQualifiedName: config.entity ?? "",
      outputVariableName: config.objectVariableName ?? "newObject",
      memberChanges: [],
      commit: {
        enabled: Boolean(config.commitImmediately),
        withEvents: Boolean(config.withEvents),
        refreshInClient: Boolean(config.refreshClient)
      }
    };
  }
  if (config.activityType === "objectChange") {
    return {
      ...base,
      kind: "changeMembers",
      officialType: "Microflows$ChangeMembersAction",
      changeVariableName: config.objectVariableName ?? "",
      memberChanges: (config.assignments ?? []).map(item => ({
        id: item.id,
        memberQualifiedName: item.attribute,
        memberKind: "attribute",
        valueExpression: item.expression,
        assignmentKind: "set"
      })),
      commit: {
        enabled: Boolean(config.commitImmediately),
        withEvents: Boolean(config.withEvents),
        refreshInClient: Boolean(config.refreshClient)
      },
      validateObject: Boolean(config.validateObject)
    };
  }
  if (config.activityType === "objectCommit") {
    return {
      ...base,
      kind: "commit",
      officialType: "Microflows$CommitAction",
      objectOrListVariableName: config.objectVariableName ?? config.listVariableName ?? "",
      withEvents: Boolean(config.withEvents),
      refreshInClient: Boolean(config.refreshClient)
    };
  }
  if (config.activityType === "objectDelete") {
    return {
      ...base,
      kind: "delete",
      officialType: "Microflows$DeleteAction",
      objectOrListVariableName: config.objectVariableName ?? config.listVariableName ?? "",
      withEvents: Boolean(config.withEvents),
      deleteBehavior: config.refreshClient ? "deleteAndRefreshClient" : "deleteOnly"
    };
  }
  if (config.activityType === "objectRollback") {
    return {
      ...base,
      kind: "rollback",
      officialType: "Microflows$RollbackAction",
      objectOrListVariableName: config.objectVariableName ?? config.listVariableName ?? "",
      refreshInClient: Boolean(config.refreshClient)
    };
  }
  if (config.activityType === "callMicroflow") {
    return {
      ...base,
      kind: "callMicroflow",
      officialType: "Microflows$MicroflowCallAction",
      targetMicroflowId: config.targetMicroflowId ?? "",
      parameterMappings: (config.parameterMappings ?? []).map(item => ({
        parameterName: item.parameterName,
        parameterType: { kind: "unknown", reason: "legacy mapping" },
        argumentExpression: item.expression
      })),
      returnValue: {
        storeResult: Boolean(config.resultVariableName),
        outputVariableName: config.resultVariableName,
        dataType: config.variableType ? toMicroflowDataType(config.variableType) : undefined
      },
      callMode: config.callMode === "async" ? "asyncReserved" : "sync"
    };
  }
  if (config.activityType === "callRest") {
    return {
      ...base,
      kind: "restCall",
      officialType: "Microflows$RestCallAction",
      request: {
        method: config.method ?? "GET",
        urlExpression: expression(config.url ?? ""),
        headers: (config.headers ?? []).map(header => ({ key: header.key, valueExpression: expression(header.value) })),
        queryParameters: (config.query ?? []).map(query => ({ key: query.key, valueExpression: expression(query.value) })),
        body: config.bodyType === "json"
          ? { kind: "json", expression: config.bodyExpression ?? expression("") }
          : config.bodyType === "text"
            ? { kind: "text", expression: config.bodyExpression ?? expression("") }
            : { kind: "none" }
      },
      response: {
        handling: config.resultVariableName ? { kind: "json", outputVariableName: config.resultVariableName } : { kind: "ignore" }
      },
      timeoutSeconds: Math.round((config.timeoutMs ?? 30000) / 1000)
    };
  }
  if (config.activityType === "logMessage") {
    return {
      ...base,
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      level: config.logLevel === "warn" ? "warning" : config.logLevel ?? "info",
      logNodeName: "Microflow",
      template: {
        text: config.messageExpression?.text ?? "",
        arguments: []
      },
      includeContextVariables: Boolean(config.logContextVariables),
      includeTraceId: Boolean(config.logTraceId)
    };
  }
  return {
    ...base,
    kind: mapActivityTypeToActionKind(config.activityType),
    officialType: `Microflows$${config.activityType}`,
    legacyActivityType: config.activityType,
    legacyConfig: config
  } as MicroflowAction;
}

function mapActivityTypeToActionKind(activityType: MicroflowActivityType): MicroflowAction["kind"] {
  const map: Partial<Record<MicroflowActivityType, MicroflowAction["kind"]>> = {
    objectCast: "cast",
    listAggregate: "aggregateList",
    listCreate: "createList",
    listChange: "changeList",
    listOperation: "listOperation",
    callJavaAction: "callJavaAction",
    callJavaScriptAction: "callJavaScriptAction",
    callNanoflow: "callNanoflow",
    variableCreate: "createVariable",
    variableChange: "changeVariable",
    closePage: "closePage",
    downloadFile: "downloadFile",
    showHomePage: "showHomePage",
    showMessage: "showMessage",
    showPage: "showPage",
    validationFeedback: "validationFeedback",
    synchronize: "synchronize",
    callWebService: "webServiceCall",
    importWithMapping: "importXml",
    exportWithMapping: "exportXml",
    callExternalAction: "callExternalAction",
    sendRestRequestBeta: "restOperationCall",
    generateDocument: "generateDocument",
    callMlModel: "mlModelCall",
    deleteExternalObject: "externalObjectAction",
    sendExternalObject: "externalObjectAction"
  };
  if (["counter", "incrementCounter", "gauge"].includes(activityType)) {
    return "metric";
  }
  if (activityType.includes("Workflow") || activityType.includes("UserTask") || activityType.includes("JumpTo")) {
    return "workflowAction";
  }
  return map[activityType] ?? "externalObjectAction";
}

export function legacyNodeToObject(node: MicroflowNode): MicroflowObject {
  if (node.type === "startEvent") {
    return {
      ...objectBase(node, "startEvent", "Microflows$StartEvent"),
      trigger: { type: node.config.startTrigger ?? "manual" }
    } as MicroflowStartEvent;
  }
  if (node.type === "endEvent") {
    return {
      ...objectBase(node, "endEvent", "Microflows$EndEvent"),
      returnValue: node.config.returnValue,
      endBehavior: { type: "normalReturn" }
    } as MicroflowEndEvent;
  }
  if (node.type === "errorEvent") {
    return {
      ...objectBase(node, "errorEvent", "Microflows$ErrorEvent"),
      error: { sourceVariableName: "$latestError", messageExpression: node.config.returnValue }
    } as MicroflowErrorEvent;
  }
  if (node.type === "breakEvent") {
    return objectBase(node, "breakEvent", "Microflows$BreakEvent") as MicroflowBreakEvent;
  }
  if (node.type === "continueEvent") {
    return objectBase(node, "continueEvent", "Microflows$ContinueEvent") as MicroflowContinueEvent;
  }
  if (node.type === "decision") {
    return {
      ...objectBase(node, "exclusiveSplit", "Microflows$ExclusiveSplit"),
      splitCondition: {
        kind: node.config.decisionType === "rule" ? "rule" : "expression",
        expression: node.config.expression,
        ruleQualifiedName: node.config.ruleReference ?? "",
        parameterMappings: [],
        resultType: node.config.resultType === "Enumeration" ? "enumeration" : "boolean"
      },
      errorHandlingType: "rollback"
    } as MicroflowExclusiveSplit;
  }
  if (node.type === "objectTypeDecision") {
    return {
      ...objectBase(node, "inheritanceSplit", "Microflows$InheritanceSplit"),
      inputObjectVariableName: node.config.inputObject,
      entity: {
        generalizedEntityQualifiedName: node.config.generalizedEntity ?? "",
        allowedSpecializations: []
      },
      errorHandlingType: "rollback"
    } as MicroflowInheritanceSplit;
  }
  if (node.type === "merge") {
    return {
      ...objectBase(node, "exclusiveMerge", "Microflows$ExclusiveMerge"),
      mergeBehavior: { strategy: "firstArrived" }
    } as MicroflowExclusiveMerge;
  }
  if (node.type === "loop") {
    return {
      ...objectBase(node, "loopedActivity", "Microflows$LoopedActivity"),
      documentation: node.documentation?.summary ?? "",
      errorHandlingType: "rollback",
      loopSource: node.config.loopType === "while"
        ? { kind: "whileCondition", officialType: "Microflows$WhileLoopCondition", expression: node.config.whileExpression ?? expression("true", { kind: "boolean" }) }
        : {
            kind: "iterableList",
            officialType: "Microflows$IterableList",
            listVariableName: node.config.iterableVariableName,
            iteratorVariableName: node.config.itemVariableName,
            currentIndexVariableName: "$currentIndex"
          },
      objectCollection: {
        id: `${node.id}-collection`,
        officialType: "Microflows$MicroflowObjectCollection",
        objects: []
      }
    } as MicroflowLoopedActivity;
  }
  if (node.type === "parameter") {
    return {
      ...objectBase(node, "parameterObject", "Microflows$MicroflowParameterObject"),
      parameterId: node.config.parameter.id
    } as MicroflowParameterObject;
  }
  if (node.type === "annotation") {
    return {
      ...objectBase(node, "annotation", "Microflows$Annotation"),
      text: node.config.text
    } as MicroflowAnnotation;
  }
  return {
    ...objectBase(node, "actionActivity", "Microflows$ActionActivity"),
    caption: node.title,
    autoGenerateCaption: false,
    backgroundColor: "default",
    disabled: node.enabled === false,
    action: makeAction(node)
  } as MicroflowActionActivity;
}

function conditionToCaseValue(edge: MicroflowEdge): MicroflowCaseValue[] {
  const value = edge.conditionValue;
  if (!value) {
    return [];
  }
  if (value.kind === "boolean") {
    return [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: value.value, persistedValue: String(value.value) as "true" | "false" }];
  }
  if (value.kind === "enumeration") {
    if (value.value === "empty") {
      return [{ kind: "empty", officialType: "Microflows$NoCase" }];
    }
    return [{ kind: "enumeration", officialType: "Microflows$EnumerationCase", enumerationQualifiedName: "", value: value.value }];
  }
  if (value.kind === "objectType") {
    if (value.entity === "empty") {
      return [{ kind: "empty", officialType: "Microflows$NoCase" }];
    }
    if (value.entity === "fallback") {
      return [{ kind: "fallback", officialType: "Microflows$NoCase" }];
    }
    return [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: value.entity }];
  }
  return [{ kind: "noCase", officialType: "Microflows$NoCase" }];
}

export function legacyEdgeToFlow(edge: MicroflowEdge, nodesById: Map<string, MicroflowNode>): MicroflowFlow {
  const source = nodesById.get(edge.sourceNodeId);
  const target = nodesById.get(edge.targetNodeId);
  const points = source && target ? [source.position, target.position] : [];
  if (edge.type === "annotation") {
    return {
      id: edge.id,
      stableId: edge.id,
      kind: "annotation",
      officialType: "Microflows$AnnotationFlow",
      originObjectId: edge.sourceNodeId,
      destinationObjectId: edge.targetNodeId,
      originConnectionIndex: 0,
      destinationConnectionIndex: 0,
      line: defaultLine(points),
      editor: {
        label: edge.label,
        description: edge.description,
        showInExport: edge.showInExport ?? true
      }
    } satisfies MicroflowAnnotationFlow;
  }
  return {
    id: edge.id,
    stableId: edge.id,
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId: edge.sourceNodeId,
    destinationObjectId: edge.targetNodeId,
    originConnectionIndex: source?.ports.findIndex(port => port.id === edge.sourcePortId) ?? 0,
    destinationConnectionIndex: target?.ports.findIndex(port => port.id === edge.targetPortId) ?? 0,
    caseValues: conditionToCaseValue(edge),
    isErrorHandler: edge.type === "errorHandler",
    line: defaultLine(points),
    editor: {
      edgeKind: edge.type === "sequence" ? "sequence" : edge.type,
      label: edge.label,
      description: edge.description,
      branchOrder: edge.branchOrder
    }
  } satisfies MicroflowSequenceFlow;
}

export function buildAuthoringFieldsFromLegacy(schema: MicroflowLegacyGraphSchema): MicroflowSchema {
  const synchronizedParameters = schema.nodes
    .filter((node): node is Extract<MicroflowNode, { type: "parameter" }> => node.type === "parameter")
    .map(node => ({
      ...node.config.parameter,
      stableId: node.config.parameter.stableId ?? node.config.parameter.id,
      dataType: node.config.parameter.dataType ?? toMicroflowDataType(node.config.parameter.type),
      documentation: node.config.parameter.documentation ?? node.config.parameter.description
    }));
  const parameters = synchronizedParameters.length > 0 ? synchronizedParameters : schema.parameters;
  const loopObjects = new Map<string, MicroflowObject[]>();
  const rootObjects: MicroflowObject[] = [];
  for (const node of schema.nodes) {
    const object = legacyNodeToObject(node);
    if (node.parentLoopId) {
      const list = loopObjects.get(node.parentLoopId) ?? [];
      list.push(object);
      loopObjects.set(node.parentLoopId, list);
    } else {
      rootObjects.push(object);
    }
  }
  for (const object of rootObjects) {
    if (object.kind === "loopedActivity") {
      object.objectCollection.objects = loopObjects.get(object.id) ?? [];
    }
  }
  const nodesById = new Map(schema.nodes.map(node => [node.id, node]));
  const objectCollection: MicroflowObjectCollection = {
    id: "root",
    officialType: "Microflows$MicroflowObjectCollection",
    objects: rootObjects
  };
  const flows = schema.edges.map(edge => legacyEdgeToFlow(edge, nodesById));
  const variableIndex = buildVariableIndex(parameters, objectCollection, flows);
  const returnType = schema.nodes
    .filter(node => node.type === "endEvent")
    .map(node => toMicroflowDataType(node.config.returnValue?.expectedType ?? node.config.returnType))
    .find(type => type.kind !== "unknown") ?? { kind: "void" as const };
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx11",
    id: schema.id,
    stableId: schema.id,
    name: schema.name,
    displayName: schema.name,
    description: schema.description,
    documentation: schema.description,
    moduleId: "order",
    moduleName: "Order",
    parameters: parameters.map(parameter => ({
      ...parameter,
      stableId: parameter.stableId ?? parameter.id,
      dataType: parameter.dataType ?? toMicroflowDataType(parameter.type),
      documentation: parameter.documentation ?? parameter.description
    })),
    returnType,
    returnVariableName: "result",
    objectCollection,
    flows,
    security: {
      applyEntityAccess: true,
      allowedModuleRoleIds: []
    },
    concurrency: {
      allowConcurrentExecution: true
    },
    exposure: {
      exportLevel: "module",
      markAsUsed: false
    },
    variables: variableIndex,
    validation: {
      issues: []
    },
    editor: {
      viewport: {
        x: schema.viewport?.offset.x ?? 0,
        y: schema.viewport?.offset.y ?? 0,
        zoom: schema.viewport?.zoom ?? 1
      },
      selection: {}
    },
    audit: {
      version: schema.version,
      status: "draft"
    },
    version: schema.version
  };
}

export function isLegacyGraphSchema(schema: MicroflowSchema | MicroflowLegacyGraphSchema | unknown): schema is MicroflowLegacyGraphSchema {
  const value = schema as Partial<MicroflowLegacyGraphSchema>;
  return Array.isArray(value.nodes) && Array.isArray(value.edges);
}

export function ensureAuthoringSchema(schema: MicroflowSchema | MicroflowLegacyGraphSchema): MicroflowSchema {
  if (isLegacyGraphSchema(schema)) {
    return buildAuthoringFieldsFromLegacy(schema);
  }
  return schema;
}

export function applyLegacyGraphPatch(schema: MicroflowLegacyGraphSchema, patch: Partial<Pick<MicroflowLegacyGraphSchema, "nodes" | "edges" | "viewport" | "variables">>): MicroflowSchema {
  const legacySchema = {
    ...schema,
    nodes: patch.nodes ?? schema.nodes,
    edges: patch.edges ?? schema.edges,
    viewport: patch.viewport ?? schema.viewport,
    variables: patch.variables ?? schema.variables
  };
  return buildAuthoringFieldsFromLegacy(legacySchema);
}

export function flattenObjectCollection(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object => object.kind === "loopedActivity" ? [object, ...flattenObjectCollection(object.objectCollection)] : [object]);
}

function portsForObject(object: MicroflowObject): MicroflowPort[] {
  const input: MicroflowPort = { id: "in", label: "In", direction: "input", kind: "sequenceIn", cardinality: "one", edgeTypes: ["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"] };
  const output: MicroflowPort = { id: "out", label: "Out", direction: "output", kind: "sequenceOut", cardinality: "one", edgeTypes: ["sequence"] };
  const error: MicroflowPort = { id: "error", label: "Error", direction: "output", kind: "errorOut", cardinality: "zeroOrOne", edgeTypes: ["errorHandler"] };
  if (object.kind === "startEvent") {
    return [output];
  }
  if (["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(object.kind)) {
    return [input];
  }
  if (object.kind === "exclusiveSplit") {
    return [input, { id: "true", label: "True", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] }, { id: "false", label: "False", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] }, error];
  }
  if (object.kind === "inheritanceSplit") {
    return [input, { id: "objectType", label: "Object Type", direction: "output", kind: "objectTypeOut", cardinality: "oneOrMore", edgeTypes: ["objectTypeCondition"] }, error];
  }
  if (object.kind === "parameterObject" || object.kind === "annotation") {
    return [{ id: "note", label: "Note", direction: "output", kind: "annotation", cardinality: "zeroOrMore", edgeTypes: ["annotation"] }];
  }
  return object.kind === "actionActivity" || object.kind === "loopedActivity" ? [input, output, error] : [input, output];
}

export function objectToLegacyNode(object: MicroflowObject, parentLoopId?: string): MicroflowNode {
  const typeMap: Record<MicroflowObjectKind, MicroflowNode["type"]> = {
    startEvent: "startEvent",
    endEvent: "endEvent",
    errorEvent: "errorEvent",
    breakEvent: "breakEvent",
    continueEvent: "continueEvent",
    exclusiveSplit: "decision",
    inheritanceSplit: "objectTypeDecision",
    exclusiveMerge: "merge",
    actionActivity: "activity",
    loopedActivity: "loop",
    parameterObject: "parameter",
    annotation: "annotation"
  };
  const type = typeMap[object.kind];
  const renderShape = object.kind === "annotation" ? "annotation" : object.kind.includes("Split") || object.kind === "exclusiveMerge" ? "diamond" : object.kind === "loopedActivity" ? "loop" : object.kind.endsWith("Event") ? "event" : "roundedRect";
  const base = {
    id: object.id,
    type,
    kind: type === "decision" ? "decision" : type === "objectTypeDecision" ? "objectTypeDecision" : type === "merge" ? "merge" : type === "loop" ? "loop" : type === "parameter" ? "parameter" : type === "annotation" ? "annotation" : type === "activity" ? "activity" : "event",
    title: object.caption ?? object.id,
    titleZh: object.caption ?? object.id,
    description: object.documentation,
    category: type === "activity" ? "activities" : type === "loop" ? "loop" : type === "parameter" ? "parameters" : type === "annotation" ? "annotations" : type === "decision" || type === "objectTypeDecision" || type === "merge" ? "decisions" : "events",
    position: object.relativeMiddlePoint,
    ports: portsForObject(object),
    render: { iconKey: object.editor.iconKey ?? object.kind, shape: renderShape, tone: "neutral", width: object.size.width, height: object.size.height },
    propertyForm: { formKey: object.kind, sections: ["General"] },
    parentLoopId
  } as MicroflowNode;
  if (object.kind === "actionActivity") {
    return {
      ...base,
      config: {
        activityType: actionToActivityType(object.action),
        activityCategory: object.action.editor.category,
        supportsErrorFlow: object.action.errorHandlingType !== "continue",
        errorHandling: { mode: object.action.errorHandlingType }
      }
    } as MicroflowNode;
  }
  if (object.kind === "exclusiveSplit") {
    return {
      ...base,
      config: object.splitCondition.kind === "expression"
        ? { expression: object.splitCondition.expression, resultType: object.splitCondition.resultType === "boolean" ? "Boolean" : "Enumeration" }
        : { expression: expression(""), decisionType: "rule", ruleReference: object.splitCondition.ruleQualifiedName, resultType: "Boolean" }
    } as MicroflowNode;
  }
  if (object.kind === "inheritanceSplit") {
    return {
      ...base,
      config: { inputObject: object.inputObjectVariableName, generalizedEntity: object.entity.generalizedEntityQualifiedName }
    } as MicroflowNode;
  }
  if (object.kind === "loopedActivity") {
    return {
      ...base,
      config: object.loopSource.kind === "whileCondition"
        ? { loopType: "while", iterableVariableName: "", itemVariableName: "", whileExpression: object.loopSource.expression }
        : { loopType: "forEach", iterableVariableName: object.loopSource.listVariableName, itemVariableName: object.loopSource.iteratorVariableName, indexVariableName: "$currentIndex" }
    } as MicroflowNode;
  }
  if (object.kind === "parameterObject") {
    return {
      ...base,
      config: { parameter: { id: object.parameterId, name: object.caption ?? object.parameterId, required: true, type: { kind: "unknown", name: "Unknown" } } }
    } as MicroflowNode;
  }
  if (object.kind === "annotation") {
    return { ...base, config: { text: object.text } } as MicroflowNode;
  }
  if (object.kind === "endEvent") {
    return { ...base, config: { returnValue: object.returnValue, returnType: object.returnValue?.expectedType } } as MicroflowNode;
  }
  return { ...base, config: {} } as MicroflowNode;
}

function actionToActivityType(action: MicroflowAction): MicroflowActivityType {
  const map: Partial<Record<MicroflowAction["kind"], MicroflowActivityType>> = {
    retrieve: "objectRetrieve",
    createObject: "objectCreate",
    changeMembers: "objectChange",
    commit: "objectCommit",
    delete: "objectDelete",
    rollback: "objectRollback",
    cast: "objectCast",
    aggregateList: "listAggregate",
    createList: "listCreate",
    changeList: "listChange",
    listOperation: "listOperation",
    callMicroflow: "callMicroflow",
    callJavaAction: "callJavaAction",
    callJavaScriptAction: "callJavaScriptAction",
    callNanoflow: "callNanoflow",
    createVariable: "variableCreate",
    changeVariable: "variableChange",
    restCall: "callRest",
    webServiceCall: "callWebService",
    importXml: "importWithMapping",
    exportXml: "exportWithMapping",
    callExternalAction: "callExternalAction",
    logMessage: "logMessage",
    generateDocument: "generateDocument",
    mlModelCall: "callMlModel"
  };
  return map[action.kind] ?? "logMessage";
}

export function flowToLegacyEdge(flow: MicroflowFlow): MicroflowEdge {
  if (flow.kind === "annotation") {
    return {
      id: flow.id,
      type: "annotation",
      sourceNodeId: flow.originObjectId,
      targetNodeId: flow.destinationObjectId,
      sourcePortId: "note",
      targetPortId: "in",
      label: flow.editor.label,
      description: flow.editor.description,
      showInExport: flow.editor.showInExport
    };
  }
  const edgeKind = flow.isErrorHandler ? "errorHandler" : flow.editor.edgeKind;
  return {
    id: flow.id,
    type: edgeKind,
    sourceNodeId: flow.originObjectId,
    targetNodeId: flow.destinationObjectId,
    sourcePortId: edgeKind === "errorHandler" ? "error" : flow.caseValues[0]?.kind === "boolean" ? String(flow.caseValues[0].value) : "out",
    targetPortId: "in",
    label: flow.editor.label,
    description: flow.editor.description,
    conditionValue: caseValueToCondition(flow.caseValues[0], edgeKind),
    branchOrder: flow.editor.branchOrder,
    errorHandlingType: flow.isErrorHandler ? "customWithRollback" : undefined
  } as MicroflowEdge;
}

function caseValueToCondition(caseValue: MicroflowCaseValue | undefined, edgeKind: MicroflowEdgeKind) {
  if (!caseValue) {
    return undefined;
  }
  if (caseValue.kind === "boolean") {
    return { kind: "boolean" as const, value: caseValue.value };
  }
  if (caseValue.kind === "enumeration") {
    return { kind: "enumeration" as const, value: caseValue.value };
  }
  if (caseValue.kind === "inheritance") {
    return { kind: "objectType" as const, entity: caseValue.entityQualifiedName };
  }
  if (edgeKind === "objectTypeCondition") {
    return { kind: "objectType" as const, entity: caseValue.kind };
  }
  return { kind: "enumeration" as const, value: caseValue.kind };
}

export function toLegacyGraph(schema: MicroflowAuthoringSchema): { nodes: MicroflowNode[]; edges: MicroflowEdge[] } {
  const nodes: MicroflowNode[] = [];
  for (const object of schema.objectCollection.objects) {
    nodes.push(objectToLegacyNode(object));
    if (object.kind === "loopedActivity") {
      nodes.push(...object.objectCollection.objects.map(child => objectToLegacyNode(child, object.id)));
    }
  }
  return {
    nodes,
    edges: schema.flows.map(flowToLegacyEdge)
  };
}

export function findMicroflowObject(collection: MicroflowObjectCollection, objectId: string): MicroflowObject | undefined {
  for (const object of collection.objects) {
    if (object.id === objectId) {
      return object;
    }
    if (object.kind === "loopedActivity") {
      const found = findMicroflowObject(object.objectCollection, objectId);
      if (found) {
        return found;
      }
    }
  }
  return undefined;
}

export function toEditorGraph(schema: MicroflowSchema | MicroflowAuthoringSchema): MicroflowEditorGraph {
  const objects = collectEditorObjects(schema.objectCollection);
  const issues = "validation" in schema ? schema.validation.issues : [];
  return {
    nodes: objects.map(entry => ({
      id: `node-${entry.object.id}`,
      objectId: entry.object.id,
      nodeKind: entry.object.kind,
      activityKind: entry.object.kind === "actionActivity" ? entry.object.action.kind : undefined,
      title: entry.object.caption ?? entry.object.id,
      subtitle: entry.object.officialType,
      iconKey: entry.object.editor.iconKey ?? entry.object.kind,
      position: entry.object.relativeMiddlePoint,
      size: entry.object.size,
      ports: portsForObject(entry.object).map((port, index) => ({
        id: `${entry.object.id}:${port.id}`,
        objectId: entry.object.id,
        label: port.label,
        direction: port.direction,
        kind: port.kind,
        connectionIndex: index,
        edgeTypes: port.edgeTypes
      })),
      parentObjectId: entry.parentObjectId,
      collectionId: entry.collectionId,
      state: {
        selected: Boolean(entry.object.editor.selected),
        disabled: Boolean(entry.object.disabled),
        hasError: issues.some(issue => issue.severity === "error" && (issue.objectId === entry.object.id || issue.nodeId === entry.object.id)),
        hasWarning: issues.some(issue => issue.severity === "warning" && (issue.objectId === entry.object.id || issue.nodeId === entry.object.id))
      }
    })),
    edges: schema.flows.map(flow => ({
      id: `edge-${flow.id}`,
      flowId: flow.id,
      sourceNodeId: `node-${flow.originObjectId}`,
      targetNodeId: `node-${flow.destinationObjectId}`,
      sourcePortId: `${flow.originObjectId}:${flow.kind === "sequence" && flow.isErrorHandler ? "error" : "out"}`,
      targetPortId: `${flow.destinationObjectId}:in`,
      edgeKind: flow.kind === "annotation" ? "annotation" : flow.editor.edgeKind,
      label: flow.kind === "annotation" ? flow.editor.label : flow.editor.label,
      style: {
        strokeType: flow.line.style.strokeType,
        colorToken: flow.kind === "sequence" && flow.isErrorHandler ? "#f93920" : flow.kind === "annotation" ? "#86909c" : "#4e5969",
        arrow: flow.line.style.arrow === "target" || flow.line.style.arrow === "both"
      },
      state: {
        selected: Boolean(flow.editor.selected),
        hasError: issues.some(issue => issue.flowId === flow.id || issue.edgeId === flow.id),
        runtimeVisited: false
      }
    })),
    viewport: schema.editor.viewport,
    selection: schema.editor.selection
  };
}

function collectEditorObjects(
  collection: MicroflowObjectCollection,
  parentObjectId?: string
): Array<{ object: MicroflowObject; collectionId: string; parentObjectId?: string }> {
  return collection.objects.flatMap(object => [
    { object, collectionId: collection.id, parentObjectId },
    ...(object.kind === "loopedActivity" ? collectEditorObjects(object.objectCollection, object.id) : [])
  ]);
}

function mapObject(collection: MicroflowObjectCollection, objectId: string, mapper: (object: MicroflowObject) => MicroflowObject): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => {
      const current = object.id === objectId ? mapper(object) : object;
      if (current.kind === "loopedActivity") {
        return {
          ...current,
          objectCollection: mapObject(current.objectCollection, objectId, mapper)
        };
      }
      return current;
    })
  };
}

export function applyEditorGraphPatch(schema: MicroflowSchema, patch: MicroflowEditorGraphPatch): MicroflowSchema {
  let objectCollection = schema.objectCollection;
  for (const moved of patch.movedNodes ?? []) {
    objectCollection = mapObject(objectCollection, moved.objectId, object => ({ ...object, relativeMiddlePoint: moved.position }));
  }
  for (const resized of patch.resizedNodes ?? []) {
    objectCollection = mapObject(objectCollection, resized.objectId, object => ({ ...object, size: resized.size }));
  }
  const flows = schema.flows.map(flow => {
    const update = patch.updatedFlows?.find(item => item.flowId === flow.id);
    if (!update) {
      return flow;
    }
    if (flow.kind === "annotation") {
      return { ...flow, line: update.line ?? flow.line, editor: { ...flow.editor, label: update.label ?? flow.editor.label } };
    }
    return { ...flow, line: update.line ?? flow.line, editor: { ...flow.editor, label: update.label ?? flow.editor.label } };
  });
  const next = {
    ...schema,
    objectCollection,
    flows,
    editor: {
      ...schema.editor,
      viewport: patch.viewport ?? schema.editor.viewport,
      selection: {
        objectId: patch.selectedObjectId ?? schema.editor.selection.objectId,
        flowId: patch.selectedFlowId ?? schema.editor.selection.flowId
      }
    }
  };
  return {
    ...next,
    version: schema.version
  };
}

export function toMendixCompat(schema: MicroflowAuthoringSchema): MendixCompatMicroflow {
  return {
    $ID: schema.id,
    $Type: "Microflows$Microflow",
    name: schema.name,
    documentation: schema.documentation ?? "",
    parameters: schema.parameters,
    microflowReturnType: schema.returnType,
    returnVariableName: schema.returnVariableName ?? "",
    objectCollection: schema.objectCollection,
    flows: schema.flows,
    applyEntityAccess: schema.security.applyEntityAccess,
    allowedModuleRoleIds: schema.security.allowedModuleRoleIds,
    allowConcurrentExecution: schema.concurrency.allowConcurrentExecution,
    concurrencyErrorMessage: schema.concurrency.errorMessage ? { text: schema.concurrency.errorMessage } : undefined,
    concurrencyErrorMicroflow: schema.concurrency.errorMicroflowId,
    excluded: false,
    exportLevel: schema.exposure.exportLevel === "hidden" ? "Hidden" : schema.exposure.exportLevel === "public" ? "Public" : "UsableFromModule",
    markAsUsed: schema.exposure.markAsUsed,
    microflowActionInfo: schema.exposure.asMicroflowAction?.enabled ? schema.exposure.asMicroflowAction : null,
    workflowActionInfo: schema.exposure.asWorkflowAction?.enabled ? schema.exposure.asWorkflowAction : null,
    url: schema.exposure.url?.path,
    urlSearchParameters: schema.exposure.url?.searchParameters,
    stableId: schema.stableId
  };
}

export function fromMendixCompat(input: MendixCompatMicroflow): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx11",
    id: input.$ID,
    stableId: input.stableId ?? input.$ID,
    name: input.name,
    displayName: input.name,
    documentation: input.documentation,
    moduleId: "default",
    parameters: input.parameters,
    returnType: input.microflowReturnType,
    returnVariableName: input.returnVariableName,
    objectCollection: input.objectCollection,
    flows: input.flows,
    security: {
      applyEntityAccess: input.applyEntityAccess,
      allowedModuleRoleIds: input.allowedModuleRoleIds
    },
    concurrency: {
      allowConcurrentExecution: input.allowConcurrentExecution,
      errorMessage: input.concurrencyErrorMessage?.text,
      errorMicroflowId: input.concurrencyErrorMicroflow
    },
    exposure: {
      exportLevel: input.exportLevel === "Hidden" ? "hidden" : input.exportLevel === "Public" ? "public" : "module",
      markAsUsed: input.markAsUsed,
      asMicroflowAction: input.microflowActionInfo ? { enabled: true, ...input.microflowActionInfo } : undefined,
      asWorkflowAction: input.workflowActionInfo ? { enabled: true, ...input.workflowActionInfo } : undefined,
      url: input.url ? { enabled: true, path: input.url, searchParameters: input.urlSearchParameters } : undefined
    },
    variables: emptyVariableIndex(),
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, selection: {} },
    audit: { version: "v1", status: "draft" }
  };
}

export function toRuntimeDto(schema: MicroflowAuthoringSchema): MicroflowRuntimeDto {
  return {
    microflowId: schema.id,
    schemaVersion: schema.schemaVersion,
    name: schema.name,
    returnType: schema.returnType,
    parameters: schema.parameters,
    objectCollection: schema.objectCollection,
    flows: schema.flows,
    variables: schema.variables
  };
}
