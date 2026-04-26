import {
  getAssociationByQualifiedName,
  getAttributeByQualifiedName,
  getEntityByQualifiedName,
  getMicroflowById,
  getTargetEntityByAssociation,
  mockMicroflowMetadataCatalog,
  type MicroflowMetadataCatalog,
} from "../metadata";
import type { MicroflowDataType, MicroflowExpression, MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import { parseExpressionReferences } from "./expression-reference-parser";
import { expressionDiagnostic, type ExpressionTypeInferenceResult } from "./expression-types";

export function sameMicroflowDataType(left: MicroflowDataType | undefined, right: MicroflowDataType | undefined): boolean {
  if (!left || !right || left.kind === "unknown" || right.kind === "unknown") {
    return true;
  }
  if (left.kind !== right.kind) {
    return false;
  }
  if (left.kind === "object" && right.kind === "object") {
    return left.entityQualifiedName === right.entityQualifiedName;
  }
  if (left.kind === "list" && right.kind === "list") {
    return sameMicroflowDataType(left.itemType, right.itemType);
  }
  if (left.kind === "enumeration" && right.kind === "enumeration") {
    return left.enumerationQualifiedName === right.enumerationQualifiedName;
  }
  return true;
}

function rawExpression(expression: MicroflowExpression | string | undefined): string {
  return typeof expression === "string" ? expression : expression?.raw ?? expression?.text ?? "";
}

function inferMemberType(variable: MicroflowVariableSymbol, memberName: string, metadata: MicroflowMetadataCatalog): MicroflowDataType {
  if (variable.dataType.kind === "list") {
    return { kind: "unknown", reason: "list member access" };
  }
  if (variable.dataType.kind !== "object") {
    return { kind: "unknown", reason: "member access on non-object" };
  }
  const entity = getEntityByQualifiedName(metadata, variable.dataType.entityQualifiedName);
  const attribute = entity?.attributes.find(item => item.name === memberName || item.qualifiedName.endsWith(`.${memberName}`)) ?? getAttributeByQualifiedName(metadata, `${variable.dataType.entityQualifiedName}.${memberName}`);
  if (attribute) {
    return attribute.type;
  }
  const association = getAssociationByQualifiedName(metadata, `${variable.dataType.entityQualifiedName}_${memberName}`)
    ?? metadata.associations.find(item => item.name === memberName && (item.sourceEntityQualifiedName === variable.dataType.entityQualifiedName || item.targetEntityQualifiedName === variable.dataType.entityQualifiedName));
  if (association) {
    const target = getTargetEntityByAssociation(metadata, association.qualifiedName, variable.dataType.entityQualifiedName);
    const targetType: MicroflowDataType = target ? { kind: "object", entityQualifiedName: target.qualifiedName } : { kind: "unknown", reason: "association target" };
    return association.multiplicity === "oneToMany" || association.multiplicity === "manyToMany" ? { kind: "list", itemType: targetType } : targetType;
  }
  return { kind: "unknown", reason: `${variable.dataType.entityQualifiedName}/${memberName}` };
}

export function inferExpressionType(input: {
  expression: MicroflowExpression | string | undefined;
  schema: MicroflowSchema;
  metadata?: MicroflowMetadataCatalog;
  variableIndex?: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
  expectedType?: MicroflowDataType;
}): ExpressionTypeInferenceResult;
export function inferExpressionType(expression: MicroflowExpression | undefined, variables: MicroflowVariableSymbol[]): MicroflowDataType;
export function inferExpressionType(
  inputOrExpression: {
    expression: MicroflowExpression | string | undefined;
    schema: MicroflowSchema;
    metadata?: MicroflowMetadataCatalog;
    variableIndex?: MicroflowVariableIndex;
    objectId?: string;
    actionId?: string;
    fieldPath?: string;
    expectedType?: MicroflowDataType;
  } | MicroflowExpression | undefined,
  legacyVariables?: MicroflowVariableSymbol[]
): ExpressionTypeInferenceResult | MicroflowDataType {
  if (!inputOrExpression || !("schema" in inputOrExpression)) {
    const raw = rawExpression(inputOrExpression);
    if (/^(true|false)$/i.test(raw.trim()) || /[<>=!]=?| and | or |^not\(/.test(raw)) {
      return { kind: "boolean" };
    }
    if (/^'.*'$|^".*"$/.test(raw.trim())) {
      return { kind: "string" };
    }
    if (/^\d+$/.test(raw.trim())) {
      return { kind: "integer" };
    }
    if (/^\d+\.\d+$/.test(raw.trim())) {
      return { kind: "decimal" };
    }
    const variableMatch = raw.trim().match(/^\$([A-Za-z_][\w]*)$/);
    if (variableMatch) {
      return legacyVariables?.find(variable => variable.name === variableMatch[1] || variable.name === `$${variableMatch[1]}`)?.dataType ?? { kind: "unknown", reason: raw };
    }
    return inputOrExpression?.inferredType ?? { kind: "unknown", reason: "expression" };
  }

  const metadata = inputOrExpression.metadata ?? mockMicroflowMetadataCatalog;
  const index = inputOrExpression.variableIndex ?? buildVariableIndex(inputOrExpression.schema, metadata);
  const raw = rawExpression(inputOrExpression.expression).trim();
  const diagnostics: ExpressionTypeInferenceResult["diagnostics"] = [];
  if (!raw) {
    return { inferredType: { kind: "unknown", reason: "empty expression" }, confidence: "low", diagnostics };
  }
  if (/^(true|false)$/i.test(raw) || /[<>=!]=?| and | or |^not\(|^empty\(/i.test(raw)) {
    return { inferredType: { kind: "boolean" }, confidence: "high", diagnostics };
  }
  if (/^'.*'$|^".*"$/.test(raw)) {
    return { inferredType: { kind: "string" }, confidence: "high", diagnostics };
  }
  if (/^\d+$/.test(raw)) {
    return { inferredType: { kind: "integer" }, confidence: "high", diagnostics };
  }
  if (/^\d+\.\d+$/.test(raw)) {
    return { inferredType: { kind: "decimal" }, confidence: "high", diagnostics };
  }
  const parse = parseExpressionReferences(raw);
  const memberAccess = parse.references.find(reference => reference.kind === "memberAccess");
  if (memberAccess && inputOrExpression.objectId) {
    const variable = resolveVariableReferenceFromIndex(inputOrExpression.schema, index, inputOrExpression, memberAccess.variableName);
    if (!variable) {
      return { inferredType: { kind: "unknown", reason: memberAccess.variableName }, confidence: "low", diagnostics };
    }
    const inferredType = inferMemberType(variable, memberAccess.memberName, metadata);
    if (memberAccess.path.length > 1) {
      diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_PARTIAL_PATH_INFERENCE",
        message: "Only the first member path segment is inferred in this editor version.",
        severity: "warning",
        range: memberAccess.range,
      }));
      return { inferredType: { kind: "unknown", reason: "multi-level path" }, confidence: "low", diagnostics };
    }
    return { inferredType, confidence: inferredType.kind === "unknown" ? "low" : "high", diagnostics };
  }
  const variableOnly = raw.match(/^\$([A-Za-z_][A-Za-z0-9_]*)$/);
  if (variableOnly && inputOrExpression.objectId) {
    const variable = resolveVariableReferenceFromIndex(inputOrExpression.schema, index, inputOrExpression, variableOnly[1]);
    return { inferredType: variable?.dataType ?? { kind: "unknown", reason: variableOnly[1] }, confidence: variable ? "high" : "low", diagnostics };
  }
  if (inputOrExpression.expectedType?.kind === "enumeration" && /^[A-Za-z_][A-Za-z0-9_.]*$|^'.*'$/.test(raw)) {
    return { inferredType: inputOrExpression.expectedType, confidence: "medium", diagnostics };
  }
  const microflow = getMicroflowById(metadata, raw);
  if (microflow) {
    return { inferredType: microflow.returnType, confidence: "medium", diagnostics };
  }
  return { inferredType: inputOrExpression.expression && typeof inputOrExpression.expression !== "string" ? inputOrExpression.expression.inferredType ?? { kind: "unknown", reason: "expression" } : { kind: "unknown", reason: "expression" }, confidence: "low", diagnostics };
}
