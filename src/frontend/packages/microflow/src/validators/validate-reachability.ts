import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

export function validateReachability(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const objects = flattenObjects(schema.objectCollection).map(item => item.object);
  const start = objects.find(object => object.kind === "startEvent");
  if (!start) {
    return [];
  }
  const visited = new Set<string>();
  const queue = [start.id];
  while (queue.length > 0) {
    const current = queue.shift();
    if (!current || visited.has(current)) {
      continue;
    }
    visited.add(current);
    for (const flow of schema.flows.filter(item => item.kind === "sequence" && item.originObjectId === current && !item.isErrorHandler)) {
      queue.push(flow.destinationObjectId);
    }
  }
  return objects
    .filter(object => !["annotation", "parameterObject"].includes(object.kind) && object.kind !== "startEvent" && !visited.has(object.id))
    .map(object => issue("MF_OBJECT_UNREACHABLE", "Executable object is not reachable from StartEvent.", { objectId: object.id }, "warning"));
}
