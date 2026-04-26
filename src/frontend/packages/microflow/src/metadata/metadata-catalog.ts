import type { MicroflowDataType } from "../schema/types";

export interface MicroflowEntityAttribute {
  name: string;
  qualifiedName: string;
  type: MicroflowDataType;
  required: boolean;
  enumRef?: string;
}

export interface MicroflowEntityRef {
  qualifiedName: string;
  attributes: MicroflowEntityAttribute[];
  associations: string[];
  generalization?: string;
  specializations: string[];
}

export interface MicroflowAssociationRef {
  qualifiedName: string;
  sourceEntity: string;
  targetEntity: string;
  multiplicity: "one" | "many";
  owner: "source" | "target" | "both" | "none";
}

export interface MicroflowEnumerationRef {
  qualifiedName: string;
  values: string[];
}

export interface MicroflowCallableParameter {
  name: string;
  type: MicroflowDataType;
  required: boolean;
}

export interface MicroflowRef {
  id: string;
  qualifiedName: string;
  parameters: MicroflowCallableParameter[];
  returnType: MicroflowDataType;
}

export interface PageRef {
  id: string;
  name: string;
  parameters: MicroflowCallableParameter[];
}

export interface WorkflowRef {
  id: string;
  qualifiedName: string;
  parameters: MicroflowCallableParameter[];
}

export interface MicroflowMetadataCatalog {
  entities: MicroflowEntityRef[];
  associations: MicroflowAssociationRef[];
  enumerations: MicroflowEnumerationRef[];
  microflows: MicroflowRef[];
  pages: PageRef[];
  workflows: WorkflowRef[];
  enabledConnectors: string[];
}

export function findEntity(metadata: MicroflowMetadataCatalog, qualifiedName: string | undefined) {
  return metadata.entities.find(entity => entity.qualifiedName === qualifiedName);
}

export function findAssociation(metadata: MicroflowMetadataCatalog, qualifiedName: string | undefined) {
  return metadata.associations.find(association => association.qualifiedName === qualifiedName);
}

export function findEnumeration(metadata: MicroflowMetadataCatalog, qualifiedName: string | undefined) {
  return metadata.enumerations.find(enumeration => enumeration.qualifiedName === qualifiedName);
}
