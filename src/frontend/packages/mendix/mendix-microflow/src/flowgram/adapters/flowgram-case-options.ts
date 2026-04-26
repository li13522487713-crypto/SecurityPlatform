import { findEntity, findEnumeration, mockMicroflowMetadataCatalog, type MicroflowMetadataCatalog } from "../../metadata";
import { flattenObjectCollection } from "../../adapters";
import type {
  MicroflowCaseValue,
  MicroflowExclusiveSplit,
  MicroflowFlow,
  MicroflowInheritanceSplit,
  MicroflowObject,
  MicroflowSchema,
} from "../../schema";

export type MicroflowCaseEditorKind = "boolean" | "enumeration" | "objectType";

export interface MicroflowCaseOption {
  key: string;
  label: string;
  caseValue: MicroflowCaseValue;
  disabled: boolean;
  reason?: string;
}

function simpleName(value: string): string {
  return value.split(".").at(-1) ?? value;
}

export function booleanCaseValue(value: boolean): MicroflowCaseValue {
  return {
    kind: "boolean",
    officialType: "Microflows$EnumerationCase",
    value,
    persistedValue: value ? "true" : "false",
  };
}

export function enumerationCaseValue(enumerationQualifiedName: string, value: string): MicroflowCaseValue {
  return {
    kind: "enumeration",
    officialType: "Microflows$EnumerationCase",
    enumerationQualifiedName,
    value,
  };
}

export function emptyCaseValue(): MicroflowCaseValue {
  return { kind: "empty", officialType: "Microflows$NoCase" };
}

export function fallbackCaseValue(): MicroflowCaseValue {
  return { kind: "fallback", officialType: "Microflows$NoCase" };
}

export function inheritanceCaseValue(entityQualifiedName: string): MicroflowCaseValue {
  return {
    kind: "inheritance",
    officialType: "Microflows$InheritanceCase",
    entityQualifiedName,
  };
}

export function caseValueKey(caseValue: MicroflowCaseValue): string {
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

export function caseValueLabel(caseValue: MicroflowCaseValue): string {
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
  return "no case";
}

export function flowCaseLabel(flow: MicroflowFlow): string | undefined {
  if (flow.kind !== "sequence") {
    return flow.editor.label;
  }
  if (flow.isErrorHandler) {
    return flow.editor.label ?? "Error";
  }
  const generatedLabel = flow.caseValues.map(caseValueLabel).join(", ") || undefined;
  return flow.editor.label ?? generatedLabel;
}

function objectById(schema: MicroflowSchema, objectId: string | undefined): MicroflowObject | undefined {
  if (!objectId) {
    return undefined;
  }
  return flattenObjectCollection(schema.objectCollection).find(object => object.id === objectId);
}

function usedCaseKeys(schema: MicroflowSchema, sourceObjectId: string, currentFlowId?: string): Set<string> {
  return new Set(
    schema.flows
      .filter(flow => flow.kind === "sequence" && flow.id !== currentFlowId && flow.originObjectId === sourceObjectId && !flow.isErrorHandler)
      .flatMap(flow => flow.caseValues.map(caseValueKey)),
  );
}

export function getCaseEditorKind(schema: MicroflowSchema, sourceObjectId: string | undefined): MicroflowCaseEditorKind | undefined {
  const source = objectById(schema, sourceObjectId);
  if (source?.kind === "exclusiveSplit") {
    return source.splitCondition.resultType === "enumeration" ? "enumeration" : "boolean";
  }
  if (source?.kind === "inheritanceSplit") {
    return "objectType";
  }
  return undefined;
}

export function getBooleanCaseOptions(schema: MicroflowSchema, sourceObjectId: string, currentFlowId?: string): MicroflowCaseOption[] {
  const used = usedCaseKeys(schema, sourceObjectId, currentFlowId);
  return [true, false].map(value => {
    const caseValue = booleanCaseValue(value);
    const disabled = used.has(caseValueKey(caseValue));
    return {
      key: caseValueKey(caseValue),
      label: caseValueLabel(caseValue),
      caseValue,
      disabled,
      reason: disabled ? "该分支已存在" : undefined,
    };
  });
}

export function getEnumerationCaseOptions(
  schema: MicroflowSchema,
  sourceObjectId: string,
  currentFlowId?: string,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog,
): MicroflowCaseOption[] {
  const source = objectById(schema, sourceObjectId);
  const enumerationQualifiedName = source?.kind === "exclusiveSplit" && source.splitCondition.kind === "expression"
    ? source.splitCondition.enumerationQualifiedName
    : undefined;
  const enumeration = findEnumeration(metadata, enumerationQualifiedName);
  const used = usedCaseKeys(schema, sourceObjectId, currentFlowId);
  const valueOptions = (enumeration?.values ?? []).map(value => {
    const caseValue = enumerationCaseValue(enumerationQualifiedName ?? "", value);
    const disabled = used.has(caseValueKey(caseValue));
    return {
      key: caseValueKey(caseValue),
      label: value,
      caseValue,
      disabled,
      reason: disabled ? "该分支已存在" : undefined,
    };
  });
  const empty = emptyCaseValue();
  const emptyDisabled = used.has(caseValueKey(empty));
  return [
    ...valueOptions,
    {
      key: caseValueKey(empty),
      label: "empty",
      caseValue: empty,
      disabled: emptyDisabled,
      reason: emptyDisabled ? "该分支已存在" : undefined,
    },
  ];
}

export function getObjectTypeCaseOptions(
  schema: MicroflowSchema,
  sourceObjectId: string,
  currentFlowId?: string,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog,
): MicroflowCaseOption[] {
  const source = objectById(schema, sourceObjectId);
  const split = source?.kind === "inheritanceSplit" ? source : undefined;
  const specializations = getAllowedSpecializations(split, metadata);
  const used = usedCaseKeys(schema, sourceObjectId, currentFlowId);
  const entityOptions = specializations.map(entityQualifiedName => {
    const caseValue = inheritanceCaseValue(entityQualifiedName);
    const disabled = used.has(caseValueKey(caseValue));
    return {
      key: caseValueKey(caseValue),
      label: simpleName(entityQualifiedName),
      caseValue,
      disabled,
      reason: disabled ? "该分支已存在" : undefined,
    };
  });
  const specialOptions = [emptyCaseValue(), fallbackCaseValue()].map(caseValue => {
    const disabled = used.has(caseValueKey(caseValue));
    return {
      key: caseValueKey(caseValue),
      label: caseValueLabel(caseValue),
      caseValue,
      disabled,
      reason: disabled ? "该分支已存在" : undefined,
    };
  });
  return [...entityOptions, ...specialOptions];
}

export function getAllowedSpecializations(
  split: MicroflowInheritanceSplit | undefined,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog,
): string[] {
  if (!split) {
    return [];
  }
  if (split.entity.allowedSpecializations.length > 0) {
    return split.entity.allowedSpecializations;
  }
  return findEntity(metadata, split.entity.generalizedEntityQualifiedName)?.specializations ?? [];
}

export function getCaseOptionsForSource(
  schema: MicroflowSchema,
  sourceObjectId: string,
  currentFlowId?: string,
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog,
): MicroflowCaseOption[] {
  const kind = getCaseEditorKind(schema, sourceObjectId);
  if (kind === "enumeration") {
    return getEnumerationCaseOptions(schema, sourceObjectId, currentFlowId, metadata);
  }
  if (kind === "objectType") {
    return getObjectTypeCaseOptions(schema, sourceObjectId, currentFlowId, metadata);
  }
  return getBooleanCaseOptions(schema, sourceObjectId, currentFlowId);
}
