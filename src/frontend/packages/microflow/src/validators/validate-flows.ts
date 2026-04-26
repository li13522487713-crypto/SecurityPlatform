import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { objectMap, issue } from "./shared";

export function validateFlows(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const objects = objectMap(schema);
  for (const flow of schema.flows) {
    const source = objects.get(flow.originObjectId);
    const target = objects.get(flow.destinationObjectId);
    if (!source) {
      issues.push(issue("MF_FLOW_ORIGIN_MISSING", "Flow originObjectId must reference an object.", { flowId: flow.id, fieldPath: "originObjectId" }));
    }
    if (!target) {
      issues.push(issue("MF_FLOW_DESTINATION_MISSING", "Flow destinationObjectId must reference an object.", { flowId: flow.id, fieldPath: "destinationObjectId" }));
    }
    if (flow.kind === "annotation") {
      if (source?.kind !== "annotation" && target?.kind !== "annotation") {
        issues.push(issue("MF_ANNOTATION_EDGE_ENDPOINT", "AnnotationFlow must connect to at least one Annotation.", { flowId: flow.id }));
      }
      continue;
    }
    if (source && ["parameterObject", "annotation"].includes(source.kind) || target && ["parameterObject", "annotation"].includes(target.kind)) {
      issues.push(issue("MF_NON_EXECUTABLE_SEQUENCE", "SequenceFlow cannot connect ParameterObject or Annotation.", { flowId: flow.id }));
    }
    if (flow.isErrorHandler && source && !["actionActivity", "loopedActivity", "exclusiveSplit", "inheritanceSplit"].includes(source.kind)) {
      issues.push(issue("MF_ERROR_FLOW_SOURCE", "isErrorHandler source must support errorHandling.", { flowId: flow.id }));
    }
  }
  return issues;
}
