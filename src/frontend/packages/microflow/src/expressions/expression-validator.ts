import type { MicroflowMetadataCatalog } from "../metadata";
import { findEntity, mockMicroflowMetadataCatalog } from "../metadata";
import type { MicroflowDataType, MicroflowExpression, MicroflowValidationIssue } from "../schema/types";
import type { MicroflowExpressionScopeContext } from "../variables";
import { getVariablesForExpression, resolveVariableReference } from "../variables";
import { parseExpressionReferences } from "./expression-reference-parser";
import { inferExpressionType, sameMicroflowDataType } from "./expression-type-inference";

export function validateExpression(
  schema: Parameters<typeof getVariablesForExpression>[0],
  expression: MicroflowExpression | undefined,
  context: MicroflowExpressionScopeContext & { expectedType?: MicroflowDataType },
  metadata: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog
): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const references = parseExpressionReferences(expression);
  const variables = getVariablesForExpression(schema, context, metadata);
  for (const variableName of references.variables) {
    const variable = resolveVariableReference(schema, context, variableName, metadata);
    if (!variable) {
      issues.push({
        id: `MF_EXPRESSION_UNKNOWN_VARIABLE:${context.objectId}:${context.fieldPath ?? ""}:${variableName}`,
        code: "MF_EXPRESSION_UNKNOWN_VARIABLE",
        severity: "error",
        message: `Variable "$${variableName}" is not available in this context.`,
        objectId: context.objectId,
        actionId: context.actionId,
        fieldPath: context.fieldPath
      });
    }
  }
  for (const access of references.attributeAccesses) {
    const variable = resolveVariableReference(schema, context, access.variableName, metadata);
    if (!variable || variable.dataType.kind !== "object") {
      continue;
    }
    const entity = findEntity(metadata, variable.dataType.entityQualifiedName);
    if (entity && !entity.attributes.some(attribute => attribute.name === access.attributeName || attribute.qualifiedName.endsWith(`.${access.attributeName}`))) {
      issues.push({
        id: `MF_EXPRESSION_UNKNOWN_ATTRIBUTE:${context.objectId}:${access.variableName}/${access.attributeName}`,
        code: "MF_EXPRESSION_UNKNOWN_ATTRIBUTE",
        severity: "error",
        message: `Attribute "${access.attributeName}" does not exist on ${variable.dataType.entityQualifiedName}.`,
        objectId: context.objectId,
        actionId: context.actionId,
        fieldPath: context.fieldPath
      });
    }
  }
  if (context.expectedType) {
    const inferred = inferExpressionType(expression, variables);
    if (!sameMicroflowDataType(inferred, context.expectedType)) {
      issues.push({
        id: `MF_EXPRESSION_TYPE_MISMATCH:${context.objectId}:${context.fieldPath ?? ""}`,
        code: "MF_EXPRESSION_TYPE_MISMATCH",
        severity: "error",
        message: `Expression type does not match expected ${context.expectedType.kind}.`,
        objectId: context.objectId,
        actionId: context.actionId,
        fieldPath: context.fieldPath
      });
    }
  }
  return issues;
}
