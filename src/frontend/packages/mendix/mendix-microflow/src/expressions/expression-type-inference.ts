import {
  getAssociationByQualifiedName,
  getAttributeByQualifiedName,
  getEntityByQualifiedName,
  getEnumerationByQualifiedName,
  getMicroflowById,
  getTargetEntityByAssociation,
  EMPTY_MICROFLOW_METADATA_CATALOG,
  type MicroflowMetadataCatalog,
} from "../metadata/metadata-catalog";
import type { MicroflowDataType, MicroflowExpression, MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import type { MicroflowExpressionAstNode } from "./expression-ast";
import { parseExpression } from "./expression-parser";
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
  const dataType = variable.dataType;
  if (dataType.kind === "list") {
    return { kind: "unknown", reason: "list member access" };
  }
  if (dataType.kind !== "object") {
    return { kind: "unknown", reason: "member access on non-object" };
  }
  const entityQn = dataType.entityQualifiedName;
  const entity = getEntityByQualifiedName(metadata, entityQn);
  const attribute = entity?.attributes.find(item => item.name === memberName || item.qualifiedName.endsWith(`.${memberName}`)) ?? getAttributeByQualifiedName(metadata, `${entityQn}.${memberName}`);
  if (attribute) {
    return attribute.type;
  }
  const association = getAssociationByQualifiedName(metadata, `${entityQn}_${memberName}`)
    ?? metadata.associations.find(item => item.name === memberName && (item.sourceEntityQualifiedName === entityQn || item.targetEntityQualifiedName === entityQn));
  if (association) {
    const target = getTargetEntityByAssociation(metadata, association.qualifiedName, entityQn);
    const targetType: MicroflowDataType = target ? { kind: "object", entityQualifiedName: target.qualifiedName } : { kind: "unknown", reason: "association target" };
    return association.multiplicity === "oneToMany" || association.multiplicity === "manyToMany" ? { kind: "list", itemType: targetType } : targetType;
  }
  return { kind: "unknown", reason: `${entityQn}/${memberName}` };
}

function isNumberType(dataType: MicroflowDataType): boolean {
  return dataType.kind === "integer" || dataType.kind === "long" || dataType.kind === "decimal";
}

function inferEnumValueType(raw: string, expectedType: MicroflowDataType | undefined, metadata: MicroflowMetadataCatalog): MicroflowDataType {
  if (expectedType?.kind === "enumeration") {
    const expected = getEnumerationByQualifiedName(metadata, expectedType.enumerationQualifiedName);
    const valueName = raw.split(".").pop() ?? raw;
    if (!expected || expected.values.some(value => value.key === valueName)) {
      return expectedType;
    }
  }
  for (const enumeration of metadata.enumerations) {
    if (raw.startsWith(`${enumeration.qualifiedName}.`)) {
      return { kind: "enumeration", enumerationQualifiedName: enumeration.qualifiedName };
    }
  }
  return { kind: "unknown", reason: `enum ${raw}` };
}

function inferAstType(input: {
  ast: MicroflowExpressionAstNode;
  schema: MicroflowSchema;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
  expectedType?: MicroflowDataType;
  diagnostics: ExpressionTypeInferenceResult["diagnostics"];
}): MicroflowDataType {
  const { ast, metadata, diagnostics } = input;
  switch (ast.kind) {
    case "literal":
      if (ast.literalKind === "boolean") {
        return { kind: "boolean" };
      }
      if (ast.literalKind === "integer") {
        return { kind: "integer" };
      }
      if (ast.literalKind === "decimal") {
        return { kind: "decimal" };
      }
      if (ast.literalKind === "string") {
        return { kind: "string" };
      }
      return { kind: "unknown", reason: ast.literalKind };
    case "variable": {
      const variable = input.objectId
        ? resolveVariableReferenceFromIndex(input.schema, input.variableIndex, { objectId: input.objectId, actionId: input.actionId, fieldPath: input.fieldPath }, ast.variableName)
        : null;
      return variable?.dataType ?? { kind: "unknown", reason: ast.variableName };
    }
    case "memberAccess": {
      const variable = input.objectId
        ? resolveVariableReferenceFromIndex(input.schema, input.variableIndex, { objectId: input.objectId, actionId: input.actionId, fieldPath: input.fieldPath }, ast.variableName)
        : null;
      if (!variable) {
        return { kind: "unknown", reason: ast.variableName };
      }
      if (variable.dataType.kind === "list") {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_LIST_MEMBER_ACCESS",
          message: "List variables cannot access attributes directly. Use a loop first.",
          severity: "warning",
          range: ast.range,
          variableName: ast.variableName,
          memberName: ast.path[0],
        }));
        return { kind: "unknown", reason: "list member access" };
      }
      const first = inferMemberType(variable, ast.path[0], metadata);
      if (ast.path.length > 1) {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_PARTIAL_PATH_INFERENCE",
          message: "Only the first member path segment is inferred in this editor version.",
          severity: "warning",
          range: ast.range,
        }));
        return { kind: "unknown", reason: "multi-level path" };
      }
      return first;
    }
    case "binary": {
      const left = inferAstType({ ...input, ast: ast.left });
      const right = inferAstType({ ...input, ast: ast.right });
      if (["=", "!=", ">", "<", ">=", "<="].includes(ast.operator)) {
        return { kind: "boolean" };
      }
      if (ast.operator === "and" || ast.operator === "or") {
        return { kind: "boolean" };
      }
      if (!isNumberType(left) || !isNumberType(right)) {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_ARITHMETIC_TYPE_MISMATCH",
          message: "Arithmetic operators require number operands.",
          severity: "warning",
          range: ast.range,
          actualType: left.kind === "unknown" ? right : left,
        }));
      }
      return left.kind === "integer" && right.kind === "integer" ? { kind: "integer" } : { kind: "decimal" };
    }
    case "unary": {
      const argument = inferAstType({ ...input, ast: ast.argument });
      if (ast.operator === "not") {
        return { kind: "boolean" };
      }
      return isNumberType(argument) ? argument : { kind: "unknown", reason: "unary number" };
    }
    case "functionCall":
      if (ast.functionName === "empty") {
        return { kind: "boolean" };
      }
      if (ast.functionName === "toString") {
        return { kind: "string" };
      }
      diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_UNSUPPORTED_FUNCTION",
        message: `Function "${ast.functionName}" is not supported by the P0 expression subset.`,
        severity: "warning",
        range: ast.range,
      }));
      return { kind: "unknown", reason: ast.functionName };
    case "if": {
      const thenType = inferAstType({ ...input, ast: ast.thenBranch });
      const elseType = inferAstType({ ...input, ast: ast.elseBranch });
      if (sameMicroflowDataType(thenType, elseType)) {
        return thenType.kind === "unknown" ? elseType : thenType;
      }
      diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_IF_BRANCH_TYPE_MISMATCH",
        message: "Then and else branches have different types.",
        severity: "warning",
        range: ast.range,
        expectedType: thenType,
        actualType: elseType,
      }));
      return { kind: "unknown", reason: "if branches" };
    }
    case "enumValue":
      return inferEnumValueType(ast.qualifiedName, input.expectedType, metadata);
    default:
      return { kind: "unknown", reason: ast.reason };
  }
}

export function inferExpressionType(input: {
  expression: MicroflowExpression | string | undefined;
  schema: MicroflowSchema;
  metadata?: MicroflowMetadataCatalog;
  variableIndex?: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
    collectionId?: string;
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
    collectionId?: string;
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

  const metadata = inputOrExpression.metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const index = inputOrExpression.variableIndex ?? buildVariableIndex(inputOrExpression.schema, metadata);
  const raw = rawExpression(inputOrExpression.expression).trim();
  const diagnostics: ExpressionTypeInferenceResult["diagnostics"] = [];
  if (inputOrExpression.metadata == null) {
    diagnostics.push(expressionDiagnostic({
      code: "MF_METADATA_CATALOG_MISSING",
      message: "元数据目录未提供，无法进行完整类型推断。",
      severity: "warning",
    }));
  }
  if (!raw) {
    return { inferredType: { kind: "unknown", reason: "empty expression" }, confidence: "low", diagnostics };
  }
  const parse = parseExpression(raw);
  diagnostics.push(...parse.diagnostics);
  const inferredType = inferAstType({
    ast: parse.ast,
    schema: inputOrExpression.schema,
    metadata,
    variableIndex: index,
    objectId: inputOrExpression.objectId,
    actionId: inputOrExpression.actionId,
    fieldPath: inputOrExpression.fieldPath,
    expectedType: inputOrExpression.expectedType,
    diagnostics,
  });
  if (inferredType.kind !== "unknown") {
    return { inferredType, confidence: diagnostics.some(item => item.severity === "warning" || item.severity === "error") ? "medium" : "high", diagnostics, references: parse.references };
  }
  const microflow = getMicroflowById(metadata, raw);
  if (microflow) {
    return { inferredType: microflow.returnType, confidence: "medium", diagnostics, references: parse.references };
  }
  return {
    inferredType: inputOrExpression.expression && typeof inputOrExpression.expression !== "string"
      ? inputOrExpression.expression.inferredType ?? inferredType
      : inferredType,
    confidence: "low",
    diagnostics,
    references: parse.references,
  };
}
