import { createAnnotationFlow, createSequenceFlow, flattenObjectCollection } from "../../adapters";
import { inferEdgeKindFromPorts } from "../../node-registry";
import type {
  MicroflowCaseValue,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowObject,
  MicroflowSchema,
} from "../../schema";
import { connectionIndexFromPortId } from "./flowgram-port-factory";

export function defaultCaseValuesForPorts(sourcePort: MicroflowEditorPort, source?: MicroflowObject): MicroflowCaseValue[] {
  if (sourcePort.kind === "decisionOut") {
    const value = sourcePort.label !== "false";
    return [{ kind: "boolean", value, persistedValue: String(value) }];
  }
  if (sourcePort.kind === "objectTypeOut") {
    const value = sourcePort.label ?? "fallback";
    return [{ kind: value === "fallback" ? "fallback" : "inheritance", value, persistedValue: value }];
  }
  if (source?.kind === "exclusiveSplit" && source.splitCondition.resultType?.kind === "boolean") {
    return [{ kind: "boolean", value: true, persistedValue: "true" }];
  }
  return [];
}

export function createMicroflowFlowFromPorts(
  schema: MicroflowSchema,
  sourcePort: MicroflowEditorPort,
  targetPort: MicroflowEditorPort,
  options?: { caseValues?: MicroflowCaseValue[]; label?: string },
): MicroflowFlow {
  const objects = new Map(flattenObjectCollection(schema.objectCollection).map(object => [object.id, object]));
  const source = objects.get(sourcePort.objectId);
  const target = objects.get(targetPort.objectId);
  if (!source || !target) {
    throw new Error("Cannot create flow for missing source or target object.");
  }
  const edgeKind = inferEdgeKindFromPorts(source, target, sourcePort);
  if (edgeKind === "annotation") {
    return createAnnotationFlow({
      originObjectId: source.id,
      destinationObjectId: target.id,
      label: options?.label ?? "Annotation",
    });
  }
  const caseValues = options?.caseValues ?? defaultCaseValuesForPorts(sourcePort, source);
  return createSequenceFlow({
    originObjectId: source.id,
    destinationObjectId: target.id,
    originConnectionIndex: connectionIndexFromPortId(sourcePort.id),
    destinationConnectionIndex: connectionIndexFromPortId(targetPort.id),
    caseValues,
    isErrorHandler: edgeKind === "errorHandler",
    edgeKind,
    label: options?.label,
  });
}

