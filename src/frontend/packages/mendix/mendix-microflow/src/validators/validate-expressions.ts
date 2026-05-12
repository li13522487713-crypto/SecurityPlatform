import { getAttributeByQualifiedName, type MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowDataType, MicroflowExpression, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import { validateExpression } from "../expressions";
import { flattenObjects, issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

interface ExpressionTarget {
  expression?: MicroflowExpression | string;
  objectId?: string;
  issueObjectId?: string;
  actionId?: string;
  parameterId?: string;
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
      { objectId: target.issueObjectId ?? target.objectId, actionId: target.actionId, parameterId: target.parameterId, fieldPath: target.fieldPath, source: "expression" },
      diagnostic.severity
    ));
  const compatibilityIssues = result.diagnostics
    .filter(diagnostic => diagnostic.severity === "error" && diagnostic.code.startsWith("MF_EXPR"))
    .map(diagnostic => issue(
      diagnostic.code === "MF_EXPR_UNKNOWN_VARIABLE" ? "MF_EXPRESSION_UNKNOWN_VARIABLE" : "MF_EXPRESSION_INVALID",
      diagnostic.message,
      { objectId: target.issueObjectId ?? target.objectId, actionId: target.actionId, parameterId: target.parameterId, fieldPath: target.fieldPath, source: "expression" },
      diagnostic.severity
    ));
  return [...issues, ...compatibilityIssues];
}

export function validateExpressions(schema: MicroflowSchema, context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const targets: ExpressionTarget[] = [];
  const { metadata, variableIndex } = context;
  const parameterObjectIds = new Map(
    flattenObjects(schema.objectCollection)
      .filter(item => item.object.kind === "parameterObject")
      .map(item => [item.object.parameterId, item.object.id] as const),
  );
  schema.parameters.forEach(parameter => {
    if (!parameter.defaultValue) {
      return;
    }
    targets.push({
      expression: parameter.defaultValue,
      issueObjectId: parameterObjectIds.get(parameter.id),
      parameterId: parameter.id,
      fieldPath: `parameters.${parameter.id}.defaultValue`,
      expectedType: parameter.dataType,
    });
  });
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
    if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "rule") {
      object.splitCondition.parameterMappings.forEach((mapping, index) => {
        targets.push({
          expression: mapping.argumentExpression,
          objectId: object.id,
          fieldPath: `splitCondition.parameterMappings.${index}.argumentExpression`,
          expectedType: mapping.targetType ?? mapping.parameterType,
          required: true,
        });
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
    if (object.kind === "errorEvent" && object.error.messageExpression) {
      targets.push({
        expression: object.error.messageExpression,
        objectId: object.id,
        fieldPath: "error.messageExpression",
        expectedType: { kind: "string" },
      });
    }
    if (object.kind !== "actionActivity") {
      continue;
    }
    const action = object.action;
    if (action.kind === "retrieve" && action.retrieveSource.kind === "database") {
      if (action.retrieveSource.xPathConstraint) {
        targets.push({
          expression: action.retrieveSource.xPathConstraint,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.retrieveSource.xPathConstraint",
          expectedType: { kind: "boolean" },
        });
      }
      if (action.retrieveSource.range.kind === "custom") {
        targets.push({
          expression: action.retrieveSource.range.limitExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.retrieveSource.range.limitExpression",
          expectedType: { kind: "integer" },
          required: true,
        });
        if (action.retrieveSource.range.offsetExpression) {
          targets.push({
            expression: action.retrieveSource.range.offsetExpression,
            objectId: object.id,
            actionId: action.id,
            fieldPath: "action.retrieveSource.range.offsetExpression",
            expectedType: { kind: "integer" },
          });
        }
      }
    }
    if (action.kind === "changeMembers" || action.kind === "createObject") {
      action.memberChanges.forEach((change, index) => {
        if (change.assignmentKind === "clear" || !change.valueExpression) {
          return;
        }
        targets.push({
          expression: change.valueExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: `action.memberChanges.${index}.valueExpression`,
          expectedType: getAttributeByQualifiedName(metadata, change.memberQualifiedName)?.type,
          required: true,
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
      if (action.request.body.kind === "form") {
        action.request.body.fields.forEach((field, index) => targets.push({
          expression: field.valueExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: `action.request.body.fields.${index}.valueExpression`,
          expectedType: { kind: "string" },
        }));
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
      action.template.arguments.forEach((argument, index) => targets.push({
        expression: argument,
        objectId: object.id,
        actionId: action.id,
        fieldPath: `action.template.arguments.${index}`,
      }));
    }
    if (action.kind === "createVariable" && action.initialValue) {
      targets.push({
        expression: action.initialValue,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.initialValue",
        expectedType: action.dataType,
      });
    }
    if (action.kind === "createList" && action.initialItemsExpression) {
      targets.push({
        expression: action.initialItemsExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.initialItemsExpression",
        expectedType: { kind: "list", itemType: action.elementType },
      });
    }
    if (action.kind === "changeVariable") {
      targets.push({
        expression: action.newValueExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.newValueExpression",
        expectedType: resolveVariableReferenceFromIndex(schema, variableIndex, { objectId: object.id, actionId: action.id, fieldPath: "action.targetVariableName" }, action.targetVariableName)?.dataType,
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
        required: mapping.parameterType != null && mapping.parameterType.kind !== "void",
      }));
    }
    if (action.kind === "changeList") {
      if (action.itemExpression) {
        targets.push({
          expression: action.itemExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.itemExpression",
        });
      }
      if (action.itemsExpression) {
        targets.push({
          expression: action.itemsExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.itemsExpression",
        });
      }
      if (action.conditionExpression) {
        targets.push({
          expression: action.conditionExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.conditionExpression",
          expectedType: { kind: "boolean" },
        });
      }
      if (action.indexExpression) {
        targets.push({
          expression: action.indexExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.indexExpression",
          expectedType: { kind: "integer" },
        });
      }
    }
    if (action.kind === "aggregateList" && action.aggregateFunction !== "count" && action.aggregateExpression) {
      targets.push({
        expression: action.aggregateExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.aggregateExpression",
        required: true,
      });
    }
    if (action.kind === "listOperation") {
      if (action.expression) {
        targets.push({
          expression: action.expression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.expression",
        });
      }
      if (action.filterExpression) {
        targets.push({
          expression: action.filterExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.filterExpression",
          expectedType: { kind: "boolean" },
        });
      }
      if (action.sortExpression) {
        targets.push({
          expression: action.sortExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.sortExpression",
        });
      }
    }
    if (action.kind === "filterList") {
      if (action.conditionExpression) {
        targets.push({
          expression: action.conditionExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.conditionExpression",
          expectedType: { kind: "boolean" },
        });
      }
      if (action.filterExpression) {
        targets.push({
          expression: action.filterExpression,
          objectId: object.id,
          actionId: action.id,
          fieldPath: "action.filterExpression",
          expectedType: { kind: "boolean" },
        });
      }
    }
    if (action.kind === "sortList" && action.sortExpression) {
      targets.push({
        expression: action.sortExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.sortExpression",
      });
    }
    if (action.kind === "sortList" && action.sortKeys) {
      action.sortKeys.forEach((sortKey, index) => {
        if (sortKey.expression) {
          targets.push({
            expression: sortKey.expression,
            objectId: object.id,
            actionId: action.id,
            fieldPath: `action.sortKeys.${index}.expression`,
          });
        }
      });
    }
    if (action.kind === "showMessage") {
      targets.push({
        expression: action.messageExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.messageExpression",
        expectedType: { kind: "string" },
        required: true,
      });
    }
    if (action.kind === "counter" || action.kind === "gauge") {
      targets.push({
        expression: action.valueExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.valueExpression",
        expectedType: { kind: "integer" },
        required: true,
      });
    }
    if (action.kind === "throwException" && action.messageExpression) {
      targets.push({
        expression: action.messageExpression,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.messageExpression",
        expectedType: { kind: "string" },
      });
    }
    if (action.kind === "notifyWorkflow") {
      targets.push({
        expression: action.payloadExpression as MicroflowExpression | undefined,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.payloadExpression",
      });
    }
  }
  return targets.flatMap(target => expressionIssues(schema, metadata, variableIndex, target));
}
