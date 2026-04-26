import type { MicroflowCaseValue, MicroflowObject, MicroflowSchema } from "../types";

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

export function getCaseDisplayLabel(caseValue: MicroflowCaseValue): string {
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

export function getUsedCaseValues(schema: MicroflowSchema, sourceObjectId: string, excludeFlowId?: string): MicroflowCaseValue[] {
  return schema.flows
    .filter(flow => flow.kind === "sequence" && flow.originObjectId === sourceObjectId && flow.id !== excludeFlowId && !flow.isErrorHandler)
    .flatMap(flow => flow.kind === "sequence" ? flow.caseValues : []);
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
  return {
    ...schema,
    flows: schema.flows.map(flow => {
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
    }),
  };
}

