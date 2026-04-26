import type {
  MicroflowEditorEdge,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowValidationIssue
} from "../schema/types";
import { objectLocationMap } from "../validators/shared";

export type MicroflowFlowRegistryKind = "sequence" | "annotation";
export type MicroflowDerivedEdgeKind = "sequence" | "decisionCondition" | "objectTypeCondition" | "errorHandler" | "annotation";
export type MicroflowEditorEdgeKind = MicroflowDerivedEdgeKind;

export interface MicroflowConnectionCheckResult {
  allowed: boolean;
  reasonCode?: string;
  message?: string;
  suggestedEdgeKind?: MicroflowEditorEdgeKind;
}

export interface MicroflowPortConnection {
  sourcePort: MicroflowEditorPort;
  targetPort: MicroflowEditorPort;
}

export interface MicroflowEdgeRegistryItem {
  key: MicroflowDerivedEdgeKind;
  edgeKind: MicroflowDerivedEdgeKind;
  kind: MicroflowFlowRegistryKind;
  officialType: "Microflows$SequenceFlow" | "Microflows$AnnotationFlow";
  title: string;
  titleZh: string;
  description: string;
  runtimeEffect: "controlFlow" | "errorFlow" | "annotationOnly";
  lineStyle: "solid" | "dashed" | "dotted";
  colorToken: string;
  hasArrow: boolean;
  labelMode: "none" | "auto" | "editable" | "condition";
  sourcePortKinds: MicroflowEditorPort["kind"][];
  targetPortKinds: MicroflowEditorPort["kind"][];
  sourceObjectKinds?: MicroflowObject["kind"][];
  targetObjectKinds?: MicroflowObject["kind"][];
  propertyTabs: Array<"properties" | "documentation" | "errorHandling">;
  derivedKinds: MicroflowDerivedEdgeKind[];
  validateConnection: (connection: MicroflowPortConnection) => MicroflowConnectionCheckResult;
  validate: (flow: MicroflowFlow, context: { flows: MicroflowFlow[] }) => MicroflowValidationIssue[];
  validateFlow: (flow: MicroflowFlow, context: { flows: MicroflowFlow[] }) => MicroflowValidationIssue[];
  toRuntimeDto: (flow: MicroflowFlow) => { kind: "edge"; edgeKind: MicroflowDerivedEdgeKind; officialType: string; unsupported: true };
}

function issue(code: string, message: string, flowId: string): MicroflowValidationIssue {
  return { id: `${code}:${flowId}`, code, severity: "error", message, flowId };
}

function validateSequence(flow: MicroflowFlow): MicroflowValidationIssue[] {
  if (flow.kind !== "sequence") {
    return [issue("MF_FLOW_KIND_INVALID", "Expected sequence flow.", flow.id)];
  }
  const issues: MicroflowValidationIssue[] = [];
  if (!flow.originObjectId || !flow.destinationObjectId) {
    issues.push(issue("MF_FLOW_ENDPOINT_REQUIRED", "SequenceFlow requires origin and destination object ids.", flow.id));
  }
  if (flow.isErrorHandler && flow.editor.edgeKind !== "errorHandler") {
    issues.push(issue("MF_FLOW_ERROR_KIND_MISMATCH", "Error handler SequenceFlow must use editor.edgeKind=errorHandler.", flow.id));
  }
  if (flow.editor.edgeKind === "errorHandler" && !flow.isErrorHandler) {
    issues.push(issue("MF_FLOW_ERROR_FLAG_REQUIRED", "edgeKind=errorHandler requires isErrorHandler=true.", flow.id));
  }
  return issues;
}

function validateAnnotation(flow: MicroflowFlow): MicroflowValidationIssue[] {
  if (flow.kind !== "annotation") {
    return [issue("MF_FLOW_KIND_INVALID", "Expected annotation flow.", flow.id)];
  }
  const issues: MicroflowValidationIssue[] = [];
  if (!flow.originObjectId || !flow.destinationObjectId) {
    issues.push(issue("MF_FLOW_ENDPOINT_REQUIRED", "AnnotationFlow requires origin and destination object ids.", flow.id));
  }
  return issues;
}

export const defaultMicroflowEdgeRegistry: MicroflowEdgeRegistryItem[] = [
  {
    key: "sequence",
    edgeKind: "sequence",
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    title: "Sequence Flow",
    titleZh: "顺序流",
    description: "Official execution flow. Derived edge kinds are editor-only metadata.",
    runtimeEffect: "controlFlow",
    lineStyle: "solid",
    colorToken: "#4e5969",
    hasArrow: true,
    labelMode: "none",
    sourcePortKinds: ["sequenceOut", "loopOut"],
    targetPortKinds: ["sequenceIn", "loopIn"],
    propertyTabs: ["properties", "documentation"],
    derivedKinds: ["sequence"],
    validateConnection: () => ok("sequence"),
    validate: (flow: MicroflowFlow) => validateSequence(flow),
    validateFlow: (flow: MicroflowFlow) => validateSequence(flow),
    toRuntimeDto: () => ({ kind: "edge", edgeKind: "sequence", officialType: "Microflows$SequenceFlow", unsupported: true })
  },
  {
    key: "decisionCondition",
    edgeKind: "decisionCondition",
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    title: "Decision Condition",
    titleZh: "决策条件流",
    description: "SequenceFlow with caseValues for exclusive split branches.",
    runtimeEffect: "controlFlow",
    lineStyle: "solid",
    colorToken: "#165dff",
    hasArrow: true,
    labelMode: "condition",
    sourcePortKinds: ["decisionOut"],
    targetPortKinds: ["sequenceIn", "loopIn"],
    propertyTabs: ["properties", "documentation"],
    derivedKinds: ["decisionCondition"],
    validateConnection: () => ok("decisionCondition"),
    validate: (flow: MicroflowFlow) => flow.kind === "sequence" && flow.caseValues.length === 0 ? [issue("MF_FLOW_CASE_VALUES_RESERVED", "Decision condition keeps caseValues for branch selection.", flow.id)] : validateSequence(flow),
    validateFlow: (flow: MicroflowFlow) => flow.kind === "sequence" && flow.caseValues.length === 0 ? [issue("MF_FLOW_CASE_VALUES_RESERVED", "Decision condition keeps caseValues for branch selection.", flow.id)] : validateSequence(flow),
    toRuntimeDto: () => ({ kind: "edge", edgeKind: "decisionCondition", officialType: "Microflows$SequenceFlow", unsupported: true })
  },
  {
    key: "objectTypeCondition",
    edgeKind: "objectTypeCondition",
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    title: "Object Type Condition",
    titleZh: "对象类型条件流",
    description: "SequenceFlow with inheritance/empty/fallback caseValues.",
    runtimeEffect: "controlFlow",
    lineStyle: "solid",
    colorToken: "#722ed1",
    hasArrow: true,
    labelMode: "condition",
    sourcePortKinds: ["objectTypeOut"],
    targetPortKinds: ["sequenceIn", "loopIn"],
    propertyTabs: ["properties", "documentation"],
    derivedKinds: ["objectTypeCondition"],
    validateConnection: () => ok("objectTypeCondition"),
    validate: (flow: MicroflowFlow) => flow.kind === "sequence" && flow.caseValues.length === 0 ? [issue("MF_FLOW_CASE_VALUES_RESERVED", "Object type condition keeps caseValues for branch selection.", flow.id)] : validateSequence(flow),
    validateFlow: (flow: MicroflowFlow) => flow.kind === "sequence" && flow.caseValues.length === 0 ? [issue("MF_FLOW_CASE_VALUES_RESERVED", "Object type condition keeps caseValues for branch selection.", flow.id)] : validateSequence(flow),
    toRuntimeDto: () => ({ kind: "edge", edgeKind: "objectTypeCondition", officialType: "Microflows$SequenceFlow", unsupported: true })
  },
  {
    key: "errorHandler",
    edgeKind: "errorHandler",
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    title: "Error Handler",
    titleZh: "错误处理流",
    description: "SequenceFlow marked with isErrorHandler=true.",
    runtimeEffect: "errorFlow",
    lineStyle: "dashed",
    colorToken: "#f93920",
    hasArrow: true,
    labelMode: "auto",
    sourcePortKinds: ["errorOut"],
    targetPortKinds: ["sequenceIn"],
    propertyTabs: ["properties", "documentation", "errorHandling"],
    derivedKinds: ["errorHandler"],
    validateConnection: () => ok("errorHandler"),
    validate: (flow: MicroflowFlow) => flow.kind === "sequence" && !flow.isErrorHandler ? [issue("MF_FLOW_ERROR_FLAG_REQUIRED", "Error handler SequenceFlow requires isErrorHandler=true.", flow.id)] : validateSequence(flow),
    validateFlow: (flow: MicroflowFlow) => flow.kind === "sequence" && !flow.isErrorHandler ? [issue("MF_FLOW_ERROR_FLAG_REQUIRED", "Error handler SequenceFlow requires isErrorHandler=true.", flow.id)] : validateSequence(flow),
    toRuntimeDto: () => ({ kind: "edge", edgeKind: "errorHandler", officialType: "Microflows$SequenceFlow", unsupported: true })
  },
  {
    key: "annotation",
    edgeKind: "annotation",
    kind: "annotation",
    officialType: "Microflows$AnnotationFlow",
    title: "Annotation Flow",
    titleZh: "注释流",
    description: "Official documentation flow without runtime behavior.",
    runtimeEffect: "annotationOnly",
    lineStyle: "dashed",
    colorToken: "#86909c",
    hasArrow: false,
    labelMode: "editable",
    sourcePortKinds: ["annotation"],
    targetPortKinds: ["annotation", "sequenceIn", "sequenceOut"],
    propertyTabs: ["properties", "documentation"],
    derivedKinds: ["annotation"],
    validateConnection: () => ok("annotation"),
    validate: (flow: MicroflowFlow) => validateAnnotation(flow),
    validateFlow: (flow: MicroflowFlow) => validateAnnotation(flow),
    toRuntimeDto: () => ({ kind: "edge", edgeKind: "annotation", officialType: "Microflows$AnnotationFlow", unsupported: true })
  }
];

const registryByKind = new Map(defaultMicroflowEdgeRegistry.map(item => [item.kind, item]));

export function getMicroflowEdgeRegistryItem(kind: MicroflowFlowRegistryKind): MicroflowEdgeRegistryItem | undefined {
  return registryByKind.get(kind);
}

export function deriveEdgeKind(flow: MicroflowFlow): MicroflowDerivedEdgeKind {
  if (flow.kind === "annotation") {
    return "annotation";
  }
  if (flow.isErrorHandler) {
    return "errorHandler";
  }
  return flow.editor.edgeKind;
}

export function edgeStyleByKind(kind: MicroflowDerivedEdgeKind): MicroflowEditorEdge["style"] {
  if (kind === "annotation") {
    return { strokeType: "dashed", colorToken: "#86909c", arrow: false };
  }
  if (kind === "errorHandler") {
    return { strokeType: "dashed", colorToken: "#f93920", arrow: true };
  }
  if (kind === "decisionCondition") {
    return { strokeType: "solid", colorToken: "#165dff", arrow: true };
  }
  if (kind === "objectTypeCondition") {
    return { strokeType: "solid", colorToken: "#722ed1", arrow: true };
  }
  return { strokeType: "solid", colorToken: "#4e5969", arrow: true };
}

function objectById(schema: MicroflowSchema): Map<string, MicroflowObject> {
  return new Map(flattenObjects(schema.objectCollection).map(object => [object.id, object]));
}

function flattenObjects(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object => object.kind === "loopedActivity" ? [object, ...flattenObjects(object.objectCollection)] : [object]);
}

function fail(reasonCode: string, message: string, suggestedEdgeKind?: MicroflowEditorEdgeKind): MicroflowConnectionCheckResult {
  return { allowed: false, reasonCode, message, suggestedEdgeKind };
}

function ok(suggestedEdgeKind: MicroflowEditorEdgeKind): MicroflowConnectionCheckResult {
  return { allowed: true, suggestedEdgeKind };
}

function isTerminalObject(object: MicroflowObject): boolean {
  return ["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(object.kind);
}

function supportsErrorFlow(object: MicroflowObject): boolean {
  return ["actionActivity", "loopedActivity", "exclusiveSplit", "inheritanceSplit"].includes(object.kind);
}

function hasIncoming(schema: MicroflowSchema, port: MicroflowEditorPort): boolean {
  return schema.flows.some(flow => flow.destinationObjectId === port.objectId && (flow.destinationConnectionIndex ?? 0) === port.connectionIndex);
}

function hasOutgoing(schema: MicroflowSchema, port: MicroflowEditorPort): boolean {
  return schema.flows.some(flow => flow.originObjectId === port.objectId && (flow.originConnectionIndex ?? 0) === port.connectionIndex);
}

function hasDecisionCase(schema: MicroflowSchema, sourceObjectId: string, value: boolean): boolean {
  return schema.flows.some(flow =>
    flow.kind === "sequence" &&
    flow.originObjectId === sourceObjectId &&
    flow.editor.edgeKind === "decisionCondition" &&
    flow.caseValues.some(caseValue => caseValue.kind === "boolean" && caseValue.value === value)
  );
}

function hasInheritanceCase(schema: MicroflowSchema, sourceObjectId: string, entityQualifiedName: string): boolean {
  return schema.flows.some(flow =>
    flow.kind === "sequence" &&
    flow.originObjectId === sourceObjectId &&
    flow.editor.edgeKind === "objectTypeCondition" &&
    flow.caseValues.some(caseValue => caseValue.kind === "inheritance" && caseValue.entityQualifiedName === entityQualifiedName)
  );
}

export function inferEdgeKindFromPorts(sourceObject: MicroflowObject, targetObject: MicroflowObject, sourcePort: MicroflowEditorPort): MicroflowEditorEdgeKind {
  if (sourceObject.kind === "annotation" || targetObject.kind === "annotation" || sourcePort.kind === "annotation") {
    return "annotation";
  }
  if (sourcePort.kind === "errorOut") {
    return "errorHandler";
  }
  if (sourceObject.kind === "exclusiveSplit" || sourcePort.kind === "decisionOut") {
    return "decisionCondition";
  }
  if (sourceObject.kind === "inheritanceSplit" || sourcePort.kind === "objectTypeOut") {
    return "objectTypeCondition";
  }
  return "sequence";
}

export function canConnectPorts(schema: MicroflowSchema, sourcePort: MicroflowEditorPort, targetPort: MicroflowEditorPort): MicroflowConnectionCheckResult {
  if (sourcePort.direction !== "output") {
    return fail("MF_CONNECT_SOURCE_DIRECTION", "Source port must be an output port.");
  }
  if (targetPort.direction !== "input") {
    return fail("MF_CONNECT_TARGET_DIRECTION", "Target port must be an input port.");
  }
  if (sourcePort.id === targetPort.id || sourcePort.objectId === targetPort.objectId) {
    return fail("MF_CONNECT_SELF_LOOP", "Self connections are not supported yet.");
  }
  const objects = objectById(schema);
  const source = objects.get(sourcePort.objectId);
  const target = objects.get(targetPort.objectId);
  if (!source || !target) {
    return fail("MF_CONNECT_OBJECT_MISSING", "Source or target object does not exist.");
  }
  const edgeKind = inferEdgeKindFromPorts(source, target, sourcePort);
  const locations = objectLocationMap(schema);
  const sourceLocation = locations.get(source.id);
  const targetLocation = locations.get(target.id);
  if (sourceLocation && targetLocation && sourceLocation.collectionId !== targetLocation.collectionId) {
    return fail(
      "MF_CONNECT_LOOP_BOUNDARY",
      "Flows cannot directly cross Loop objectCollection boundaries.",
      edgeKind
    );
  }
  if (target.kind === "startEvent") {
    return fail("MF_CONNECT_START_TARGET", "StartEvent cannot have incoming flows.", edgeKind);
  }
  if (isTerminalObject(source)) {
    return fail("MF_CONNECT_TERMINAL_SOURCE", "Terminal events cannot have outgoing flows.", edgeKind);
  }
  if (source.kind === "parameterObject" || target.kind === "parameterObject") {
    return fail("MF_CONNECT_PARAMETER_SEQUENCE", "ParameterObject cannot participate in execution flows.", edgeKind);
  }
  if (edgeKind === "annotation") {
    return source.kind === "annotation" || target.kind === "annotation"
      ? ok("annotation")
      : fail("MF_CONNECT_ANNOTATION_ENDPOINT", "AnnotationFlow must connect to at least one Annotation.", "annotation");
  }
  if (source.kind === "annotation" || target.kind === "annotation") {
    return fail("MF_CONNECT_ANNOTATION_SEQUENCE", "Annotation can only use AnnotationFlow.", edgeKind);
  }
  if (target.kind === "errorEvent" && edgeKind !== "errorHandler") {
    return fail("MF_CONNECT_ERROR_EVENT_TARGET", "ErrorEvent can only be reached from an error handler flow.", edgeKind);
  }
  if (edgeKind === "errorHandler" && !supportsErrorFlow(source)) {
    return fail("MF_CONNECT_ERROR_UNSUPPORTED", "Source object does not support custom error handling.", edgeKind);
  }
  if (edgeKind !== "annotation" && sourcePort.cardinality === "one" && hasOutgoing(schema, sourcePort)) {
    return fail("MF_CONNECT_SOURCE_CARDINALITY", "Source port already has an outgoing flow.", edgeKind);
  }
  if (edgeKind !== "annotation" && targetPort.cardinality === "one" && hasIncoming(schema, targetPort)) {
    return fail("MF_CONNECT_TARGET_CARDINALITY", "Target port already has an incoming flow.", edgeKind);
  }
  if (edgeKind === "decisionCondition" && source.kind === "exclusiveSplit") {
    const label = sourcePort.label.toLowerCase();
    if ((label === "true" && hasDecisionCase(schema, source.id, true)) || (label === "false" && hasDecisionCase(schema, source.id, false))) {
      return fail("MF_CONNECT_DECISION_CASE_DUPLICATED", `Decision case ${sourcePort.label} already exists.`, edgeKind);
    }
  }
  if (edgeKind === "objectTypeCondition" && source.kind === "inheritanceSplit") {
    const entity = source.entity.allowedSpecializations[sourcePort.connectionIndex] ?? sourcePort.label;
    if (entity && hasInheritanceCase(schema, source.id, entity)) {
      return fail("MF_CONNECT_OBJECT_TYPE_CASE_DUPLICATED", `Object type case ${entity} already exists.`, edgeKind);
    }
  }
  return ok(edgeKind);
}
