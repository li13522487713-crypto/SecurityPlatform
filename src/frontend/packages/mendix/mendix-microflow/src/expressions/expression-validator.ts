import {
  getAssociationByQualifiedName,
  getAttributeByQualifiedName,
  getEntityByQualifiedName,
  EMPTY_MICROFLOW_METADATA_CATALOG,
  type MicroflowMetadataCatalog,
} from "../metadata/metadata-catalog";
import type { MicroflowDataType, MicroflowExpression, MicroflowSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema/types";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import { parseExpressionReferences } from "./expression-reference-parser";
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
  const parse = parseExpressionReferences(raw);
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
    }
  }
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
