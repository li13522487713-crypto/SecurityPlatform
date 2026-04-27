import type { MicroflowObject, MicroflowObjectCollection, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";

function sourceFromCode(code: string): NonNullable<MicroflowValidationIssue["source"]> {
  if (code.startsWith("MF_ROOT") || code === "MF_OBJECT_COLLECTION_MISSING" || code === "MF_FLOWS_MISSING" || code.startsWith("MF_SELECTION")) {
    return "schema";
  }
  if (code.startsWith("MF_PARAMETER")) {
    return "parameter";
  }
  if (code.startsWith("MF_OBJECT_")) {
    return "node";
  }
  if (code.startsWith("MF_FLOW") || code.startsWith("MF_SEQUENCE") || code.startsWith("MF_ANNOTATION_FLOW")) {
    return "flow";
  }
  if (code.startsWith("MF_START") || code.startsWith("MF_END") || code.startsWith("MF_ERROR_EVENT") || code.startsWith("MF_BREAK") || code.startsWith("MF_CONTINUE")) {
    return "node";
  }
  if (code.startsWith("MF_DECISION") || code.startsWith("MF_OBJECT_TYPE")) {
    return "decision";
  }
  if (code.startsWith("MF_LOOP")) {
    return "loop";
  }
  if (code.startsWith("MF_CALL_MICROFLOW") || code.startsWith("MF_METADATA_MICROFLOW")) {
    return "callMicroflow";
  }
  if (code.startsWith("MF_METADATA")) {
    return "domainModel";
  }
  if (code.startsWith("MF_VARIABLE") || code.startsWith("MF_CURRENT") || code.startsWith("MF_LATEST")) {
    return "variable";
  }
  if (code.startsWith("MF_CREATE_OBJECT") || code.startsWith("MF_CHANGE_OBJECT") || code.startsWith("MF_COMMIT") || code.startsWith("MF_DELETE") || code.startsWith("MF_ROLLBACK")) {
    return "domainModel";
  }
  if (code.startsWith("MF_EXPR") || code.startsWith("MF_EXPRESSION")) {
    return "expression";
  }
  if (code.startsWith("MF_ERROR_HANDLER")) {
    return "errorHandling";
  }
  if (code.includes("UNREACHABLE") || code.includes("DEAD_END") || code.includes("CANNOT_REACH")) {
    return "reachability";
  }
  return "action";
}

export function issue(
  code: MicroflowValidationIssue["code"],
  message: string,
  target: Partial<Pick<MicroflowValidationIssue, "objectId" | "flowId" | "actionId" | "fieldPath" | "nodeId" | "edgeId" | "parameterId" | "collectionId" | "source" | "details" | "relatedObjectIds" | "relatedFlowIds">> = {},
  severity: MicroflowValidationIssue["severity"] = "error"
): MicroflowValidationIssue {
  const source = target.source ?? sourceFromCode(code);
  return {
    id: `${code}:${source}:${target.objectId ?? target.flowId ?? target.actionId ?? target.nodeId ?? target.edgeId ?? target.parameterId ?? target.collectionId ?? "schema"}:${target.fieldPath ?? "root"}`,
    code,
    message,
    severity,
    source,
    ...target
  };
}

export function flattenObjects(collection: MicroflowObjectCollection): Array<{ object: MicroflowObject; loopObjectId?: string; collectionId: string }> {
  const result: Array<{ object: MicroflowObject; loopObjectId?: string; collectionId: string }> = [];
  for (const object of collection.objects) {
    if (object.kind !== "loopedActivity") {
      result.push({ object, collectionId: collection.id });
      continue;
    }
    result.push({ object, collectionId: collection.id });
    result.push(...flattenObjects(object.objectCollection).map(item => ({ ...item, loopObjectId: item.loopObjectId ?? object.id })));
  }
  return result;
}

export function objectMap(schema: MicroflowSchema): Map<string, MicroflowObject> {
  return new Map(flattenObjects(schema.objectCollection).map(item => [item.object.id, item.object]));
}

export interface MicroflowObjectLocation {
  object: MicroflowObject;
  collectionId: string;
  parentLoopObjectId?: string;
  ancestorLoopObjectIds: string[];
}

export function flattenObjectLocations(
  collection: MicroflowObjectCollection,
  ancestorLoopObjectIds: string[] = [],
  parentLoopObjectId?: string
): MicroflowObjectLocation[] {
  return collection.objects.flatMap(object => {
    const location: MicroflowObjectLocation = {
      object,
      collectionId: collection.id,
      parentLoopObjectId,
      ancestorLoopObjectIds,
    };
    if (object.kind !== "loopedActivity") {
      return [location];
    }
    return [
      location,
      ...flattenObjectLocations(object.objectCollection, [...ancestorLoopObjectIds, object.id], object.id),
    ];
  });
}

export function objectLocationMap(schema: MicroflowSchema): Map<string, MicroflowObjectLocation> {
  return new Map(flattenObjectLocations(schema.objectCollection).map(item => [item.object.id, item]));
}
