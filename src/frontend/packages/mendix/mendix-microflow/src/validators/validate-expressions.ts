import { getAttributeByQualifiedName, type MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowDataType, MicroflowExpression, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import { validateExpression } from "../expressions";
import { flattenObjects, issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

interface ExpressionTarget {
  expression?: MicroflowExpression | string;
  objectId: string;
  actionId?: string;
  fieldPath: string;
  expectedType?: MicroflowDataType;
  required?: boolean;
}

function expressionIssues(schema: MicroflowSchema, metadata: MicroflowMetadataCatalog, variableIndex: MicroflowValidatorContext["variableIndex"], target: ExpressionTarget): MicroflowValidationIssue[] {
  const result = validateExpression({
    expression: target.expression,
    schema,
    metadata,
    variableIndex,
    context: {
      objectId: target.objectId,
      actionId: target.actionId,
      fieldPath: target.fieldPath,
      expectedType: target.expectedType,
      required: target.required,
    },
  });
  const issues = result.diagnostics.map(diagnostic => issue(
    diagnostic.code,
    diagnostic.message,
    { objectId: target.objectId, actionId: target.actionId, fieldPath: target.fieldPath, source: "expression" },
    diagnostic.severity
  ));
  const legacyCompatibilityIssues = result.diagnostics
    .filter(diagnostic => diagnostic.severity === "error" && diagnostic.code.startsWith("MF_EXPR"))
    .map(diagnostic => issue(
      diagnostic.code === "MF_EXPR_UNKNOWN_VARIABLE" ? "MF_EXPRESSION_UNKNOWN_VARIABLE" : "MF_EXPRESSION_INVALID",
      diagnostic.message,
      { objectId: target.objectId, actionId: target.actionId, fieldPath: target.fieldPath, source: "expression" },
      diagnostic.severity
    ));
  return [...issues, ...legacyCompatibilityIssues];
}

export function validateExpressions(schema: MicroflowSchema, context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const targets: ExpressionTarget[] = [];
  const { metadata, variableIndex } = context;
  for (const { object } of flattenObjects(schema.objectCollection)) {
    if (object.kind === "endEvent") {
      targets.push({
        expression: object.returnValue,
        objectId: object.id,
        fieldPath: "returnValue",
        expectedType: schema.returnType,
        required: schema.returnType.kind !== "void",
      });
    }
    if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression") {
      targets.push({
        expression: object.splitCondition.expression,
        objectId: object.id,
        fieldPath: "splitCondition.expression",
        expectedType: object.splitCondition.resultType === "enumeration"
          ? { kind: "enumeration", enumerationQualifiedName: object.splitCondition.enumerationQualifiedName ?? "" }
          : { kind: "boolean" },
        required: true,
      });
    }
    if (object.kind === "loopedActivity" && object.loopSource.kind === "whileCondition") {
      targets.push({
        expression: object.loopSource.expression,
        objectId: object.id,
        fieldPath: "loopSource.expression",
        expectedType: { kind: "boolean" },
        required: true,
      });
    }
    if (object.kind !== "actionActivity") {
      continue;
    }
    const action = object.action;
    if (action.kind === "retrieve" && action.retrieveSource.kind === "database" && action.retrieveSource.xPathConstraint) {
      targets.push({
        expression: action.retrieveSource.xPathConstraint,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.retrieveSource.xPathConstraint",
        expectedType: { kind: "boolean" },
      });
    }
    if (action.kind === "changeMembers" || action.kind === "createObject") {
      action.memberChanges.forEach((change, index) => {
        targets.push({
          expression: change.valueExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: `action.memberChanges.${index}.valueExpression`,
          expectedType: getAttributeByQualifiedName(metadata, change.memberQualifiedName)?.type,
          required: change.assignmentKind !== "clear",
        });
      });
    }
    if (action.kind === "restCall") {
      targets.push({
        expression: action.request.urlExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.request.urlExpression",
        expectedType: { kind: "string" },
        required: true,
      });
      action.request.headers.forEach((header, index) => targets.push({
        expression: header.valueExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: `action.request.headers.${index}.valueExpression`,
        expectedType: { kind: "string" },
      }));
      action.request.queryParameters.forEach((parameter, index) => targets.push({
        expression: parameter.valueExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: `action.request.queryParameters.${index}.valueExpression`,
        expectedType: { kind: "string" },
      }));
      if (action.request.body.kind === "json" || action.request.body.kind === "text") {
        targets.push({
          expression: action.request.body.expression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.request.body.expression",
          expectedType: action.request.body.kind === "json" ? { kind: "json" } : { kind: "string" },
        });
      }
    }
    if (action.kind === "logMessage") {
      targets.push({
        expression: action.template.text,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.template.text",
        expectedType: { kind: "string" },
      });
    }
    if (action.kind === "createVariable") {
      targets.push({
        expression: action.initialValue,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.initialValue",
        expectedType: action.dataType,
      });
    }
    if (action.kind === "changeVariable") {
      targets.push({
        expression: action.valueExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.valueExpression",
        expectedType: resolveVariableReferenceFromIndex(schema, variableIndex, { objectId: object.id, actionId: action.id, fieldPath: "action.variableName" }, action.variableName)?.dataType,
        required: true,
      });
    }
    if (action.kind === "callMicroflow") {
      action.parameterMappings.forEach((mapping, index) => targets.push({
        expression: mapping.argumentExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: `action.parameterMappings.${index}.argumentExpression`,
        expectedType: mapping.parameterType,
        required: mapping.parameterType.kind !== "void",
      }));
    }
  }
  return targets.flatMap(target => expressionIssues(schema, metadata, variableIndex, target));
}
