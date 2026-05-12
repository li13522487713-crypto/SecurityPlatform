import { createAnnotationFlow, createMicroflowFlowId, createSequenceFlow, flattenObjectCollection } from "../../adapters";
import { inferEdgeKindFromPorts } from "../../node-registry";
import type {
  MicroflowCaseValue,
  MicroflowEditorPort,
  MicroflowLine,
  MicroflowFlow,
  MicroflowObject,
  MicroflowSchema,
} from "../../schema";
import { booleanCaseValue, emptyCaseValue, inheritanceCaseValue, noCaseValue } from "./flowgram-case-options";
import { forceOrthogonalLineKind } from "../FlowGramMicroflowTypes";

export function defaultCaseValuesForPorts(sourcePort: MicroflowEditorPort, source?: MicroflowObject): MicroflowCaseValue[] {
  if (source?.kind === "exclusiveSplit" && source.splitCondition.resultType === "enumeration") {
    return [noCaseValue()];
  }
  if (sourcePort.kind === "decisionOut") {
    const value = sourcePort.label.toLowerCase() !== "false";
    return [booleanCaseValue(value)];
  }
  if (source?.kind === "inheritanceSplit") {
    return [noCaseValue()];
  }
  if (sourcePort.kind === "objectTypeOut") {
    const value = sourcePort.label ?? "(empty)";
    return [value === "fallback" || value === "(empty)" ? emptyCaseValue() : inheritanceCaseValue(value)];
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
  options?: { caseValues?: MicroflowCaseValue[]; label?: string; lineKind?: MicroflowLine["kind"] },
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
        id: createMicroflowFlowId(schema, "annotation-flow"),
        originObjectId: source.id,
        destinationObjectId: target.id,
        label: options?.label ?? "Annotation",
      }),
      originConnectionIndex: sourcePort.connectionIndex,
      destinationConnectionIndex: targetPort.connectionIndex,
    };
  }
  const caseValues = options?.caseValues ?? defaultCaseValuesForPorts(sourcePort, source);
  const flow = createSequenceFlow({
    id: createMicroflowFlowId(schema, "flow"),
    originObjectId: source.id,
    destinationObjectId: target.id,
    originConnectionIndex: sourcePort.connectionIndex,
    destinationConnectionIndex: targetPort.connectionIndex,
    caseValues,
    isErrorHandler: edgeKind === "errorHandler",
    edgeKind,
    label: options?.label ?? (edgeKind === "errorHandler" ? "Error" : undefined),
  });
  return {
    ...flow,
    line: { ...flow.line, kind: forceOrthogonalLineKind(options?.lineKind) },
  };
}
