import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { flattenObjects, issue } from "./shared";

export function validateReachability(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const objects = flattenObjects(schema.objectCollection).map(item => item.object);
  const flows = collectFlowsRecursive(schema);
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
    for (const flow of flows.filter(item => item.kind === "sequence" && item.originObjectId === current && !item.isErrorHandler)) {
      queue.push(flow.destinationObjectId);
    }
  }
  const issues = objects
    .filter(object => !["annotation", "parameterObject"].includes(object.kind) && object.kind !== "startEvent" && !visited.has(object.id))
    .map(object => issue("MF_OBJECT_UNREACHABLE", "Executable object is not reachable from StartEvent.", { objectId: object.id }, "warning"));

  const terminalIds = new Set(
    objects
      .filter(object => object.kind === "endEvent" || object.kind === "errorEvent")
      .map(object => object.id)
  );
  const outgoingByOrigin = new Map<string, string[]>();
  for (const flow of flows.filter(item => item.kind === "sequence")) {
    outgoingByOrigin.set(flow.originObjectId, [...(outgoingByOrigin.get(flow.originObjectId) ?? []), flow.destinationObjectId]);
  }
  const canReachTerminal = new Map<string, boolean>();
  const dfs = (objectId: string, stack = new Set<string>()): boolean => {
    if (terminalIds.has(objectId)) {
      return true;
    }
    if (canReachTerminal.has(objectId)) {
      return canReachTerminal.get(objectId) ?? false;
    }
    if (stack.has(objectId)) {
      return false;
    }
    stack.add(objectId);
    const result = (outgoingByOrigin.get(objectId) ?? []).some(next => dfs(next, stack));
    stack.delete(objectId);
    canReachTerminal.set(objectId, result);
    return result;
  };
  for (const object of objects) {
    if (["annotation", "parameterObject", "endEvent", "errorEvent"].includes(object.kind)) {
      continue;
    }
    if (!dfs(object.id)) {
      issues.push(issue("MF_OBJECT_TERMINAL_UNREACHABLE", "Object path cannot reach EndEvent or ErrorEvent.", { objectId: object.id }, "warning"));
    }
  }
  return issues;
}
