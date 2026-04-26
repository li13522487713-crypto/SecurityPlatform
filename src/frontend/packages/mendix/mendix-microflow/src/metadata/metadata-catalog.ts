import type { MicroflowDataType } from "../schema/types";

export interface MetadataModule {
  id: string;
  name: string;
  qualifiedName: string;
  description?: string;
}

export interface MetadataAttribute {
  id: string;
  name: string;
  qualifiedName: string;
  type: MicroflowDataType;
  required: boolean;
  defaultValue?: string;
  documentation?: string;
  enumQualifiedName?: string;
  isReadonly?: boolean;
}

export interface MetadataAssociationRef {
  associationQualifiedName: string;
  targetEntityQualifiedName: string;
  direction: "sourceToTarget" | "targetToSource" | "bidirectional";
  multiplicity: "oneToOne" | "oneToMany" | "manyToOne" | "manyToMany";
}

export interface MetadataEntity {
  id: string;
  name: string;
  qualifiedName: string;
  moduleName: string;
  documentation?: string;
  attributes: MetadataAttribute[];
  associations: MetadataAssociationRef[];
  generalization?: string;
  specializations: string[];
  isPersistable: boolean;
  isSystemEntity?: boolean;
}

export interface MetadataAssociation {
  id: string;
  name: string;
  qualifiedName: string;
  sourceEntityQualifiedName: string;
  targetEntityQualifiedName: string;
  ownerEntityQualifiedName?: string;
  multiplicity: "oneToOne" | "oneToMany" | "manyToOne" | "manyToMany";
  direction: "sourceToTarget" | "targetToSource" | "bidirectional";
  documentation?: string;
}

export interface MetadataEnumerationValue {
  key: string;
  caption: string;
  description?: string;
  colorToken?: string;
}

export interface MetadataEnumeration {
  id: string;
  name: string;
  qualifiedName: string;
  moduleName: string;
  values: MetadataEnumerationValue[];
  documentation?: string;
}

export interface MetadataMicroflowParameter {
  name: string;
  type: MicroflowDataType;
  required: boolean;
  documentation?: string;
}

export interface MetadataMicroflowRef {
  id: string;
  name: string;
  qualifiedName: string;
  moduleName: string;
  description?: string;
  parameters: MetadataMicroflowParameter[];
  returnType: MicroflowDataType;
  status?: "draft" | "published" | "archived";
}

export interface MetadataPageParameter {
  name: string;
  type: MicroflowDataType;
  required: boolean;
}

export interface MetadataPageRef {
  id: string;
  name: string;
  qualifiedName: string;
  moduleName: string;
  description?: string;
  parameters: MetadataPageParameter[];
}

export interface MetadataWorkflowRef {
  id: string;
  name: string;
  qualifiedName: string;
  moduleName: string;
  contextEntityQualifiedName?: string;
  description?: string;
}

export interface MetadataConnector {
  id: string;
  name: string;
  type: string;
  enabled: boolean;
  capabilities: string[];
}

export interface MicroflowMetadataCatalog {
  entities: MetadataEntity[];
  associations: MetadataAssociation[];
  enumerations: MetadataEnumeration[];
  microflows: MetadataMicroflowRef[];
  pages: MetadataPageRef[];
  workflows: MetadataWorkflowRef[];
  modules: MetadataModule[];
  connectors?: MetadataConnector[];
  version?: string;
}

/** 显式空目录：metadata 未加载且不做 mock 回落时使用。 */
export const EMPTY_MICROFLOW_METADATA_CATALOG: MicroflowMetadataCatalog = {
  modules: [],
  entities: [],
  associations: [],
  enumerations: [],
  microflows: [],
  pages: [],
  workflows: [],
  version: "empty",
};

export type MicroflowEntityAttribute = MetadataAttribute;
export type MicroflowEntityRef = MetadataEntity;
export type MicroflowAssociationRef = MetadataAssociation;
export type MicroflowEnumerationRef = MetadataEnumeration;
export type MicroflowCallableParameter = MetadataMicroflowParameter;
export type MicroflowRef = MetadataMicroflowRef;
export type PageRef = MetadataPageRef;
export type WorkflowRef = MetadataWorkflowRef;

export function createMetadataCatalog(data: MicroflowMetadataCatalog): MicroflowMetadataCatalog {
  return data;
}

function includesKeyword(values: Array<string | undefined>, keyword: string): boolean {
  const normalized = keyword.trim().toLowerCase();
  if (!normalized) {
    return true;
  }
  return values.some(value => value?.toLowerCase().includes(normalized));
}

export function getEntityByQualifiedName(catalog: MicroflowMetadataCatalog, qualifiedName?: string) {
  return catalog.entities.find(entity => entity.qualifiedName === qualifiedName);
}

export const findEntity = getEntityByQualifiedName;

/** 将简名或未限定名解析为 catalog 中的 qualifiedName（用于旧 demo 数据兼容）。 */
export function resolveStoredEntityQualifiedName(catalog: MicroflowMetadataCatalog, value?: string): string | undefined {
  if (!value) {
    return undefined;
  }
  if (getEntityByQualifiedName(catalog, value)) {
    return value;
  }
  const byName = catalog.entities.find(entity => entity.name === value);
  if (byName) {
    return byName.qualifiedName;
  }
  const bySuffix = catalog.entities.find(entity => entity.qualifiedName.endsWith(`.${value}`));
  return bySuffix?.qualifiedName ?? value;
}

export function getEntityAttributes(catalog: MicroflowMetadataCatalog, entityQualifiedName?: string): MetadataAttribute[] {
  return getEntityByQualifiedName(catalog, entityQualifiedName)?.attributes ?? [];
}

export function getAttributeByQualifiedName(catalog: MicroflowMetadataCatalog, attributeQualifiedName?: string) {
  return catalog.entities.flatMap(entity => entity.attributes).find(attribute => attribute.qualifiedName === attributeQualifiedName);
}

export function getAssociationsForEntity(catalog: MicroflowMetadataCatalog, entityQualifiedName?: string): MetadataAssociation[] {
  if (!entityQualifiedName) {
    return [];
  }
  return catalog.associations.filter(association =>
    association.sourceEntityQualifiedName === entityQualifiedName ||
    association.targetEntityQualifiedName === entityQualifiedName
  );
}

export function getAssociationByQualifiedName(catalog: MicroflowMetadataCatalog, associationQualifiedName?: string) {
  return catalog.associations.find(association => association.qualifiedName === associationQualifiedName);
}

export const findAssociation = getAssociationByQualifiedName;

export function getTargetEntityByAssociation(
  catalog: MicroflowMetadataCatalog,
  associationQualifiedName?: string,
  startEntityQualifiedName?: string,
) {
  const association = getAssociationByQualifiedName(catalog, associationQualifiedName);
  if (!association) {
    return undefined;
  }
  const target = startEntityQualifiedName === association.targetEntityQualifiedName
    ? association.sourceEntityQualifiedName
    : association.targetEntityQualifiedName;
  return getEntityByQualifiedName(catalog, target);
}

export function getEnumerationByQualifiedName(catalog: MicroflowMetadataCatalog, enumerationQualifiedName?: string) {
  return catalog.enumerations.find(enumeration => enumeration.qualifiedName === enumerationQualifiedName);
}

export const findEnumeration = getEnumerationByQualifiedName;

export function getEnumerationValues(catalog: MicroflowMetadataCatalog, enumerationQualifiedName?: string): MetadataEnumerationValue[] {
  return getEnumerationByQualifiedName(catalog, enumerationQualifiedName)?.values ?? [];
}

export function getEnumerationValueKeys(catalog: MicroflowMetadataCatalog, enumerationQualifiedName?: string): string[] {
  return getEnumerationValues(catalog, enumerationQualifiedName).map(value => value.key);
}

export function getMicroflowById(catalog: MicroflowMetadataCatalog, id?: string) {
  return catalog.microflows.find(microflow => microflow.id === id);
}

export function getMicroflowByQualifiedName(catalog: MicroflowMetadataCatalog, qualifiedName?: string) {
  return catalog.microflows.find(microflow => microflow.qualifiedName === qualifiedName);
}

export function getPageById(catalog: MicroflowMetadataCatalog, id?: string) {
  return catalog.pages.find(page => page.id === id);
}

export function getWorkflowById(catalog: MicroflowMetadataCatalog, id?: string) {
  return catalog.workflows.find(workflow => workflow.id === id);
}

export function getSpecializations(catalog: MicroflowMetadataCatalog, generalizedEntityQualifiedName?: string): string[] {
  const entity = getEntityByQualifiedName(catalog, generalizedEntityQualifiedName);
  if (entity?.specializations.length) {
    return entity.specializations;
  }
  return catalog.entities.filter(candidate => candidate.generalization === generalizedEntityQualifiedName).map(candidate => candidate.qualifiedName);
}

export function isEntitySpecializationOf(catalog: MicroflowMetadataCatalog, childEntityQualifiedName: string, parentEntityQualifiedName: string): boolean {
  let current = getEntityByQualifiedName(catalog, childEntityQualifiedName);
  while (current?.generalization) {
    if (current.generalization === parentEntityQualifiedName) {
      return true;
    }
    current = getEntityByQualifiedName(catalog, current.generalization);
  }
  return false;
}

export function searchEntities(catalog: MicroflowMetadataCatalog, keyword = ""): MetadataEntity[] {
  return catalog.entities.filter(entity => includesKeyword([entity.name, entity.qualifiedName, entity.documentation], keyword));
}

export function searchAttributes(catalog: MicroflowMetadataCatalog, entityQualifiedName?: string, keyword = ""): MetadataAttribute[] {
  return getEntityAttributes(catalog, entityQualifiedName).filter(attribute => includesKeyword([attribute.name, attribute.qualifiedName, attribute.documentation], keyword));
}

export function searchAssociations(catalog: MicroflowMetadataCatalog, entityQualifiedName?: string, keyword = ""): MetadataAssociation[] {
  return getAssociationsForEntity(catalog, entityQualifiedName).filter(association => includesKeyword([association.name, association.qualifiedName, association.documentation], keyword));
}

export function searchEnumerations(catalog: MicroflowMetadataCatalog, keyword = ""): MetadataEnumeration[] {
  return catalog.enumerations.filter(enumeration => includesKeyword([enumeration.name, enumeration.qualifiedName, enumeration.documentation], keyword));
}

export function searchMicroflows(catalog: MicroflowMetadataCatalog, keyword = ""): MetadataMicroflowRef[] {
  return catalog.microflows.filter(microflow => includesKeyword([microflow.name, microflow.qualifiedName, microflow.description], keyword));
}

export function searchPages(catalog: MicroflowMetadataCatalog, keyword = ""): MetadataPageRef[] {
  return catalog.pages.filter(page => includesKeyword([page.name, page.qualifiedName, page.description], keyword));
}

export function searchWorkflows(catalog: MicroflowMetadataCatalog, keyword = ""): MetadataWorkflowRef[] {
  return catalog.workflows.filter(workflow => includesKeyword([workflow.name, workflow.qualifiedName, workflow.description], keyword));
}
