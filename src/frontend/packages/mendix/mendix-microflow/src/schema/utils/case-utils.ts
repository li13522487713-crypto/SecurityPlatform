import type { MicroflowCaseValue, MicroflowFlow, MicroflowObject, MicroflowObjectCollection, MicroflowSchema } from "../types";
import type { MicroflowMetadataCatalog } from "../../metadata";
import { collectFlowsRecursive } from "./object-utils";

function simpleName(value: string): string {
  return value.split(".").at(-1) ?? value;
}

export function createBooleanCaseValue(value: boolean): MicroflowCaseValue {
  return { kind: "boolean", officialType: "Microflows$EnumerationCase", value, persistedValue: value ? "true" : "false" };
}

export function createEnumerationCaseValue(enumerationQualifiedName: string, value: string): MicroflowCaseValue {
  return { kind: "enumeration", officialType: "Microflows$EnumerationCase", enumerationQualifiedName, value };
}

export function createInheritanceCaseValue(entityQualifiedName: string): MicroflowCaseValue {
  return { kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName };
}

export function createEmptyCaseValue(): MicroflowCaseValue {
  return { kind: "empty", officialType: "Microflows$NoCase" };
}

export function createFallbackCaseValue(): MicroflowCaseValue {
  return { kind: "fallback", officialType: "Microflows$NoCase" };
}

export function createNoCaseValue(): MicroflowCaseValue {
  return { kind: "noCase", officialType: "Microflows$NoCase" };
}

export function getCaseValueKey(caseValue: MicroflowCaseValue): string {
  return caseValueIdentity(caseValue);
}

export function getCaseDisplayLabel(caseValue: MicroflowCaseValue, _metadata?: MicroflowMetadataCatalog): string {
  if (caseValue.kind === "boolean") {
    return caseValue.value ? "是" : "否";
  }
  if (caseValue.kind === "enumeration") {
    return simpleName(caseValue.value);
  }
  if (caseValue.kind === "inheritance") {
    return simpleName(caseValue.entityQualifiedName);
  }
  if (caseValue.kind === "empty") {
    return "empty";
  }
  if (caseValue.kind === "fallback") {
    return "fallback";
  }
  return "未配置条件";
}

export function caseValueIdentity(caseValue: MicroflowCaseValue): string {
  if (caseValue.kind === "boolean") {
    return `boolean:${caseValue.value}`;
  }
  if (caseValue.kind === "enumeration") {
    return `enumeration:${caseValue.enumerationQualifiedName}:${caseValue.value}`;
  }
  if (caseValue.kind === "inheritance") {
    return `inheritance:${caseValue.entityQualifiedName}`;
  }
  return caseValue.kind;
}

export function isSameCaseValue(left: MicroflowCaseValue, right: MicroflowCaseValue): boolean {
  return caseValueIdentity(left) === caseValueIdentity(right);
}

export function isCaseValueCompatibleWithSource(caseValue: MicroflowCaseValue, sourceObject: MicroflowObject | undefined): boolean {
  if (!sourceObject) {
    return false;
  }
  if (sourceObject.kind === "exclusiveSplit") {
    return sourceObject.splitCondition.kind === "expression" && sourceObject.splitCondition.resultType === "boolean"
      ? caseValue.kind === "boolean" || caseValue.kind === "noCase"
      : caseValue.kind === "enumeration" || caseValue.kind === "empty" || caseValue.kind === "noCase";
  }
  if (sourceObject.kind === "inheritanceSplit") {
    return caseValue.kind === "inheritance" || caseValue.kind === "empty" || caseValue.kind === "fallback" || caseValue.kind === "noCase";
  }
  return caseValue.kind === "noCase";
}

export function getUsedCaseValues(schema: MicroflowSchema, sourceObjectId: string, excludeFlowId?: string): MicroflowCaseValue[] {
  return collectFlowsRecursive(schema)
    .filter(flow => flow.kind === "sequence" && flow.originObjectId === sourceObjectId && flow.id !== excludeFlowId && !flow.isErrorHandler)
    .flatMap(flow => flow.kind === "sequence" ? flow.caseValues : []);
}

export function validateDuplicateCaseValues(schema: MicroflowSchema, sourceObjectId: string, excludeFlowId?: string): Array<{ key: string; flowIds: string[] }> {
  const byKey = new Map<string, string[]>();
  for (const flow of collectFlowsRecursive(schema)) {
    if (flow.kind !== "sequence" || flow.originObjectId !== sourceObjectId || flow.id === excludeFlowId || flow.isErrorHandler) {
      continue;
    }
    for (const caseValue of flow.caseValues) {
      const key = caseValueIdentity(caseValue);
      byKey.set(key, [...(byKey.get(key) ?? []), flow.id]);
    }
  }
  return [...byKey.entries()].filter(([, flowIds]) => flowIds.length > 1).map(([key, flowIds]) => ({ key, flowIds }));
}

export function isCaseValueDuplicate(
  schema: MicroflowSchema,
  sourceObjectId: string,
  candidateCase: MicroflowCaseValue,
  excludeFlowId?: string,
): boolean {
  const key = caseValueIdentity(candidateCase);
  return getUsedCaseValues(schema, sourceObjectId, excludeFlowId).some(caseValue => caseValueIdentity(caseValue) === key);
}

export function inferCaseEditorType(sourceObject: MicroflowObject | undefined): "boolean" | "enumeration" | "objectType" | "none" {
  if (sourceObject?.kind === "exclusiveSplit") {
    return sourceObject.splitCondition.resultType === "enumeration" ? "enumeration" : "boolean";
  }
  if (sourceObject?.kind === "inheritanceSplit") {
    return "objectType";
  }
  return "none";
}

export function updateFlowCaseValue(schema: MicroflowSchema, flowId: string, nextCaseValue: MicroflowCaseValue): MicroflowSchema {
  const updateFlows = (flows: MicroflowFlow[] | undefined): MicroflowFlow[] | undefined => flows?.map(flow => {
    if (flow.id !== flowId || flow.kind !== "sequence") {
      return flow;
    }
    return {
      ...flow,
      caseValues: [nextCaseValue],
      editor: {
        ...flow.editor,
        label: getCaseDisplayLabel(nextCaseValue),
      },
    };
  });
  const updateCollection = (collection: MicroflowObjectCollection): MicroflowObjectCollection => ({
    ...collection,
    flows: updateFlows(collection.flows),
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: updateCollection(object.objectCollection) }
      : object),
  });
  return {
    ...schema,
    flows: updateFlows(schema.flows) ?? schema.flows,
    objectCollection: updateCollection(schema.objectCollection),
  };
}

export function clearFlowCaseValue(schema: MicroflowSchema, flowId: string): MicroflowSchema {
  return updateFlowCaseValue(schema, flowId, createNoCaseValue());
}

