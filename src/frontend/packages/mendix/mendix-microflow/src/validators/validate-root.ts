import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { flattenObjects, issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

export function validateRoot(schema: MicroflowSchema, _context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  if (!schema.objectCollection) {
    issues.push(issue("MF_OBJECT_COLLECTION_MISSING", "Microflow must have objectCollection.", { fieldPath: "objectCollection" }));
  }
  if (!Array.isArray(schema.flows)) {
    issues.push(issue("MF_FLOWS_MISSING", "Microflow must have flows array.", { fieldPath: "flows" }));
  }
  const objects = flattenObjects(schema.objectCollection).map(item => item.object);
  const objectIds = new Set(objects.map(object => object.id));
  const flowIds = new Set(collectFlowsRecursive(schema).map(flow => flow.id));
  if (schema.editor.selection.objectId && !objectIds.has(schema.editor.selection.objectId)) {
    issues.push(issue("MF_SELECTION_OBJECT_NOT_FOUND", "Editor selection points to a missing object.", { fieldPath: "editor.selection.objectId" }, "warning"));
  }
  if (schema.editor.selection.flowId && !flowIds.has(schema.editor.selection.flowId)) {
    issues.push(issue("MF_SELECTION_FLOW_NOT_FOUND", "Editor selection points to a missing flow.", { fieldPath: "editor.selection.flowId" }, "warning"));
  }
  const names = new Map<string, string>();
  for (const parameter of schema.parameters) {
    const trimmed = parameter.name.trim();
    const normalized = trimmed.toLocaleLowerCase();
    if (!trimmed) {
      issues.push(issue("MF_PARAMETER_NAME_MISSING", "Parameter name is required.", { fieldPath: `parameters.${parameter.id}.name`, parameterId: parameter.id }));
      continue;
    }
    if (names.has(normalized)) {
      issues.push(issue("MF_PARAMETER_DUPLICATED", `Parameter "${parameter.name}" duplicates "${names.get(normalized)}".`, { fieldPath: `parameters.${parameter.id}.name`, parameterId: parameter.id }));
    }
    names.set(normalized, parameter.name);
  }
  const parameterIds = new Set(schema.parameters.map(parameter => parameter.id));
  for (const object of objects) {
    if (object.kind !== "parameterObject") {
      continue;
    }
    if (!parameterIds.has(object.parameterId)) {
      issues.push(issue("MF_PARAMETER_OBJECT_STALE", "Parameter node references a missing schema-level parameter.", { objectId: object.id, parameterId: object.parameterId, fieldPath: "parameterId" }));
    }
  }
  const parameterObjectIds = new Set(objects.filter(object => object.kind === "parameterObject").map(object => object.kind === "parameterObject" ? object.parameterId : ""));
  for (const parameter of schema.parameters) {
    if (!parameterObjectIds.has(parameter.id)) {
      issues.push(issue("MF_PARAMETER_SCHEMA_STALE", `Parameter "${parameter.name}" has no matching Parameter node.`, { parameterId: parameter.id, fieldPath: `parameters.${parameter.id}` }));
    }
    if (parameter.dataType.kind === "unknown" || parameter.dataType.kind === "void") {
      issues.push(issue("MF_PARAMETER_TYPE_MISSING", `Parameter "${parameter.name || parameter.id}" must have a concrete data type.`, { parameterId: parameter.id, fieldPath: `parameters.${parameter.id}.dataType` }, "warning"));
    }
  }
  return issues;
}
