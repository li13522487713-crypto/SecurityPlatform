import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

export function validateObjectCollection(schema: MicroflowSchema, _context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const ids = new Set<string>();
  for (const { object } of flattenObjects(schema.objectCollection)) {
    if (ids.has(object.id)) {
      issues.push(issue("MF_OBJECT_ID_DUPLICATED", `Object "${object.id}" is duplicated.`, { objectId: object.id }));
    }
    ids.add(object.id);
    if ("caption" in object && typeof object.caption === "string" && !object.caption.trim()) {
      issues.push(issue("MF_OBJECT_CAPTION_MISSING", "Object caption/name is empty.", { objectId: object.id, fieldPath: "caption" }, "warning"));
    }
    const rawObject = object as unknown as { kind?: string; officialType?: string };
    if (!rawObject.kind || !rawObject.officialType) {
      issues.push(issue("MF_OBJECT_KIND_UNSUPPORTED", "Object kind/officialType is missing or unsupported.", { objectId: object.id, fieldPath: "kind" }));
    }
    if (!Number.isFinite(object.relativeMiddlePoint.x) || !Number.isFinite(object.relativeMiddlePoint.y)) {
      issues.push(issue("MF_OBJECT_POSITION_INVALID", "Object relativeMiddlePoint must be finite.", { objectId: object.id, fieldPath: "relativeMiddlePoint" }));
    }
  }
  return issues;
}
