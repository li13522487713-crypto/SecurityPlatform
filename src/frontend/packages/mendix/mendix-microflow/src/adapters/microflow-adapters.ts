import type {
  MendixCompatMicroflow,
  MendixCompatDataType,
  MendixCompatPrimitiveDataType,
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
  MicroflowEdgeKind,
  MicroflowEditorGraph,
  MicroflowEditorGraphPatch,
  MicroflowEndEvent,
  MicroflowEventConfig,
  MicroflowErrorEvent,
  MicroflowExclusiveMerge,
  MicroflowExclusiveSplit,
  MicroflowExpression,
  MicroflowFlow,
  MicroflowInheritanceSplit,
  MicroflowLine,
  MicroflowLoopedActivity,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowObjectKind,
  MicroflowParameterObject,
  MicroflowPoint,
  MicroflowPort,
  MicroflowRuntimeDto,
  MicroflowSequenceFlow,
  MicroflowSize,
  MicroflowStartEvent,
  MicroflowTypeRef,
  MicroflowVariable,
  MicroflowVariableIndex
} from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { createPortId } from "../schema/utils/port-utils";
import { deriveMicroflowReturnVariableName } from "../schema/utils/microflow-signature";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex as buildVariableIndexV2 } from "../variables/variable-index";
import { microflowActionRegistryByKind } from "../node-registry/action-registry";
import { canonicalizeFlowLine } from "../flowgram/FlowGramMicroflowTypes";

const defaultLineStyle: MicroflowLine["style"] = {
  strokeType: "solid",
  strokeWidth: 2,
  arrow: "target"
};

const EMPTY_ROOT_COLLECTION: MicroflowObjectCollection = {
  id: "root-collection",
  officialType: "Microflows$MicroflowObjectCollection",
  objects: [],
  flows: [],
};

function normalizeCollection(collection: MicroflowObjectCollection | undefined, fallbackId = EMPTY_ROOT_COLLECTION.id): MicroflowObjectCollection {
  const safeId = (collection?.id?.trim() || fallbackId).trim() || fallbackId;
  return {
    id: safeId,
    officialType: collection?.officialType || EMPTY_ROOT_COLLECTION.officialType,
    objects: Array.isArray(collection?.objects)
      ? collection.objects.map(object =>
        object.kind === "loopedActivity"
          ? { ...object, objectCollection: normalizeCollection(object.objectCollection, `${object.id ?? "loop"}-collection`) }
          : object,
      )
      : [],
    flows: Array.isArray(collection?.flows) ? collection.flows : [],
  };
}

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
      },
      $currentSession: {
        name: "$currentSession",
        dataType: { kind: "object", entityQualifiedName: "System.Session" },
        source: { kind: "system", name: "$currentSession" },
        scope: { collectionId: "root" },
        readonly: true
      }
    }
  };
}

function buildVariableIndex(parameters: MicroflowAuthoringSchema["parameters"], collection: MicroflowObjectCollection | undefined, flows: MicroflowFlow[]): MicroflowVariableIndex {
  const safeCollection = normalizeCollection(collection);
  const index = emptyVariableIndex();
  const objectsById = new Map(flattenObjectCollection(safeCollection).map(object => [object.id, object] as const));
  for (const parameter of parameters) {
    index.parameters[parameter.name] = {
      name: parameter.name,
      dataType: parameter.dataType ?? toMicroflowDataType(parameter.type),
      source: { kind: "parameter", parameterId: parameter.id },
      scope: { collectionId: safeCollection.id },
      readonly: true
    };
  }
  for (const object of flattenObjectCollection(safeCollection)) {
    if (object.kind === "loopedActivity") {
      if (object.loopSource.kind === "iterableList" && object.loopSource.iteratorVariableName) {
        index.loopVariables[object.loopSource.iteratorVariableName] = {
          name: object.loopSource.iteratorVariableName,
          dataType: { kind: "unknown", reason: object.loopSource.listVariableName },
          source: { kind: "loopIterator", loopObjectId: object.id },
          scope: { collectionId: object.objectCollection?.id ?? safeCollection.id, loopObjectId: object.id },
          readonly: true
        };
      }
      index.systemVariables.$currentIndex = {
        name: "$currentIndex",
        dataType: { kind: "integer" },
        source: { kind: "system", name: "$currentIndex" },
        scope: { collectionId: object.objectCollection?.id ?? safeCollection.id, loopObjectId: object.id },
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
          scope: { collectionId: safeCollection.id, startObjectId: object.id },
          readonly: false
        };
      }
      if (action.kind === "createObject") {
        index.objectOutputs[action.outputVariableName] = {
          name: action.outputVariableName,
          dataType: { kind: "object", entityQualifiedName: action.entityQualifiedName },
          source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
          scope: { collectionId: safeCollection.id, startObjectId: object.id },
          readonly: false
        };
      }
      if (action.kind === "callMicroflow" && action.returnValue.storeResult && action.returnValue.outputVariableName) {
        index.localVariables[action.returnValue.outputVariableName] = {
          name: action.returnValue.outputVariableName,
          dataType: action.returnValue.dataType ?? { kind: "unknown", reason: "microflow return" },
          source: { kind: "actionOutput", objectId: object.id, actionId: action.id },
          scope: { collectionId: safeCollection.id, startObjectId: object.id },
          readonly: false
        };
      }
    }
  }
  const safeFlows = Array.isArray(flows) ? flows : [];
  for (const flow of safeFlows.filter((item): item is MicroflowSequenceFlow => item.kind === "sequence" && item.isErrorHandler)) {
    const sourceObject = objectsById.get(flow.originObjectId);
    index.errorVariables.$latestError = {
      name: "$latestError",
      dataType: { kind: "object", entityQualifiedName: "System.Error" },
      source: { kind: "errorContext", flowId: flow.id },
      scope: { collectionId: safeCollection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
      readonly: true
    };
    if (sourceObject?.kind === "actionActivity" && sourceObject.action.kind === "restCall") {
      index.errorVariables.$latestHttpResponse = {
        name: "$latestHttpResponse",
        dataType: { kind: "object", entityQualifiedName: "System.HttpResponse" },
        source: { kind: "errorContext", flowId: flow.id },
        scope: { collectionId: safeCollection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
        readonly: true
      };
    }
    if (sourceObject?.kind === "actionActivity" && sourceObject.action.kind === "webServiceCall") {
      index.errorVariables.$latestSoapFault = {
        name: "$latestSoapFault",
        dataType: { kind: "object", entityQualifiedName: "System.SoapFault" },
        source: { kind: "errorContext", flowId: flow.id },
        scope: { collectionId: safeCollection.id, errorHandlerFlowId: flow.id, startObjectId: flow.destinationObjectId },
        readonly: true
      };
    }
  }
  return index;
}

export function toMicroflowDataType(type?: MicroflowTypeRef): MicroflowDataType {
  if (!type) {
    return { kind: "unknown", reason: "missing type" };
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

export function toMicroflowTypeRef(type: MicroflowDataType): MicroflowTypeRef {
  if (type.kind === "void") {
    return { kind: "void", name: "Void" };
  }
  if (type.kind === "object") {
    return { kind: "entity", name: type.entityQualifiedName, entity: type.entityQualifiedName };
  }
  if (type.kind === "list") {
    return { kind: "list", name: "List", itemType: toMicroflowTypeRef(type.itemType) };
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

export function flattenObjectCollection(collection: MicroflowObjectCollection | undefined): MicroflowObject[] {
  const safeCollection = normalizeCollection(collection);
  return safeCollection.objects.flatMap(object => object.kind === "loopedActivity" ? [object, ...flattenObjectCollection(object.objectCollection)] : [object]);
}

function portsForObject(object: MicroflowObject): MicroflowPort[] {
  const input: MicroflowPort = { id: "in", label: "In", direction: "input", kind: "sequenceIn", cardinality: "one", edgeTypes: ["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"] };
  const output: MicroflowPort = { id: "out", label: "Out", direction: "output", kind: "sequenceOut", cardinality: "one", edgeTypes: ["sequence"] };
  const error: MicroflowPort = { id: "error", label: "Error", direction: "output", kind: "errorOut", cardinality: "zeroOrOne", edgeTypes: ["errorHandler"] };
  const loopBodyIn: MicroflowPort = { id: "bodyIn", label: "Body In", direction: "output", kind: "loopBodyIn", cardinality: "one", edgeTypes: ["loopBody"] };
  const loopBodyOut: MicroflowPort = { id: "bodyOut", label: "Body Out", direction: "input", kind: "loopBodyOut", cardinality: "zeroOrMore", edgeTypes: ["sequence"] };
  if (object.kind === "startEvent") {
    return [output];
  }
  if (["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(object.kind)) {
    return [input];
  }
  if (object.kind === "exclusiveSplit") {
    if (object.splitCondition.resultType === "enumeration") {
      return [input, { id: "case", label: "Case", direction: "output", kind: "decisionOut", cardinality: "oneOrMore", edgeTypes: ["decisionCondition"] }, error];
    }
    return [input, { id: "true", label: "True", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] }, { id: "false", label: "False", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] }, error];
  }
  if (object.kind === "inheritanceSplit") {
    return [input, { id: "objectType", label: "Object Type", direction: "output", kind: "objectTypeOut", cardinality: "oneOrMore", edgeTypes: ["objectTypeCondition"] }, error];
  }
  if (object.kind === "parameterObject" || object.kind === "annotation") {
    return [{ id: "note", label: "Note", direction: "output", kind: "annotation", cardinality: "zeroOrMore", edgeTypes: ["annotation"] }];
  }
  if (object.kind === "loopedActivity") {
    return [input, output, loopBodyIn, loopBodyOut, error];
  }
  if (object.kind === "actionActivity") {
    const supportsErrorHandling = microflowActionRegistryByKind.get(object.action.kind)?.supportsErrorHandling ?? true;
    return supportsErrorHandling ? [input, output, error] : [input, output];
  }
  return [input, output];
}

export function toMendixCompatDataType(type: MicroflowDataType): MendixCompatDataType {
  if (type.kind === "enumeration") {
    return { $Type: "DataTypes$EnumerationType", enumerationQualifiedName: type.enumerationQualifiedName };
  }
  if (type.kind === "object") {
    return { $Type: "DataTypes$ObjectType", entityQualifiedName: type.entityQualifiedName };
  }
  if (type.kind === "list") {
    return { $Type: "DataTypes$ListType", itemType: toMendixCompatDataType(type.itemType) };
  }
  if (type.kind === "fileDocument") {
    return { $Type: "DataTypes$FileDocumentType", entityQualifiedName: type.entityQualifiedName };
  }
  const primitiveMap: Record<Exclude<MicroflowDataType["kind"], "enumeration" | "object" | "list" | "fileDocument">, MendixCompatDataType & { $Type: "DataTypes$PrimitiveType" }> = {
    void: { $Type: "DataTypes$PrimitiveType", primitive: "Void" },
    boolean: { $Type: "DataTypes$PrimitiveType", primitive: "Boolean" },
    integer: { $Type: "DataTypes$PrimitiveType", primitive: "Integer" },
    long: { $Type: "DataTypes$PrimitiveType", primitive: "Long" },
    decimal: { $Type: "DataTypes$PrimitiveType", primitive: "Decimal" },
    string: { $Type: "DataTypes$PrimitiveType", primitive: "String" },
    dateTime: { $Type: "DataTypes$PrimitiveType", primitive: "DateTime" },
    binary: { $Type: "DataTypes$PrimitiveType", primitive: "Binary" },
    json: { $Type: "DataTypes$PrimitiveType", primitive: "Json" },
    unknown: { $Type: "DataTypes$PrimitiveType", primitive: "Unknown" }
  };
  return primitiveMap[type.kind];
}

export function fromMendixCompatDataType(type: MendixCompatDataType): MicroflowDataType {
  if (type.$Type === "DataTypes$MicroflowDataType") {
    return type.authoringType;
  }
  if (type.$Type === "DataTypes$EnumerationType") {
    return { kind: "enumeration", enumerationQualifiedName: type.enumerationQualifiedName };
  }
  if (type.$Type === "DataTypes$ObjectType") {
    return { kind: "object", entityQualifiedName: type.entityQualifiedName };
  }
  if (type.$Type === "DataTypes$ListType") {
    return { kind: "list", itemType: fromMendixCompatDataType(type.itemType) };
  }
  if (type.$Type === "DataTypes$FileDocumentType") {
    return { kind: "fileDocument", entityQualifiedName: type.entityQualifiedName };
  }
  const primitiveMap: Record<MendixCompatPrimitiveDataType, MicroflowDataType> = {
    Void: { kind: "void" },
    Boolean: { kind: "boolean" },
    Integer: { kind: "integer" },
    Long: { kind: "long" },
    Decimal: { kind: "decimal" },
    String: { kind: "string" },
    DateTime: { kind: "dateTime" },
    Binary: { kind: "binary" },
    Json: { kind: "json" },
    Unknown: { kind: "unknown", reason: "compat primitive" }
  };
  const primitive = (type as { primitive?: keyof typeof primitiveMap }).primitive ?? "Unknown";
  return primitiveMap[primitive];
}

function portPosition(object: MicroflowObject, port: MicroflowPort, index: number): MicroflowPoint {
  const width = object.size.width;
  const height = object.size.height;
  if (port.kind === "sequenceIn" || port.kind === "loopBodyOut") {
    return { x: 0, y: height / 2 };
  }
  if (port.kind === "errorOut") {
    return { x: width, y: Math.max(18, height - 16) };
  }
  if (port.kind === "decisionOut") {
    const decisionOutputs = portsForObject(object).filter(item => item.kind === "decisionOut");
    const decisionIndex = Math.max(0, decisionOutputs.findIndex(item => item.id === port.id));
    return { x: width, y: ((decisionIndex + 1) * height) / (decisionOutputs.length + 1) };
  }
  if (port.kind === "objectTypeOut" || port.kind === "loopBodyIn") {
    return { x: width, y: Math.max(18, height / 2 - 18 + index * 12) };
  }
  if (port.kind === "annotation") {
    return { x: width, y: height / 2 };
  }
  return { x: width, y: height / 2 };
}

export function findMicroflowObject(collection: MicroflowObjectCollection | undefined, objectId: string): MicroflowObject | undefined {
  const safeCollection = normalizeCollection(collection);
  for (const object of safeCollection.objects) {
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

export function toEditorGraph(schema: MicroflowAuthoringSchema): MicroflowEditorGraph {
  const objects = collectEditorObjects(schema.objectCollection);
  const objectById = new Map(objects.map(entry => [entry.object.id, entry.object]));
  const issues = "validation" in schema ? schema.validation.issues : [];
  return {
    nodes: objects.map(entry => ({
      id: `node-${entry.object.id}`,
      objectId: entry.object.id,
      kind: entry.object.kind,
      nodeKind: entry.object.kind,
      activityKind: entry.object.kind === "actionActivity" ? entry.object.action.kind : undefined,
      title: entry.object.caption ?? entry.object.id,
      subtitle: entry.object.officialType,
      iconKey: entry.object.editor.iconKey ?? entry.object.kind,
      position: entry.object.relativeMiddlePoint,
      size: entry.object.size,
      ports: portsForObject(entry.object).map((port, index) => ({
        id: createPortId(entry.object.id, port.kind, index),
        objectId: entry.object.id,
        label: port.label,
        direction: port.direction,
        kind: port.kind,
        connectionIndex: index,
        cardinality: port.cardinality,
        position: portPosition(entry.object, port, index),
        edgeTypes: port.edgeTypes
      })),
      parentObjectId: entry.parentObjectId,
      collectionId: entry.collectionId,
      state: {
        selected: schema.editor.selection.objectId === entry.object.id || (schema.editor.selection.objectIds ?? []).includes(entry.object.id),
        disabled: Boolean(entry.object.disabled),
        hasError: issues.some(issue => issue.severity === "error" && (issue.objectId === entry.object.id || issue.nodeId === entry.object.id)),
        hasWarning: issues.some(issue => issue.severity === "warning" && (issue.objectId === entry.object.id || issue.nodeId === entry.object.id))
      }
    })),
    edges: collectFlowsRecursive(schema).map(flow => {
      const source = objectById.get(flow.originObjectId);
      const target = objectById.get(flow.destinationObjectId);
      const sourcePorts = source ? portsForObject(source) : [];
      const targetPorts = target ? portsForObject(target) : [];
      const sourcePort = flow.kind === "annotation"
        ? sourcePorts.find(port => port.kind === "annotation" && port.direction === "output") ?? sourcePorts[flow.originConnectionIndex ?? 0]
        : flow.isErrorHandler
          ? sourcePorts.find(port => port.kind === "errorOut")
          : sourcePorts[flow.originConnectionIndex ?? 0] ?? sourcePorts.find(port => port.direction === "output");
      const targetPort = flow.kind === "annotation"
        ? targetPorts.find(port => port.kind === "annotation" && port.direction === "input") ?? targetPorts[flow.destinationConnectionIndex ?? 0]
        : targetPorts[flow.destinationConnectionIndex ?? 0] ?? targetPorts.find(port => port.direction === "input");
      const sourceConnectionIndex = sourcePort ? sourcePorts.indexOf(sourcePort) : flow.originConnectionIndex ?? 0;
      const targetConnectionIndex = targetPort ? targetPorts.indexOf(targetPort) : flow.destinationConnectionIndex ?? 0;
      return {
        id: `edge-${flow.id}`,
        flowId: flow.id,
        kind: flow.kind === "annotation" ? "annotation" : flow.editor.edgeKind,
        sourceNodeId: `node-${flow.originObjectId}`,
        sourceObjectId: flow.originObjectId,
        targetNodeId: `node-${flow.destinationObjectId}`,
        targetObjectId: flow.destinationObjectId,
        sourcePortId: createPortId(flow.originObjectId, sourcePort?.kind ?? "sequenceOut", sourceConnectionIndex),
        targetPortId: createPortId(flow.destinationObjectId, targetPort?.kind ?? "sequenceIn", targetConnectionIndex),
        edgeKind: flow.kind === "annotation" ? "annotation" : flow.editor.edgeKind,
        label: flow.kind === "annotation" ? flow.editor.label : flow.editor.label,
        style: {
          strokeType: flow.line.style.strokeType,
          colorToken: flow.kind === "sequence" && flow.isErrorHandler ? "#f93920" : flow.kind === "annotation" ? "#86909c" : "#4e5969",
          arrow: flow.line.style.arrow === "target" || flow.line.style.arrow === "both"
        },
        state: {
          selected: schema.editor.selection.flowId === flow.id || (schema.editor.selection.flowIds ?? []).includes(flow.id),
          hasError: issues.some(issue => issue.flowId === flow.id || issue.edgeId === flow.id),
          runtimeVisited: false
        }
      };
    }),
    viewport: schema.editor.viewport,
    selection: schema.editor.selection
  };
}

function collectEditorObjects(
  collection: MicroflowObjectCollection | undefined,
  parentObjectId?: string
): Array<{ object: MicroflowObject; collectionId: string; parentObjectId?: string }> {
  const safeCollection = normalizeCollection(collection);
  return safeCollection.objects.flatMap(object => [
    { object, collectionId: safeCollection.id, parentObjectId },
    ...(object.kind === "loopedActivity" ? collectEditorObjects(object.objectCollection, object.id) : [])
  ]);
}

function mapObject(collection: MicroflowObjectCollection | undefined, objectId: string, mapper: (object: MicroflowObject) => MicroflowObject): MicroflowObjectCollection {
  const safeCollection = normalizeCollection(collection);
  return {
    ...safeCollection,
    objects: safeCollection.objects.map(object => {
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

export function applyEditorGraphPatch(schema: MicroflowAuthoringSchema, patch: MicroflowEditorGraphPatch): MicroflowAuthoringSchema {
  let objectCollection = schema.objectCollection;
  for (const moved of patch.movedNodes ?? []) {
    objectCollection = mapObject(objectCollection, moved.objectId, object => ({ ...object, relativeMiddlePoint: moved.position }));
  }
  for (const resized of patch.resizedNodes ?? []) {
    objectCollection = mapObject(objectCollection, resized.objectId, object => ({ ...object, size: resized.size }));
  }
  const safeFlows = Array.isArray(schema.flows) ? schema.flows : [];
  const flows = safeFlows.map(flow => {
    const update = patch.updatedFlows?.find(item => item.flowId === flow.id);
    if (!update) {
      return flow;
    }
    const nextLine = canonicalizeFlowLine(update.line, flow.line);
    if (flow.kind === "annotation") {
      return { ...flow, line: nextLine, editor: { ...flow.editor, label: update.label ?? flow.editor.label } };
    }
    return { ...flow, line: nextLine, editor: { ...flow.editor, label: update.label ?? flow.editor.label } };
  });
  const hasSelectedObject = Object.prototype.hasOwnProperty.call(patch, "selectedObjectId");
  const hasSelectedFlow = Object.prototype.hasOwnProperty.call(patch, "selectedFlowId");
  return {
    ...schema,
    objectCollection,
    flows,
    editor: {
      ...schema.editor,
      viewport: patch.viewport ?? schema.editor.viewport,
      selection: {
        objectId: patch.selectedObjectId ?? schema.editor.selection.objectId,
        flowId: patch.selectedFlowId ?? schema.editor.selection.flowId,
        collectionId: patch.selectedCollectionId ?? schema.editor.selection.collectionId,
        objectIds: patch.selectedObjectIds ?? (hasSelectedObject ? (patch.selectedObjectId ? [patch.selectedObjectId] : []) : schema.editor.selection.objectIds ?? []),
        flowIds: patch.selectedFlowIds ?? (hasSelectedFlow ? (patch.selectedFlowId ? [patch.selectedFlowId] : []) : schema.editor.selection.flowIds ?? []),
        mode: patch.selectionMode ?? schema.editor.selection.mode
      }
    }
  };
}

export function toMendixCompat(schema: MicroflowAuthoringSchema): MendixCompatMicroflow {
  const safeObjectCollection = normalizeCollection(schema.objectCollection);
  const safeFlows = Array.isArray(schema.flows) ? schema.flows : [];
  const returnVariableName = deriveMicroflowReturnVariableName(schema) ?? "";
  return {
    $ID: schema.id,
    $Type: "Microflows$Microflow",
    $UnitID: schema.moduleId,
    name: schema.name,
    documentation: schema.documentation ?? "",
    parameters: schema.parameters,
    microflowReturnType: toMendixCompatDataType(schema.returnType),
    returnVariableName,
    objectCollection: safeObjectCollection,
    flows: safeFlows,
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
  const safeObjectCollection = normalizeCollection(input.objectCollection);
  const safeParameters = input.parameters ?? [];
  const safeFlows = Array.isArray(input.flows) ? input.flows : [];
  const normalizedFlows = safeFlows.map(flow => ({
    ...flow,
    line: canonicalizeFlowLine(flow.line, {
      kind: "orthogonal",
      points: [],
      routing: { mode: "auto", bendPoints: [] },
      style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
    }),
  }));
  const variables = buildVariableIndex(safeParameters, safeObjectCollection, normalizedFlows);
  const schema: MicroflowAuthoringSchema = {
    schemaVersion: "1.0.0",
    mendixProfile: "mx11",
    id: input.$ID,
    stableId: input.stableId ?? input.$ID,
    name: input.name,
    displayName: input.name,
    documentation: input.documentation,
    moduleId: input.$UnitID ?? "default",
    parameters: safeParameters,
    returnType: fromMendixCompatDataType(input.microflowReturnType),
    returnVariableName: input.returnVariableName,
    objectCollection: safeObjectCollection,
    flows: normalizedFlows,
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
    variables,
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
    audit: { version: "v1", status: input.excluded ? "archived" : "draft" }
  };
  const derivedReturnVariableName = deriveMicroflowReturnVariableName(schema);
  return derivedReturnVariableName === undefined
    ? schema
    : {
      ...schema,
      returnVariableName: derivedReturnVariableName,
    };
}

export function toRuntimeDto(schema: MicroflowAuthoringSchema): MicroflowRuntimeDto {
  const safeObjectCollection = normalizeCollection(schema.objectCollection);
  const safeFlows = Array.isArray(schema.flows) ? schema.flows : [];
  const existingVariables = schema.variables;
  const hasExistingVariables = Boolean(existingVariables?.all?.length);
  return {
    microflowId: schema.id,
    schemaVersion: schema.schemaVersion,
    name: schema.name,
    returnType: schema.returnType,
    parameters: schema.parameters ?? [],
    objectCollection: safeObjectCollection,
    flows: safeFlows.filter(flow => flow.kind === "sequence"),
    variables: hasExistingVariables && existingVariables ? existingVariables : buildVariableIndexV2(schema, EMPTY_MICROFLOW_METADATA_CATALOG),
    p0RuntimeActionBlocks: []
  };
}
