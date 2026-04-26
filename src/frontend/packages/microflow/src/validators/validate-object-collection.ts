import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

export function validateObjectCollection(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const ids = new Set<string>();
  for (const { object } of flattenObjects(schema.objectCollection)) {
    if (ids.has(object.id)) {
      issues.push(issue("MF_OBJECT_DUPLICATED", `Object "${object.id}" is duplicated.`, { objectId: object.id }));
    }
    ids.add(object.id);
    if (!Number.isFinite(object.relativeMiddlePoint.x) || !Number.isFinite(object.relativeMiddlePoint.y)) {
      issues.push(issue("MF_OBJECT_POSITION_INVALID", "Object relativeMiddlePoint must be finite.", { objectId: object.id, fieldPath: "relativeMiddlePoint" }));
    }
  }
  return issues;
}
