import { createAnnotationFlow, createSequenceFlow, flattenObjectCollection } from "../../adapters";
import { inferEdgeKindFromPorts } from "../../node-registry";
import type {
  MicroflowCaseValue,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowObject,
  MicroflowSchema,
} from "../../schema";
import { booleanCaseValue, fallbackCaseValue, inheritanceCaseValue } from "./flowgram-case-options";

export function defaultCaseValuesForPorts(sourcePort: MicroflowEditorPort, source?: MicroflowObject): MicroflowCaseValue[] {
  if (sourcePort.kind === "decisionOut") {
    const value = sourcePort.label !== "false";
    return [booleanCaseValue(value)];
  }
  if (sourcePort.kind === "objectTypeOut") {
    const value = sourcePort.label ?? "fallback";
    return [value === "fallback" ? fallbackCaseValue() : inheritanceCaseValue(value)];
  }
  if (source?.kind === "exclusiveSplit" && source.splitCondition.resultType === "boolean") {
    return [booleanCaseValue(true)];
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
    return {
      ...createAnnotationFlow({
      originObjectId: source.id,
      destinationObjectId: target.id,
      label: options?.label ?? "Annotation",
      }),
      originConnectionIndex: sourcePort.connectionIndex,
      destinationConnectionIndex: targetPort.connectionIndex,
    };
  }
  const caseValues = options?.caseValues ?? defaultCaseValuesForPorts(sourcePort, source);
  return createSequenceFlow({
    originObjectId: source.id,
    destinationObjectId: target.id,
    originConnectionIndex: sourcePort.connectionIndex,
    destinationConnectionIndex: targetPort.connectionIndex,
    caseValues,
    isErrorHandler: edgeKind === "errorHandler",
    edgeKind,
    label: options?.label ?? (edgeKind === "errorHandler" ? "Error" : undefined),
  });
}
