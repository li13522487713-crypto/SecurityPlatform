import {
  getAssociationByQualifiedName,
  getAttributeByQualifiedName,
  getEntityByQualifiedName,
  getEnumerationByQualifiedName,
  getEnumerationValueKeys,
  getMicroflowById,
  getSpecializations,
  mockMicroflowMetadataCatalog,
  type MicroflowMetadataCatalog,
} from "../metadata";
import type { MicroflowDataType, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

function validateDataType(
  dataType: MicroflowDataType,
  target: Partial<Pick<MicroflowValidationIssue, "objectId" | "actionId" | "fieldPath">>,
  catalog: MicroflowMetadataCatalog,
): MicroflowValidationIssue[] {
  if (dataType.kind === "object" && !getEntityByQualifiedName(catalog, dataType.entityQualifiedName)) {
    return [issue("MF_METADATA_ENTITY_NOT_FOUND", "Referenced entity does not exist in metadata catalog.", target)];
  }
  if (dataType.kind === "enumeration" && !getEnumerationByQualifiedName(catalog, dataType.enumerationQualifiedName)) {
    return [issue("MF_METADATA_ENUMERATION_NOT_FOUND", "Referenced enumeration does not exist in metadata catalog.", target)];
  }
  if (dataType.kind === "list") {
    return validateDataType(dataType.itemType, target, catalog);
  }
  return [];
}

export function validateMetadataReferences(
  schema: MicroflowSchema,
  catalog: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog,
): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];

  for (const parameter of schema.parameters) {
    issues.push(...validateDataType(parameter.dataType, { fieldPath: `parameters.${parameter.id}.dataType` }, catalog));
  }

  for (const { object } of flattenObjects(schema.objectCollection)) {
    if (object.kind === "parameterObject") {
      const parameter = schema.parameters.find(item => item.id === object.parameterId);
      if (parameter) {
        issues.push(...validateDataType(parameter.dataType, { objectId: object.id, fieldPath: "parameter.dataType" }, catalog));
      }
    }

    if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression" && object.splitCondition.resultType === "enumeration") {
      if (object.splitCondition.enumerationQualifiedName && !getEnumerationByQualifiedName(catalog, object.splitCondition.enumerationQualifiedName)) {
        issues.push(issue("MF_METADATA_ENUMERATION_NOT_FOUND", "Decision enumeration does not exist in metadata catalog.", { objectId: object.id, fieldPath: "splitCondition.enumerationQualifiedName" }));
      }
    }

    if (object.kind === "inheritanceSplit") {
      if (object.generalizedEntityQualifiedName && !getEntityByQualifiedName(catalog, object.generalizedEntityQualifiedName)) {
        issues.push(issue("MF_METADATA_ENTITY_NOT_FOUND", "Object type decision generalized entity does not exist in metadata catalog.", { objectId: object.id, fieldPath: "generalizedEntityQualifiedName" }));
      }
      const allowed = getSpecializations(catalog, object.generalizedEntityQualifiedName);
      for (const specialization of object.allowedSpecializations) {
        if (!allowed.includes(specialization)) {
          issues.push(issue("MF_METADATA_SPECIALIZATION_NOT_FOUND", "Allowed specialization is not valid for the selected generalized entity.", { objectId: object.id, fieldPath: "allowedSpecializations" }));
        }
      }
    }

    if (object.kind !== "actionActivity") {
      continue;
    }

    const { action } = object;
    if (action.kind === "retrieve") {
      if (action.retrieveSource.kind === "database") {
        const entity = getEntityByQualifiedName(catalog, action.retrieveSource.entityQualifiedName ?? undefined);
        if (action.retrieveSource.entityQualifiedName && !entity) {
          issues.push(issue("MF_METADATA_ENTITY_NOT_FOUND", "Retrieve entity does not exist in metadata catalog.", { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.entityQualifiedName" }));
        }
        for (const sortItem of action.retrieveSource.sortItemList.items) {
          const attribute = getAttributeByQualifiedName(catalog, sortItem.attributeQualifiedName);
          if (!attribute || attribute.qualifiedName.split(".").slice(0, -1).join(".") !== action.retrieveSource.entityQualifiedName) {
            issues.push(issue("MF_METADATA_ATTRIBUTE_NOT_FOUND", "Retrieve sort attribute must belong to the selected entity.", { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.sortItemList" }));
          }
        }
      }
      if (action.retrieveSource.kind === "association" && action.retrieveSource.associationQualifiedName && !getAssociationByQualifiedName(catalog, action.retrieveSource.associationQualifiedName)) {
        issues.push(issue("MF_METADATA_ASSOCIATION_NOT_FOUND", "Retrieve association does not exist in metadata catalog.", { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.associationQualifiedName" }));
      }
    }

    if (action.kind === "createObject") {
      if (action.entityQualifiedName && !getEntityByQualifiedName(catalog, action.entityQualifiedName)) {
        issues.push(issue("MF_METADATA_ENTITY_NOT_FOUND", "CreateObject entity does not exist in metadata catalog.", { objectId: object.id, actionId: action.id, fieldPath: "action.entityQualifiedName" }));
      }
      for (const change of action.memberChanges) {
        if (change.memberQualifiedName && !getAttributeByQualifiedName(catalog, change.memberQualifiedName)) {
          issues.push(issue("MF_METADATA_ATTRIBUTE_NOT_FOUND", "Member change attribute does not exist in metadata catalog.", { objectId: object.id, actionId: action.id, fieldPath: "action.memberChanges" }));
        }
      }
    }

    if (action.kind === "changeMembers") {
      for (const change of action.memberChanges) {
        if (change.memberQualifiedName && !getAttributeByQualifiedName(catalog, change.memberQualifiedName)) {
          issues.push(issue("MF_METADATA_ATTRIBUTE_NOT_FOUND", "Member change attribute does not exist in metadata catalog.", { objectId: object.id, actionId: action.id, fieldPath: "action.memberChanges" }));
        }
      }
    }

    if (action.kind === "callMicroflow" && action.targetMicroflowId && !getMicroflowById(catalog, action.targetMicroflowId)) {
      issues.push(issue("MF_METADATA_MICROFLOW_NOT_FOUND", "Target microflow does not exist in metadata catalog.", { objectId: object.id, actionId: action.id, fieldPath: "action.targetMicroflowId" }));
    }

    if (action.kind === "createVariable") {
      issues.push(...validateDataType(action.dataType, { objectId: object.id, actionId: action.id, fieldPath: "action.dataType" }, catalog));
    }
  }

  for (const flow of schema.flows) {
    if (flow.kind !== "sequence") {
      continue;
    }
    const source = flattenObjects(schema.objectCollection).find(item => item.object.id === flow.originObjectId)?.object;
    if (source?.kind === "exclusiveSplit" && source.splitCondition.kind === "expression" && source.splitCondition.resultType === "enumeration") {
      const values = getEnumerationValueKeys(catalog, source.splitCondition.enumerationQualifiedName);
      for (const caseValue of flow.caseValues) {
        if (caseValue.kind === "enumeration" && !values.includes(caseValue.value)) {
          issues.push(issue("MF_METADATA_ENUMERATION_VALUE_NOT_FOUND", "Enumeration case value does not exist in metadata catalog.", { objectId: source.id, flowId: flow.id, fieldPath: "caseValues" }));
        }
      }
    }
    if (source?.kind === "inheritanceSplit") {
      const specializations = getSpecializations(catalog, source.generalizedEntityQualifiedName);
      for (const caseValue of flow.caseValues) {
        if (caseValue.kind === "inheritance" && !specializations.includes(caseValue.entityQualifiedName)) {
          issues.push(issue("MF_METADATA_SPECIALIZATION_NOT_FOUND", "Object type case does not belong to the selected generalized entity.", { objectId: source.id, flowId: flow.id, fieldPath: "caseValues" }));
        }
      }
    }
  }

  return issues;
}
