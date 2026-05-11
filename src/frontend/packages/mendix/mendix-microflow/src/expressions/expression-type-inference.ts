import {
  getAssociationByQualifiedName,
  getAttributeByQualifiedName,
  getEntityByQualifiedName,
  getEnumerationByQualifiedName,
  getAssociationsForEntity,
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

const BOOLEAN_TYPE: MicroflowDataType = { kind: "boolean" };
const STRING_TYPE: MicroflowDataType = { kind: "string" };
const INTEGER_TYPE: MicroflowDataType = { kind: "integer" };
const LONG_TYPE: MicroflowDataType = { kind: "long" };
const DECIMAL_TYPE: MicroflowDataType = { kind: "decimal" };
const DATETIME_TYPE: MicroflowDataType = { kind: "dateTime" };

const FUNCTION_RETURN_TYPES: Record<string, MicroflowDataType | ((args: MicroflowDataType[]) => MicroflowDataType)> = {
  empty: BOOLEAN_TYPE,
  toString: STRING_TYPE,
  toLowerCase: STRING_TYPE,
  toUpperCase: STRING_TYPE,
  substring: STRING_TYPE,
  trim: STRING_TYPE,
  replaceAll: STRING_TYPE,
  replaceFirst: STRING_TYPE,
  urlEncode: STRING_TYPE,
  urlDecode: STRING_TYPE,
  htmlEncode: STRING_TYPE,
  contains: BOOLEAN_TYPE,
  startsWith: BOOLEAN_TYPE,
  endsWith: BOOLEAN_TYPE,
  isMatch: BOOLEAN_TYPE,
  isNew: BOOLEAN_TYPE,
  isSynced: BOOLEAN_TYPE,
  find: INTEGER_TYPE,
  findLast: INTEGER_TYPE,
  round: DECIMAL_TYPE,
  floor: LONG_TYPE,
  ceil: LONG_TYPE,
  abs: args => args[0] && args[0].kind !== "unknown" ? args[0] : DECIMAL_TYPE,
  pow: DECIMAL_TYPE,
  sqrt: DECIMAL_TYPE,
  max: args => args.some(arg => arg.kind === "decimal") ? DECIMAL_TYPE : INTEGER_TYPE,
  min: args => args.some(arg => arg.kind === "decimal") ? DECIMAL_TYPE : INTEGER_TYPE,
  random: DECIMAL_TYPE,
  formatDateTime: STRING_TYPE,
  toDateTime: DATETIME_TYPE,
  dateTime: DATETIME_TYPE,
  addDays: DATETIME_TYPE,
  addMonths: DATETIME_TYPE,
  addYears: DATETIME_TYPE,
  addHours: DATETIME_TYPE,
  addMinutes: DATETIME_TYPE,
  addSeconds: DATETIME_TYPE,
  addWeeks: DATETIME_TYPE,
  addQuarters: DATETIME_TYPE,
  getYear: INTEGER_TYPE,
  getMonth: INTEGER_TYPE,
  getDay: INTEGER_TYPE,
  getHour: INTEGER_TYPE,
  getMinute: INTEGER_TYPE,
  getSecond: INTEGER_TYPE,
  getDayOfWeek: INTEGER_TYPE,
  dateDiff: INTEGER_TYPE,
  currentDateTime: DATETIME_TYPE,
  parseInteger: INTEGER_TYPE,
  parseDecimal: DECIMAL_TYPE,
  formatDecimal: STRING_TYPE,
};

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

function sameEntityToken(raw: string, entityQualifiedName: string): boolean {
  return raw === entityQualifiedName || raw === entityQualifiedName.split(".").pop();
}

function inferMemberTypeFromPath(dataType: MicroflowDataType, path: string[], metadata: MicroflowMetadataCatalog): MicroflowDataType {
  if (path.length === 0) {
    return dataType;
  }
  if (dataType.kind === "list") {
    return { kind: "unknown", reason: "list member access" };
  }
  if (dataType.kind !== "object") {
    return { kind: "unknown", reason: "member access on non-object" };
  }
  const entityQn = dataType.entityQualifiedName;
  const entity = getEntityByQualifiedName(metadata, entityQn);
  const [segment, ...rest] = path;
  const attribute = entity?.attributes.find(item => item.name === segment || item.qualifiedName.endsWith(`.${segment}`))
    ?? getAttributeByQualifiedName(metadata, `${entityQn}.${segment}`);
  if (attribute) {
    return rest.length === 0 ? attribute.type : { kind: "unknown", reason: `${entityQn}/${segment}` };
  }
  const association = getAssociationByQualifiedName(metadata, segment)
    ?? getAssociationByQualifiedName(metadata, `${entityQn}_${segment}`)
    ?? getAssociationsForEntity(metadata, entityQn).find(item => item.name === segment || item.qualifiedName === segment);
  if (!association) {
    return { kind: "unknown", reason: `${entityQn}/${segment}` };
  }
  const target = getTargetEntityByAssociation(metadata, association.qualifiedName, entityQn);
  if (!target) {
    return { kind: "unknown", reason: "association target" };
  }
  const targetType: MicroflowDataType = { kind: "object", entityQualifiedName: target.qualifiedName };
  const remaining = rest[0] && sameEntityToken(rest[0], target.qualifiedName) ? rest.slice(1) : rest;
  if (association.multiplicity === "oneToMany" || association.multiplicity === "manyToMany") {
    return remaining.length === 0 ? { kind: "list", itemType: targetType } : { kind: "unknown", reason: "association list member access" };
  }
  return inferMemberTypeFromPath(targetType, remaining, metadata);
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
      return inferMemberTypeFromPath(variable.dataType, ast.path, metadata);
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
      if (ast.operator === "/") {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_USE_DIV_OPERATOR",
          message: "Mendix 表达式不能用 / 做除法；请使用 div 或 :。",
          severity: "error",
          range: ast.range,
        }));
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
      if (ast.operator === "div" || ast.operator === ":") {
        return { kind: "decimal" };
      }
      if (ast.operator === "mod") {
        return left.kind === "long" || right.kind === "long" ? { kind: "long" } : { kind: "integer" };
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
      {
        const argTypes = ast.args.map(arg => inferAstType({ ...input, ast: arg }));
        const returnType = FUNCTION_RETURN_TYPES[ast.functionName];
        if (!returnType) {
          diagnostics.push(expressionDiagnostic({
            code: "MF_EXPR_UNSUPPORTED_FUNCTION",
            message: `Function "${ast.functionName}" is not supported by the P0 expression subset.`,
            severity: "warning",
            range: ast.range,
          }));
          return { kind: "unknown", reason: ast.functionName };
        }
        return typeof returnType === "function" ? returnType(argTypes) : returnType;
      }
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
  fallbackVariables?: MicroflowVariableSymbol[]
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
      return fallbackVariables?.find(variable => variable.name === variableMatch[1] || variable.name === `$${variableMatch[1]}`)?.dataType ?? { kind: "unknown", reason: raw };
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
