import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectFlowsRecursive, findFlowWithCollection } from "../schema/utils/object-utils";
import { objectLocationMap, objectMap, issue } from "./shared";
import { findEnumeration, getEnumerationValueKeys } from "../metadata";
import { caseValueKey, getAllowedSpecializations } from "../flowgram/adapters/flowgram-case-options";
import type { MicroflowValidatorContext } from "./validator-types";
import { getDefaultSourcePortForEdgeKind, getDefaultTargetPortForEdgeKind, portsForObject } from "../schema/utils/port-utils";

function flowEdgeKind(flow: { kind: string; isErrorHandler?: boolean; editor: { edgeKind?: string } }): "sequence" | "decisionCondition" | "objectTypeCondition" | "errorHandler" | "annotation" {
  if (flow.kind === "annotation") {
    return "annotation";
  }
  return flow.isErrorHandler ? "errorHandler" : (flow.editor.edgeKind as "sequence" | "decisionCondition" | "objectTypeCondition" | "errorHandler");
}

export function validateFlows(schema: MicroflowSchema, context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const { metadata } = context;
  const issues: MicroflowValidationIssue[] = [];
  const objects = objectMap(schema);
  const locations = objectLocationMap(schema);
  const outgoingCaseKeys = new Map<string, Map<string, string>>();
  for (const flow of collectFlowsRecursive(schema)) {
    const flowCollectionId = findFlowWithCollection(schema, flow.id)?.collectionId;
    const source = objects.get(flow.originObjectId);
    const target = objects.get(flow.destinationObjectId);
    const sourceLocation = source ? locations.get(source.id) : undefined;
    const targetLocation = target ? locations.get(target.id) : undefined;
    if (!source) {
      issues.push(issue("MF_FLOW_ORIGIN_MISSING", "Flow originObjectId must reference an object.", { flowId: flow.id, fieldPath: "originObjectId", collectionId: flowCollectionId }));
    }
    if (!target) {
      issues.push(issue("MF_FLOW_DESTINATION_MISSING", "Flow destinationObjectId must reference an object.", { flowId: flow.id, fieldPath: "destinationObjectId", collectionId: flowCollectionId }));
    }
    if (flow.kind === "annotation") {
      if (sourceLocation && targetLocation && sourceLocation.collectionId !== targetLocation.collectionId) {
        issues.push(issue("MF_FLOW_LOOP_BOUNDARY", "AnnotationFlow cannot directly cross Loop objectCollection boundaries.", { flowId: flow.id, collectionId: flowCollectionId }));
      }
      if (source?.kind !== "annotation" && target?.kind !== "annotation") {
        issues.push(issue("MF_ANNOTATION_EDGE_ENDPOINT", "AnnotationFlow must connect to at least one Annotation.", { flowId: flow.id, collectionId: flowCollectionId }));
      }
      continue;
    }
    const edgeKind = flowEdgeKind(flow);
    if (flow.editor.edgeKind === "errorHandler" && !flow.isErrorHandler) {
      issues.push(issue("MF_FLOW_ERROR_KIND_MISMATCH", "edgeKind=errorHandler requires isErrorHandler=true.", { flowId: flow.id, fieldPath: "editor.edgeKind", collectionId: flowCollectionId }));
    }
    if (flow.isErrorHandler && flow.editor.edgeKind !== "errorHandler") {
      issues.push(issue("MF_FLOW_ERROR_KIND_MISMATCH", "isErrorHandler=true requires editor.edgeKind=errorHandler.", { flowId: flow.id, fieldPath: "isErrorHandler", collectionId: flowCollectionId }));
    }
    if (edgeKind === "sequence" && flow.caseValues.length > 0) {
      issues.push(issue("MF_FLOW_SEQUENCE_CASE_VALUES", "Plain sequence flow must not define caseValues.", { flowId: flow.id, fieldPath: "caseValues", collectionId: flowCollectionId }));
    }
    if (edgeKind === "errorHandler" && flow.caseValues.length > 0) {
      issues.push(issue("MF_FLOW_ERROR_CASE_VALUES", "Error handler flow must not define caseValues.", { flowId: flow.id, fieldPath: "caseValues", collectionId: flowCollectionId }));
    }
    const sourcePorts = source ? portsForObject(source) : [];
    const targetPorts = target ? portsForObject(target) : [];
    const sourcePort = sourcePorts.find((port, index) => port.kind === getDefaultSourcePortForEdgeKind(edgeKind) && index === flow.originConnectionIndex) ?? sourcePorts[flow.originConnectionIndex];
    const targetPort = targetPorts.find((port, index) => port.kind === getDefaultTargetPortForEdgeKind(edgeKind) && index === flow.destinationConnectionIndex) ?? targetPorts[flow.destinationConnectionIndex];
    if (source && !sourcePort) {
      issues.push(issue("MF_FLOW_ORIGIN_PORT_MISSING", "originConnectionIndex must resolve to a source port.", { flowId: flow.id, objectId: source.id, fieldPath: "originConnectionIndex", collectionId: flowCollectionId }));
    }
    if (target && !targetPort) {
      issues.push(issue("MF_FLOW_DESTINATION_PORT_MISSING", "destinationConnectionIndex must resolve to a target port.", { flowId: flow.id, objectId: target.id, fieldPath: "destinationConnectionIndex", collectionId: flowCollectionId }));
    }
    if (sourcePort && sourcePort.direction !== "output") {
      issues.push(issue("MF_FLOW_ORIGIN_PORT_DIRECTION", "originConnectionIndex must reference an output port.", { flowId: flow.id, fieldPath: "originConnectionIndex", collectionId: flowCollectionId }));
    }
    if (targetPort && targetPort.direction !== "input") {
      issues.push(issue("MF_FLOW_DESTINATION_PORT_DIRECTION", "destinationConnectionIndex must reference an input port.", { flowId: flow.id, fieldPath: "destinationConnectionIndex", collectionId: flowCollectionId }));
    }
    if (edgeKind === "decisionCondition" && source?.kind !== "exclusiveSplit") {
      issues.push(issue("MF_DECISION_FLOW_SOURCE", "decisionCondition flow must start from ExclusiveSplit.", { flowId: flow.id, fieldPath: "editor.edgeKind", collectionId: flowCollectionId }));
    }
    if (edgeKind === "objectTypeCondition" && source?.kind !== "inheritanceSplit") {
      issues.push(issue("MF_OBJECT_TYPE_FLOW_SOURCE", "objectTypeCondition flow must start from InheritanceSplit.", { flowId: flow.id, fieldPath: "editor.edgeKind", collectionId: flowCollectionId }));
    }
    if (sourceLocation && targetLocation && sourceLocation.collectionId !== targetLocation.collectionId) {
      issues.push(issue("MF_FLOW_LOOP_BOUNDARY", "SequenceFlow cannot directly cross Loop objectCollection boundaries.", { flowId: flow.id, collectionId: flowCollectionId }));
    }
    if (flowCollectionId && sourceLocation && targetLocation && sourceLocation.collectionId === targetLocation.collectionId && flowCollectionId !== sourceLocation.collectionId) {
      issues.push(issue("MF_FLOW_COLLECTION_MISMATCH", "Flow must be stored in the same objectCollection as its endpoints.", { flowId: flow.id, collectionId: flowCollectionId }));
    }
    if (source?.kind === "startEvent" && flow.isErrorHandler) {
      issues.push(issue("MF_START_ERROR_HANDLER", "StartEvent cannot create an error handler flow.", { flowId: flow.id, objectId: source.id }));
    }
    if (target?.kind === "startEvent") {
      issues.push(issue("MF_START_HAS_INCOMING", "StartEvent cannot have incoming SequenceFlow.", { flowId: flow.id, objectId: target.id }));
    }
    if (source && ["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(source.kind)) {
      issues.push(issue("MF_TERMINAL_HAS_OUTGOING", "Terminal events cannot have outgoing SequenceFlow.", { flowId: flow.id, objectId: source.id }));
    }
    if (source && ["parameterObject", "annotation"].includes(source.kind) || target && ["parameterObject", "annotation"].includes(target.kind)) {
      issues.push(issue("MF_NON_EXECUTABLE_SEQUENCE", "SequenceFlow cannot connect ParameterObject or Annotation.", { flowId: flow.id }));
    }
    if (target?.kind === "errorEvent" && !flow.isErrorHandler) {
      issues.push(issue("MF_ERROR_EVENT_REQUIRES_ERROR_FLOW", "ErrorEvent can only be reached by an error handler SequenceFlow.", { flowId: flow.id, objectId: target.id }));
    }
    if (flow.isErrorHandler && source && !["actionActivity", "loopedActivity", "exclusiveSplit", "inheritanceSplit"].includes(source.kind)) {
      issues.push(issue("MF_ERROR_FLOW_SOURCE", "isErrorHandler source must support errorHandling.", { flowId: flow.id }));
    }
    if (source?.kind === "exclusiveSplit" && flow.editor.edgeKind === "decisionCondition") {
      if (flow.caseValues.length === 0) {
        issues.push(issue("MF_DECISION_CASE_MISSING", "Decision condition flow must define a case value.", { flowId: flow.id, objectId: source.id, fieldPath: "caseValues" }));
      }
      for (const caseValue of flow.caseValues) {
        if (caseValue.kind === "noCase") {
          issues.push(issue("MF_DECISION_CASE_NO_CASE", "Decision condition case is not configured.", {
            flowId: flow.id,
            objectId: source.id,
            fieldPath: "caseValues",
          }, context.mode === "edit" ? "warning" : "error"));
        }
        const key = caseValueKey(caseValue);
        const perSource = outgoingCaseKeys.get(source.id) ?? new Map<string, string>();
        if (perSource.has(key)) {
          issues.push(issue("MF_DECISION_CASE_DUPLICATED", "Decision case values must be unique per source.", { flowId: flow.id, objectId: source.id, fieldPath: "caseValues" }));
        }
        perSource.set(key, flow.id);
        outgoingCaseKeys.set(source.id, perSource);
      }
      const booleanCases = flow.caseValues.filter(caseValue => caseValue.kind === "boolean");
      for (const caseValue of booleanCases) {
        const duplicates = collectFlowsRecursive(schema).filter(item =>
          item.kind === "sequence" &&
          item.id !== flow.id &&
          item.originObjectId === source.id &&
          item.caseValues.some(other => other.kind === "boolean" && other.value === caseValue.value)
        );
        if (duplicates.length > 0) {
          issues.push(issue("MF_DECISION_CASE_DUPLICATED", "Decision case values must be unique per source.", { flowId: flow.id, objectId: source.id }));
        }
      }
      for (const caseValue of flow.caseValues.filter(caseValue => caseValue.kind === "enumeration")) {
        const enumeration = findEnumeration(metadata, caseValue.enumerationQualifiedName);
        if (!caseValue.enumerationQualifiedName || !enumeration) {
          issues.push(issue("MF_ENUMERATION_CASE_UNKNOWN_ENUMERATION", "Enumeration case must reference an existing enumeration.", { flowId: flow.id, objectId: source.id, fieldPath: "caseValues" }));
          continue;
        }
        if (!getEnumerationValueKeys(metadata, caseValue.enumerationQualifiedName).includes(caseValue.value)) {
          issues.push(issue("MF_ENUMERATION_CASE_INVALID_VALUE", "Enumeration case value must belong to the selected enumeration.", { flowId: flow.id, objectId: source.id, fieldPath: "caseValues" }));
        }
      }
    }
    if (source?.kind === "inheritanceSplit" && flow.editor.edgeKind === "objectTypeCondition") {
      if (!source.inputObjectVariableName) {
        issues.push(issue("MF_OBJECT_TYPE_INPUT_MISSING", "InheritanceSplit must define inputObjectVariableName.", { objectId: source.id, fieldPath: "inputObjectVariableName" }));
      }
      if (!source.entity.generalizedEntityQualifiedName) {
        issues.push(issue("MF_OBJECT_TYPE_GENERALIZATION_MISSING", "InheritanceSplit must define generalizedEntityQualifiedName.", { objectId: source.id, fieldPath: "entity.generalizedEntityQualifiedName" }));
      }
      if (flow.caseValues.length === 0) {
        issues.push(issue("MF_OBJECT_TYPE_CASE_MISSING", "Object type condition flow must define a case value.", { flowId: flow.id, objectId: source.id, fieldPath: "caseValues" }));
      }
      for (const caseValue of flow.caseValues) {
        if (caseValue.kind === "noCase") {
          issues.push(issue("MF_OBJECT_TYPE_CASE_NO_CASE", "Object type condition case is not configured.", {
            flowId: flow.id,
            objectId: source.id,
            fieldPath: "caseValues",
          }, context.mode === "edit" ? "warning" : "error"));
        }
        const key = caseValueKey(caseValue);
        const perSource = outgoingCaseKeys.get(source.id) ?? new Map<string, string>();
        if (perSource.has(key)) {
          issues.push(issue("MF_OBJECT_TYPE_CASE_DUPLICATED", "Object type case values must be unique per source.", { flowId: flow.id, objectId: source.id, fieldPath: "caseValues" }));
        }
        perSource.set(key, flow.id);
        outgoingCaseKeys.set(source.id, perSource);
      }
      for (const caseValue of flow.caseValues.filter(caseValue => caseValue.kind === "inheritance")) {
        const allowed = getAllowedSpecializations(source, metadata);
        if (allowed.length > 0 && !allowed.includes(caseValue.entityQualifiedName)) {
          issues.push(issue("MF_OBJECT_TYPE_CASE_INVALID_SPECIALIZATION", "Object type case specialization must be allowed by the source InheritanceSplit.", { flowId: flow.id, objectId: source.id, fieldPath: "caseValues" }));
        }
        const duplicates = collectFlowsRecursive(schema).filter(item =>
          item.kind === "sequence" &&
          item.id !== flow.id &&
          item.originObjectId === source.id &&
          item.caseValues.some(other => other.kind === "inheritance" && other.entityQualifiedName === caseValue.entityQualifiedName)
        );
        if (duplicates.length > 0) {
          issues.push(issue("MF_OBJECT_TYPE_CASE_DUPLICATED", "Object type case values must be unique per source.", { flowId: flow.id, objectId: source.id }));
        }
      }
    }
  }
  return issues;
}
