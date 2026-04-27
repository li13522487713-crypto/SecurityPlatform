import {
  getAssociationByQualifiedName,
  getAttributeByQualifiedName,
  getEntityByQualifiedName,
  EMPTY_MICROFLOW_METADATA_CATALOG,
  type MicroflowMetadataCatalog,
} from "../metadata/metadata-catalog";
import type { MicroflowDataType, MicroflowExpression, MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import type { MicroflowExpressionAstNode } from "./expression-ast";
import { parseExpression } from "./expression-parser";
import { inferExpressionType, sameMicroflowDataType } from "./expression-type-inference";
import { expressionDiagnostic, type ExpressionDiagnostic, type ExpressionValidationResult } from "./expression-types";

function rawExpression(expression: MicroflowExpression | string | undefined): string {
  return typeof expression === "string" ? expression : expression?.raw ?? expression?.text ?? "";
}

function isSystemErrorVariable(name: string): boolean {
  return name === "$latestError" || name === "$latestHttpResponse" || name === "$latestSoapFault";
}

function memberExists(variable: MicroflowVariableSymbol, memberName: string, metadata: MicroflowMetadataCatalog): boolean {
  const dataType = variable.dataType;
  if (dataType.kind !== "object") {
    return false;
  }
  const entityQn = dataType.entityQualifiedName;
  const entity = getEntityByQualifiedName(metadata, entityQn);
  return Boolean(
    entity?.attributes.some(attribute => attribute.name === memberName || attribute.qualifiedName.endsWith(`.${memberName}`)) ||
    getAttributeByQualifiedName(metadata, `${entityQn}.${memberName}`) ||
    getAssociationByQualifiedName(metadata, `${entityQn}_${memberName}`) ||
    metadata.associations.some(association => association.name === memberName && (association.sourceEntityQualifiedName === entityQn || association.targetEntityQualifiedName === entityQn))
  );
}

function typeMismatchSeverity(actual: MicroflowDataType, expected: MicroflowDataType): ExpressionDiagnostic["severity"] {
  if (actual.kind === "unknown" || expected.kind === "unknown") {
    return "warning";
  }
  return "error";
}

function isBooleanType(dataType: MicroflowDataType): boolean {
  return dataType.kind === "boolean" || dataType.kind === "unknown";
}

function isNumberType(dataType: MicroflowDataType): boolean {
  return dataType.kind === "integer" || dataType.kind === "long" || dataType.kind === "decimal" || dataType.kind === "unknown";
}

function validateAstOperators(input: {
  ast: MicroflowExpressionAstNode;
  schema: MicroflowSchema;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  context: {
    objectId?: string;
    actionId?: string;
    fieldPath?: string;
  };
  diagnostics: ExpressionDiagnostic[];
}): void {
  const infer = (ast: MicroflowExpressionAstNode): MicroflowDataType => inferExpressionType({
    expression: ast.raw,
    schema: input.schema,
    metadata: input.metadata,
    variableIndex: input.variableIndex,
    objectId: input.context.objectId,
    actionId: input.context.actionId,
    fieldPath: input.context.fieldPath,
  }).inferredType;
  switch (input.ast.kind) {
    case "binary": {
      validateAstOperators({ ...input, ast: input.ast.left });
      validateAstOperators({ ...input, ast: input.ast.right });
      const left = infer(input.ast.left);
      const right = infer(input.ast.right);
      if ((input.ast.operator === "and" || input.ast.operator === "or") && (!isBooleanType(left) || !isBooleanType(right))) {
        input.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_BOOLEAN_OPERATOR_TYPE_MISMATCH",
          message: `${input.ast.operator} requires boolean operands.`,
          severity: "error",
          range: input.ast.range,
          expectedType: { kind: "boolean" },
          actualType: left.kind === "boolean" ? right : left,
        }));
      }
      if (["+", "-", "*", "/"].includes(input.ast.operator) && (!isNumberType(left) || !isNumberType(right))) {
        input.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_ARITHMETIC_TYPE_MISMATCH",
          message: "Arithmetic operators require number operands.",
          severity: "error",
          range: input.ast.range,
          actualType: left.kind === "unknown" ? right : left,
        }));
      }
      if (["=", "!=", ">", "<", ">=", "<="].includes(input.ast.operator) && left.kind !== "unknown" && right.kind !== "unknown" && !sameMicroflowDataType(left, right) && !(isNumberType(left) && isNumberType(right))) {
        input.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_COMPARISON_TYPE_MISMATCH",
          message: "Comparison operands have incompatible types.",
          severity: "warning",
          range: input.ast.range,
          expectedType: left,
          actualType: right,
        }));
      }
      break;
    }
    case "unary": {
      validateAstOperators({ ...input, ast: input.ast.argument });
      const argument = infer(input.ast.argument);
      if (input.ast.operator === "not" && !isBooleanType(argument)) {
        input.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_BOOLEAN_OPERATOR_TYPE_MISMATCH",
          message: "not requires a boolean operand.",
          severity: "error",
          range: input.ast.range,
          expectedType: { kind: "boolean" },
          actualType: argument,
        }));
      }
      break;
    }
    case "functionCall":
      input.ast.args.forEach(arg => validateAstOperators({ ...input, ast: arg }));
      if (input.ast.functionName === "empty" && input.ast.args.length !== 1) {
        input.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_FUNCTION_ARGUMENT_COUNT",
          message: "empty() requires exactly one argument.",
          severity: "error",
          range: input.ast.range,
        }));
      } else if (input.ast.functionName !== "empty" && input.ast.functionName !== "toString") {
        input.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_UNSUPPORTED_FUNCTION",
          message: `Function "${input.ast.functionName}" is not supported by the P0 expression subset.`,
          severity: "warning",
          range: input.ast.range,
        }));
      }
      break;
    case "if": {
      validateAstOperators({ ...input, ast: input.ast.condition });
      validateAstOperators({ ...input, ast: input.ast.thenBranch });
      validateAstOperators({ ...input, ast: input.ast.elseBranch });
      const condition = infer(input.ast.condition);
      if (!isBooleanType(condition)) {
        input.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_IF_CONDITION_TYPE_MISMATCH",
          message: "If condition must be boolean.",
          severity: "error",
          range: input.ast.condition.range,
          expectedType: { kind: "boolean" },
          actualType: condition,
        }));
      }
      break;
    }
    case "unknown":
      input.ast.children?.forEach(child => validateAstOperators({ ...input, ast: child }));
      break;
    default:
      break;
  }
}

export function validateExpression(input: {
  expression: MicroflowExpression | string | undefined;
  schema: MicroflowSchema;
  metadata?: MicroflowMetadataCatalog;
  variableIndex?: MicroflowVariableIndex;
  context: {
    objectId?: string;
    actionId?: string;
    flowId?: string;
    fieldPath?: string;
    expectedType?: MicroflowDataType;
    required?: boolean;
    allowMaybeVariables?: boolean;
  };
}): ExpressionValidationResult {
  const metadata = input.metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const variableIndex = input.variableIndex ?? buildVariableIndex(input.schema, metadata);
  const raw = rawExpression(input.expression);
  const parse = parseExpression(raw);
  const diagnostics: ExpressionDiagnostic[] = [...parse.diagnostics];
  if (input.metadata == null) {
    diagnostics.push(expressionDiagnostic({
      code: "MF_METADATA_CATALOG_MISSING",
      message: "元数据目录未提供，成员与关联类型校验可能不完整。",
      severity: "warning",
    }));
  }
  if (input.context.required && !raw.trim()) {
    diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_REQUIRED",
      message: "Expression is required.",
      severity: "error",
    }));
  }
  if (input.context.expectedType?.kind === "void" && raw.trim()) {
    diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_TYPE_MISMATCH",
      message: "Void return does not accept an expression.",
      severity: "error",
      expectedType: input.context.expectedType,
    }));
  }
  for (const reference of parse.references) {
    if (reference.kind === "functionCall") {
      continue;
    }
    const variableName = reference.kind === "variable" ? reference.variableName : reference.variableName;
    const variable = input.context.objectId
      ? resolveVariableReferenceFromIndex(input.schema, variableIndex, { objectId: input.context.objectId, actionId: input.context.actionId, fieldPath: input.context.fieldPath }, variableName)
      : null;
    if (!variable) {
      if (variableName === "currentIndex") {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_LOOP_VARIABLE_OUT_OF_SCOPE",
          message: "$currentIndex is only valid inside Loop scope.",
          severity: "error",
          range: reference.range,
          variableName,
        }));
        continue;
      }
      if (variableName === "latestError" || variableName === "latestHttpResponse" || variableName === "latestSoapFault") {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_ERROR_VARIABLE_OUT_OF_SCOPE",
          message: `$${variableName} is only valid inside error handler scope.`,
          severity: "error",
          range: reference.range,
          variableName,
        }));
        continue;
      }
      diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_UNKNOWN_VARIABLE",
        message: `Variable "$${variableName}" is not available in this context.`,
        severity: "error",
        range: reference.range,
        variableName,
      }));
      continue;
    }
    if (variable.visibility === "maybe" && !input.context.allowMaybeVariables) {
      diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_MAYBE_VARIABLE",
        message: `Variable "$${variableName}" may not be assigned on every incoming path.`,
        severity: "warning",
        range: reference.range,
        variableName,
      }));
    }
    if (variable.name === "$currentIndex" && variable.scope.kind !== "loop") {
      diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_LOOP_VARIABLE_OUT_OF_SCOPE",
        message: "$currentIndex is only valid inside Loop scope.",
        severity: "error",
        range: reference.range,
        variableName,
      }));
    }
    if (isSystemErrorVariable(variable.name) && variable.scope.kind !== "errorHandler") {
      diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_ERROR_VARIABLE_OUT_OF_SCOPE",
        message: `${variable.name} is only valid inside error handler scope.`,
        severity: "error",
        range: reference.range,
        variableName,
      }));
    }
    if (reference.kind === "memberAccess") {
      if (variable.dataType.kind === "list") {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_INVALID_MEMBER_ACCESS",
          message: "List variables cannot access attributes directly. Use a loop or list operation first.",
          severity: "warning",
          range: reference.range,
          variableName,
          memberName: reference.memberName,
        }));
      } else if (variable.dataType.kind !== "object") {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_INVALID_MEMBER_ACCESS",
          message: `Variable "$${variableName}" is not an object and cannot access members.`,
          severity: "error",
          range: reference.range,
          variableName,
          memberName: reference.memberName,
        }));
      } else if (!getEntityByQualifiedName(metadata, variable.dataType.entityQualifiedName)) {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_ENTITY_NOT_FOUND",
          message: `Entity "${variable.dataType.entityQualifiedName}" is not found in metadata.`,
          severity: "error",
          range: reference.range,
          variableName,
          memberName: reference.memberName,
        }));
      } else if (!memberExists(variable, reference.memberName, metadata)) {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_MEMBER_NOT_FOUND",
          message: `Member "${reference.memberName}" does not exist on ${variable.dataType.entityQualifiedName}.`,
          severity: "error",
          range: reference.range,
          variableName,
          memberName: reference.memberName,
        }));
      }
      if (reference.path.length > 1) {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_PARTIAL_PATH_INFERENCE",
          message: "Multi-level member access is parsed but only the first segment is fully validated in this version.",
          severity: "warning",
          range: reference.range,
          variableName,
          memberName: reference.memberName,
        }));
      }
    }
  }
  validateAstOperators({
    ast: parse.ast,
    schema: input.schema,
    metadata,
    variableIndex,
    context: {
      objectId: input.context.objectId,
      actionId: input.context.actionId,
      fieldPath: input.context.fieldPath,
    },
    diagnostics,
  });
  const inference = inferExpressionType({
    expression: input.expression,
    schema: input.schema,
    metadata,
    variableIndex,
    objectId: input.context.objectId,
    actionId: input.context.actionId,
    fieldPath: input.context.fieldPath,
    expectedType: input.context.expectedType,
  });
  diagnostics.push(...inference.diagnostics);
  if (input.context.expectedType && raw.trim() && inference.inferredType.kind === "unknown") {
    diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_UNKNOWN_TYPE",
      message: `Expression type is unknown while "${input.context.expectedType.kind}" is expected.`,
      severity: "warning",
      expectedType: input.context.expectedType,
      actualType: inference.inferredType,
    }));
  }
  if (input.context.expectedType && raw.trim() && !sameMicroflowDataType(inference.inferredType, input.context.expectedType)) {
    diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_TYPE_MISMATCH",
      message: `Expression type "${inference.inferredType.kind}" does not match expected "${input.context.expectedType.kind}".`,
      severity: typeMismatchSeverity(inference.inferredType, input.context.expectedType),
      expectedType: input.context.expectedType,
      actualType: inference.inferredType,
    }));
  }
  return {
    references: parse.references,
    diagnostics,
    inferredType: inference.inferredType,
    confidence: inference.confidence,
  };
}
