import {
  getAssociationByQualifiedName,
  getEntityByQualifiedName,
  getMicroflowById,
  getTargetEntityByAssociation,
  type MicroflowMetadataCatalog,
} from "../metadata";
import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowDataType,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowSequenceFlow,
  MicroflowVariableDiagnostic,
  MicroflowVariableIndex,
  MicroflowVariableKind,
  MicroflowVariableScope,
  MicroflowVariableSource,
  MicroflowVariableSymbol,
  MicroflowVariableVisibility,
} from "../schema/types";
import { collectFlowsRecursive, findObjectWithCollection } from "../schema/utils/object-utils";

const variableNamePattern = /^[A-Za-z_][A-Za-z0-9_]*$/;

function emptyIndex(schema: MicroflowSchema): MicroflowVariableIndex {
  return {
    schemaId: schema.id,
    builtAt: new Date().toISOString(),
    all: [],
    byName: {},
    byObjectId: {},
    byActionId: {},
    byScopeKey: {},
    diagnostics: [],
    parameters: {},
    localVariables: {},
    objectOutputs: {},
    listOutputs: {},
    loopVariables: {},
    errorVariables: {},
    systemVariables: {},
  };
}

function flattenObjects(collection: MicroflowObjectCollection, parentLoopId?: string): Array<{ object: MicroflowObject; collectionId: string; loopObjectId?: string }> {
  return collection.objects.flatMap(object => {
    if (object.kind !== "loopedActivity") {
      return [{ object, collectionId: collection.id, loopObjectId: parentLoopId }];
    }
    return [
      { object, collectionId: collection.id, loopObjectId: parentLoopId },
      ...flattenObjects(object.objectCollection, object.id),
    ];
  });
}

function dataTypeKind(dataType: MicroflowDataType): MicroflowVariableKind {
  if (dataType.kind === "list") {
    return "listOutput";
  }
  if (dataType.kind === "object") {
    return "objectOutput";
  }
  return "primitiveOutput";
}

function scopeKey(scope: MicroflowVariableScope): string {
  return [
    scope.kind ?? "objectCollection",
    scope.collectionId,
    scope.startObjectId,
    scope.loopObjectId,
    scope.errorHandlerFlowId,
    scope.branchFlowId,
  ].filter(Boolean).join(":");
}

function addDiagnostic(index: MicroflowVariableIndex, diagnostic: Omit<MicroflowVariableDiagnostic, "id">): void {
  index.diagnostics ??= [];
  index.diagnostics.push({
    id: `${diagnostic.code}:${diagnostic.objectId ?? diagnostic.actionId ?? diagnostic.flowId ?? diagnostic.variableName ?? diagnostic.fieldPath ?? index.diagnostics.length}`,
    ...diagnostic,
  });
}

function addSymbol(index: MicroflowVariableIndex, symbol: MicroflowVariableSymbol): void {
  const normalized: MicroflowVariableSymbol = {
    ...symbol,
    id: symbol.id ?? `${symbol.source.kind}:${symbol.name}:${symbol.availableFromObjectId ?? symbol.scope.startObjectId ?? symbol.scope.collectionId}`,
    displayName: symbol.displayName ?? symbol.name,
    visibility: symbol.visibility ?? "definite",
  };
  index.all ??= [];
  index.byName ??= {};
  index.byObjectId ??= {};
  index.byActionId ??= {};
  index.byScopeKey ??= {};
  index.all.push(normalized);
  index.byName[normalized.name] = [...(index.byName[normalized.name] ?? []), normalized];
  index.byScopeKey[scopeKey(normalized.scope)] = [...(index.byScopeKey[scopeKey(normalized.scope)] ?? []), normalized];
  if ("objectId" in normalized.source) {
    index.byObjectId[normalized.source.objectId] = [...(index.byObjectId[normalized.source.objectId] ?? []), normalized];
  }
  if ("actionId" in normalized.source) {
    index.byActionId[normalized.source.actionId] = [...(index.byActionId[normalized.source.actionId] ?? []), normalized];
  }
  if (normalized.kind === "parameter") {
    index.parameters[normalized.name] = normalized;
  } else if (normalized.kind === "loopIterator") {
    index.loopVariables[normalized.name] = normalized;
  } else if (normalized.kind === "system") {
    index.systemVariables[normalized.name] = normalized;
  } else if (normalized.kind === "errorContext" || normalized.kind === "restResponse" || normalized.kind === "soapFault") {
    index.errorVariables[normalized.name] = normalized;
  } else if (normalized.dataType.kind === "list") {
    index.listOutputs[normalized.name] = normalized;
  } else if (normalized.dataType.kind === "object") {
    index.objectOutputs[normalized.name] = normalized;
  } else {
    index.localVariables[normalized.name] = normalized;
  }
}

function createSymbol(input: {
  name: string;
  kind: MicroflowVariableKind;
  dataType: MicroflowDataType;
  source: MicroflowVariableSource;
  scope: MicroflowVariableScope;
  readonly: boolean;
  visibility?: MicroflowVariableVisibility;
  documentation?: string;
}): MicroflowVariableSymbol {
  return {
    id: `${input.source.kind}:${input.name}:${"objectId" in input.source ? input.source.objectId : input.scope.collectionId}`,
    name: input.name,
    kind: input.kind,
    dataType: input.dataType,
    source: input.source,
    scope: input.scope,
    readonly: input.readonly,
    visibility: input.visibility ?? "definite",
    availableFromObjectId: input.scope.startObjectId,
    documentation: input.documentation,
  };
}

function validateOutputName(index: MicroflowVariableIndex, name: string, objectId?: string, actionId?: string, fieldPath = "action.outputVariableName"): void {
  if (!name.trim()) {
    addDiagnostic(index, {
      severity: "error",
      code: "MF_VARIABLE_NAME_REQUIRED",
      message: "Variable name is required.",
      objectId,
      actionId,
      fieldPath,
      variableName: name,
    });
    return;
  }
  if (name.startsWith("$")) {
    addDiagnostic(index, {
      severity: "error",
      code: "MF_VARIABLE_NAME_SYSTEM_RESERVED",
      message: `Variable "${name}" cannot use the reserved system prefix $.`,
      objectId,
      actionId,
      fieldPath,
      variableName: name,
    });
    return;
  }
  if (!variableNamePattern.test(name)) {
    addDiagnostic(index, {
      severity: "error",
      code: "MF_VARIABLE_NAME_INVALID",
      message: `Variable "${name}" must start with a letter or underscore and contain only letters, numbers and underscores.`,
      objectId,
      actionId,
      fieldPath,
      variableName: name,
    });
  }
}

function addOutput(index: MicroflowVariableIndex, symbol: MicroflowVariableSymbol, fieldPath?: string): void {
  validateOutputName(index, symbol.name, "objectId" in symbol.source ? symbol.source.objectId : undefined, "actionId" in symbol.source ? symbol.source.actionId : undefined, fieldPath);
  addSymbol(index, symbol);
}

function retrieveOutputType(action: Extract<MicroflowAction, { kind: "retrieve" }>, metadata: MicroflowMetadataCatalog): MicroflowDataType {
  if (action.retrieveSource.kind === "database") {
    const entityQualifiedName = action.retrieveSource.entityQualifiedName ?? "";
    const itemType: MicroflowDataType = entityQualifiedName
      ? { kind: "object", entityQualifiedName }
      : { kind: "unknown", reason: "retrieve entity missing" };
    return action.retrieveSource.range.kind === "first" ? itemType : { kind: "list", itemType };
  }
  const association = getAssociationByQualifiedName(metadata, action.retrieveSource.associationQualifiedName ?? undefined);
  const target = getTargetEntityByAssociation(metadata, action.retrieveSource.associationQualifiedName ?? undefined);
  const itemType: MicroflowDataType = target
    ? { kind: "object", entityQualifiedName: target.qualifiedName }
    : { kind: "unknown", reason: action.retrieveSource.associationQualifiedName ?? "association retrieve" };
  return association?.multiplicity === "oneToMany" || association?.multiplicity === "manyToMany"
    ? { kind: "list", itemType }
    : itemType;
}

function stringField(action: MicroflowAction, key: string): string | undefined {
  const value = (action as unknown as Record<string, unknown>)[key];
  return typeof value === "string" && value.trim() ? value : undefined;
}

function nestedStringField(action: MicroflowAction, key: string, nestedKey: string): string | undefined {
  const value = (action as unknown as Record<string, unknown>)[key];
  if (!value || typeof value !== "object") {
    return undefined;
  }
  const nested = (value as Record<string, unknown>)[nestedKey];
  return typeof nested === "string" && nested.trim() ? nested : undefined;
}

function genericOutputType(action: MicroflowAction): MicroflowDataType | undefined {
  if (action.kind === "cast") {
    const entityQualifiedName = stringField(action, "targetEntityQualifiedName");
    return entityQualifiedName ? { kind: "object", entityQualifiedName } : { kind: "unknown", reason: "cast target entity missing" };
  }
  if (action.kind === "aggregateList") {
    return stringField(action, "aggregateFunction") === "count" ? { kind: "integer" } : { kind: "decimal" };
  }
  if (action.kind === "createList") {
    const entityQualifiedName = stringField(action, "entityQualifiedName");
    return { kind: "list", itemType: entityQualifiedName ? { kind: "object", entityQualifiedName } : { kind: "unknown", reason: "createList entity missing" } };
  }
  if (action.kind === "listOperation") {
    const operation = stringField(action, "operation");
    if (operation === "equals" || operation === "contains") {
      return { kind: "boolean" };
    }
    if (operation === "find" || operation === "head") {
      return { kind: "unknown", reason: "list operation object result" };
    }
    return { kind: "list", itemType: { kind: "unknown", reason: "list operation item type" } };
  }
  if (action.kind === "exportXml") {
    return stringField(action, "outputType") === "fileDocument" ? { kind: "object", entityQualifiedName: "System.FileDocument" } : { kind: "string" };
  }
  if (action.kind === "callWorkflow") {
    return { kind: "object", entityQualifiedName: "Workflow.Workflow" };
  }
  if (action.kind === "generateJumpToOptions" || action.kind === "retrieveWorkflowActivityRecords" || action.kind === "retrieveWorkflows") {
    return { kind: "list", itemType: { kind: "unknown", reason: action.kind } };
  }
  if (action.kind === "retrieveWorkflowContext" || action.kind === "mlModelCall" || action.kind === "webServiceCall" || action.kind === "importXml" || action.kind === "restOperationCall") {
    return { kind: "unknown", reason: action.kind };
  }
  return undefined;
}

function genericOutputName(action: MicroflowAction): { name: string; fieldPath: string } | undefined {
  if (action.kind === "cast" || action.kind === "aggregateList" || action.kind === "listOperation" || action.kind === "webServiceCall" || action.kind === "importXml" || action.kind === "exportXml" || action.kind === "restOperationCall" || action.kind === "mlModelCall" || action.kind === "retrieveWorkflowContext") {
    const name = stringField(action, "outputVariableName");
    return name ? { name, fieldPath: "action.outputVariableName" } : undefined;
  }
  if (action.kind === "createList" || action.kind === "retrieveWorkflows") {
    const name = stringField(action, "outputListVariableName");
    return name ? { name, fieldPath: "action.outputListVariableName" } : undefined;
  }
  if (action.kind === "callJavaAction" || action.kind === "callJavaScriptAction" || action.kind === "callNanoflow") {
    const name = nestedStringField(action, "returnValue", "outputVariableName");
    return name ? { name, fieldPath: "action.returnValue.outputVariableName" } : undefined;
  }
  if (action.kind === "callExternalAction") {
    const name = stringField(action, "returnVariableName");
    return name ? { name, fieldPath: "action.returnVariableName" } : undefined;
  }
  if (action.kind === "callWorkflow") {
    const name = stringField(action, "outputWorkflowVariableName");
    return name ? { name, fieldPath: "action.outputWorkflowVariableName" } : undefined;
  }
  if (action.kind === "generateJumpToOptions" || action.kind === "retrieveWorkflowActivityRecords") {
    const name = stringField(action, "outputVariableName");
    return name ? { name, fieldPath: "action.outputVariableName" } : undefined;
  }
  return undefined;
}

function addActionOutputs(index: MicroflowVariableIndex, object: MicroflowActionActivity, collectionId: string, metadata: MicroflowMetadataCatalog): void {
  const action = object.action;
  const downstream: MicroflowVariableScope = { kind: "downstream", collectionId, startObjectId: object.id };
  if (action.kind === "retrieve") {
    const dataType = retrieveOutputType(action, metadata);
    addOutput(index, createSymbol({
      name: action.outputVariableName,
      kind: dataTypeKind(dataType),
      dataType,
      source: { kind: "actionOutput", objectId: object.id, actionId: action.id, actionKind: action.kind },
      scope: downstream,
      readonly: false,
    }), "action.outputVariableName");
  }
  if (action.kind === "createObject") {
    addOutput(index, createSymbol({
      name: action.outputVariableName,
      kind: "objectOutput",
      dataType: action.entityQualifiedName ? { kind: "object", entityQualifiedName: action.entityQualifiedName } : { kind: "unknown", reason: "createObject entity missing" },
      source: { kind: "actionOutput", objectId: object.id, actionId: action.id, actionKind: action.kind },
      scope: downstream,
      readonly: false,
    }), "action.outputVariableName");
  }
  if (action.kind === "createVariable") {
    addOutput(index, createSymbol({
      name: action.variableName,
      kind: "localVariable",
      dataType: action.dataType,
      source: { kind: "createVariable", objectId: object.id, actionId: action.id },
      scope: downstream,
      readonly: action.readonly ?? false,
    }), "action.variableName");
  }
  if (action.kind === "callMicroflow" && action.returnValue.storeResult && action.returnValue.outputVariableName) {
    const target = getMicroflowById(metadata, action.targetMicroflowId);
    const dataType = target?.returnType ?? action.returnValue.dataType ?? { kind: "unknown", reason: "microflow return" };
    addOutput(index, createSymbol({
      name: action.returnValue.outputVariableName,
      kind: "microflowReturn",
      dataType,
      source: { kind: "microflowReturn", objectId: object.id, targetMicroflowId: action.targetMicroflowId },
      scope: downstream,
      readonly: false,
    }), "action.returnValue.outputVariableName");
  }
  if (action.kind === "restCall") {
    if (action.response.handling.kind !== "ignore") {
      const dataType: MicroflowDataType = action.response.handling.kind === "string"
        ? { kind: "string" }
        : action.response.handling.kind === "json"
          ? { kind: "json" }
          : { kind: "unknown", reason: "REST import mapping" };
      addOutput(index, createSymbol({
        name: action.response.handling.outputVariableName,
        kind: "restResponse",
        dataType,
        source: { kind: "restResponse", objectId: object.id, responseKind: action.response.handling.kind },
        scope: downstream,
        readonly: false,
      }), "action.response.handling.outputVariableName");
    }
    if (action.response.statusCodeVariableName) {
      addOutput(index, createSymbol({
        name: action.response.statusCodeVariableName,
        kind: "restResponse",
        dataType: { kind: "integer" },
        source: { kind: "restResponse", objectId: object.id, responseKind: "statusCode" },
        scope: downstream,
        readonly: true,
      }), "action.response.statusCodeVariableName");
    }
    if (action.response.headersVariableName) {
      addOutput(index, createSymbol({
        name: action.response.headersVariableName,
        kind: "restResponse",
        dataType: { kind: "json" },
        source: { kind: "restResponse", objectId: object.id, responseKind: "headers" },
        scope: downstream,
        readonly: true,
      }), "action.response.headersVariableName");
    }
  }
  const genericName = genericOutputName(action);
  const genericType = genericOutputType(action);
  if (genericName && genericType) {
    addOutput(index, createSymbol({
      name: genericName.name,
      kind: dataTypeKind(genericType),
      dataType: genericType,
      source: { kind: "actionOutput", objectId: object.id, actionId: action.id, actionKind: action.kind },
      scope: downstream,
      readonly: false,
    }), genericName.fieldPath);
    if (genericType.kind === "unknown") {
      addDiagnostic(index, {
        severity: "warning",
        code: "MF_VARIABLE_OUTPUT_TYPE_UNKNOWN",
        message: `Variable "${genericName.name}" has an unknown type until metadata is configured.`,
        objectId: object.id,
        actionId: action.id,
        fieldPath: genericName.fieldPath,
        variableName: genericName.name,
      });
    }
  }
}

function addLoopVariables(index: MicroflowVariableIndex, object: Extract<MicroflowObject, { kind: "loopedActivity" }>, metadata: MicroflowMetadataCatalog): void {
  if (object.loopSource.kind !== "iterableList") {
    return;
  }
  const listVariable = (index.byName?.[object.loopSource.listVariableName] ?? []).find(symbol => symbol.dataType.kind === "list");
  const iteratorType = listVariable?.dataType.kind === "list"
    ? listVariable.dataType.itemType
    : { kind: "unknown", reason: object.loopSource.listVariableName } satisfies MicroflowDataType;
  addSymbol(index, createSymbol({
    name: object.loopSource.iteratorVariableName,
    kind: "loopIterator",
    dataType: iteratorType,
    source: { kind: "loopIterator", loopObjectId: object.id },
    scope: { kind: "loop", collectionId: object.objectCollection.id, loopObjectId: object.id },
    readonly: false,
  }));
  addSymbol(index, createSymbol({
    name: object.loopSource.currentIndexVariableName,
    kind: "system",
    dataType: { kind: "integer" },
    source: { kind: "system", name: "$currentIndex" },
    scope: { kind: "loop", collectionId: object.objectCollection.id, loopObjectId: object.id },
    readonly: true,
  }));
  if (iteratorType.kind === "object" && !getEntityByQualifiedName(metadata, iteratorType.entityQualifiedName)) {
    addDiagnostic(index, {
      severity: "warning",
      code: "MF_VARIABLE_LOOP_ENTITY_UNKNOWN",
      message: `Loop iterator entity "${iteratorType.entityQualifiedName}" is not found in metadata.`,
      objectId: object.id,
      fieldPath: "loopSource.iteratorVariableName",
      variableName: object.loopSource.iteratorVariableName,
    });
  }
}

function addErrorContextVariables(index: MicroflowVariableIndex, schema: MicroflowSchema, flow: MicroflowSequenceFlow): void {
  const sourceObject = flattenObjects(schema.objectCollection).find(item => item.object.id === flow.originObjectId)?.object;
  const targetCollectionId = findObjectWithCollection(schema, flow.destinationObjectId)?.collectionId ?? schema.objectCollection.id;
  const addErrorVariable = (name: "$latestError" | "$latestHttpResponse" | "$latestSoapFault", dataType: MicroflowDataType, kind: MicroflowVariableKind) => {
    addSymbol(index, createSymbol({
      name,
      kind,
      dataType,
      source: { kind: "errorContext", flowId: flow.id, sourceObjectId: flow.originObjectId, errorVariable: name },
      scope: { kind: "errorHandler", collectionId: targetCollectionId, startObjectId: flow.destinationObjectId, errorHandlerFlowId: flow.id },
      readonly: true,
    }));
  };
  addErrorVariable("$latestError", { kind: "object", entityQualifiedName: "System.Error" }, "errorContext");
  if (sourceObject?.kind === "actionActivity" && sourceObject.action.kind === "restCall") {
    addErrorVariable("$latestHttpResponse", { kind: "json" }, "restResponse");
  }
  if (sourceObject?.kind === "actionActivity" && sourceObject.action.kind === "webServiceCall") {
    addErrorVariable("$latestSoapFault", { kind: "unknown", reason: "SOAP fault" }, "soapFault");
  }
}

function finalizeDiagnostics(index: MicroflowVariableIndex): void {
  for (const [name, symbols] of Object.entries(index.byName ?? {})) {
    const userSymbols = symbols.filter(symbol => !symbol.name.startsWith("$"));
    if (userSymbols.length > 1) {
      for (const symbol of userSymbols) {
        addDiagnostic(index, {
          severity: "error",
          code: "MF_VARIABLE_DUPLICATED",
          message: `Variable "${name}" is duplicated in the current microflow scope.`,
          objectId: "objectId" in symbol.source ? symbol.source.objectId : undefined,
          actionId: "actionId" in symbol.source ? symbol.source.actionId : undefined,
          fieldPath: symbol.source.kind === "parameter" ? "parameters" : "action.outputVariableName",
          variableName: name,
        });
      }
    }
  }
}

export function buildVariableIndex(schema: MicroflowSchema, metadata: MicroflowMetadataCatalog): MicroflowVariableIndex {
  const index = emptyIndex(schema);
  addSymbol(index, createSymbol({
    name: "$currentUser",
    kind: "system",
    dataType: getEntityByQualifiedName(metadata, "System.User") ? { kind: "object", entityQualifiedName: "System.User" } : { kind: "unknown", reason: "System.User metadata missing" },
    source: { kind: "system", name: "$currentUser" },
    scope: { kind: "global", collectionId: schema.objectCollection.id },
    readonly: true,
  }));
  for (const parameter of schema.parameters) {
    validateOutputName(index, parameter.name, undefined, undefined, "parameters");
    addSymbol(index, createSymbol({
      name: parameter.name,
      kind: "parameter",
      dataType: parameter.dataType,
      source: { kind: "parameter", parameterId: parameter.id },
      scope: { kind: "global", collectionId: schema.objectCollection.id },
      readonly: true,
      documentation: parameter.documentation,
    }));
  }
  for (const { object, collectionId } of flattenObjects(schema.objectCollection)) {
    if (object.kind === "loopedActivity") {
      addLoopVariables(index, object, metadata);
    }
    if (object.kind === "actionActivity") {
      addActionOutputs(index, object, collectionId, metadata);
    }
  }
  for (const flow of collectFlowsRecursive(schema).filter((item): item is MicroflowSequenceFlow => item.kind === "sequence" && Boolean(item.isErrorHandler))) {
    addErrorContextVariables(index, schema, flow);
  }
  finalizeDiagnostics(index);
  return index;
}

export function getExistingVariableNames(_schema: MicroflowSchema, index: MicroflowVariableIndex): string[] {
  return Object.keys(index.byName ?? {});
}

export function isVariableNameDuplicate(_schema: MicroflowSchema, index: MicroflowVariableIndex, name: string, excludeSource?: MicroflowVariableSource): boolean {
  return (index.byName?.[name] ?? []).some(symbol => {
    if (!excludeSource) {
      return true;
    }
    return JSON.stringify(symbol.source) !== JSON.stringify(excludeSource);
  });
}

export function createUniqueVariableName(baseName: string, existingNames: Iterable<string>): string {
  const existing = new Set(existingNames);
  if (!existing.has(baseName)) {
    return baseName;
  }
  let index = 1;
  while (existing.has(`${baseName}${index}`)) {
    index += 1;
  }
  return `${baseName}${index}`;
}

export function validateOutputVariableName(name: string): MicroflowVariableDiagnostic[] {
  if (!name.trim()) {
    return [{ id: "MF_VARIABLE_NAME_REQUIRED", severity: "error", code: "MF_VARIABLE_NAME_REQUIRED", message: "Variable name is required.", variableName: name }];
  }
  if (name.startsWith("$") || !variableNamePattern.test(name)) {
    return [{ id: `MF_VARIABLE_NAME_INVALID:${name}`, severity: "error", code: "MF_VARIABLE_NAME_INVALID", message: `Variable "${name}" is not a valid output variable name.`, variableName: name }];
  }
  return [];
}
