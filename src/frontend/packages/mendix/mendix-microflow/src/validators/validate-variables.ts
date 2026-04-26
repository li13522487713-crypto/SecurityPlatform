import type { MicroflowDataType, MicroflowObject, MicroflowObjectCollection, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import { issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

function compatibleKinds(dataType: MicroflowDataType, expected: MicroflowDataType["kind"][]): boolean {
  return expected.includes(dataType.kind);
}

function variableIssue(
  code: string,
  message: string,
  objectId: string,
  actionId: string | undefined,
  fieldPath: string,
  severity: MicroflowValidationIssue["severity"] = "error",
): MicroflowValidationIssue {
  return issue(code, message, { objectId, actionId, fieldPath }, severity);
}

function checkVariableReference(input: {
  schema: MicroflowSchema;
  metadata: MicroflowValidatorContext["metadata"];
  objectId: string;
  actionId?: string;
  fieldPath: string;
  value: string;
  expectedKinds: MicroflowDataType["kind"][];
  label: string;
  issues: MicroflowValidationIssue[];
}) {
  const index = buildVariableIndex(input.schema, input.metadata);
  const symbol = resolveVariableReferenceFromIndex(input.schema, index, { objectId: input.objectId, actionId: input.actionId, fieldPath: input.fieldPath }, input.value);
  if (!input.value.trim()) {
    input.issues.push(variableIssue("MF_VARIABLE_REFERENCE_REQUIRED", `${input.label} variable is required.`, input.objectId, input.actionId, input.fieldPath));
    return;
  }
  if (!symbol) {
    input.issues.push(variableIssue("MF_VARIABLE_REFERENCE_UNKNOWN", `Variable "${input.value}" is not available at this node.`, input.objectId, input.actionId, input.fieldPath));
    return;
  }
  if (!compatibleKinds(symbol.dataType, input.expectedKinds)) {
    input.issues.push(variableIssue("MF_VARIABLE_TYPE_MISMATCH", `Variable "${input.value}" has type "${symbol.dataType.kind}" but ${input.label} expects ${input.expectedKinds.join(" or ")}.`, input.objectId, input.actionId, input.fieldPath));
  }
  if (symbol.visibility === "maybe") {
    input.issues.push(variableIssue("MF_VARIABLE_MAYBE_SCOPE", `Variable "${input.value}" may not be assigned on every incoming path.`, input.objectId, input.actionId, input.fieldPath, "warning"));
  }
}

export function validateVariables(schema: MicroflowSchema, context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const { metadata } = context;
  const index = buildVariableIndex(schema, metadata);
  const issues: MicroflowValidationIssue[] = (index.diagnostics ?? []).map(diagnostic => issue(
    diagnostic.code,
    diagnostic.message,
    {
      objectId: diagnostic.objectId,
      actionId: diagnostic.actionId,
      flowId: diagnostic.flowId,
      fieldPath: diagnostic.fieldPath,
    },
    diagnostic.severity === "info" ? "info" : diagnostic.severity
  ));
  const collectObjects = (collection: MicroflowObjectCollection): MicroflowObject[] =>
    collection.objects.flatMap(object => object.kind === "loopedActivity"
      ? [object, ...collectObjects(object.objectCollection)]
      : [object]);
  const objects = collectObjects(schema.objectCollection);
  for (const object of objects) {
    if (object.kind === "loopedActivity" && object.loopSource.kind === "iterableList") {
      checkVariableReference({
        schema,
        metadata,
        objectId: object.id,
        fieldPath: "loopSource.listVariableName",
        value: object.loopSource.listVariableName,
        expectedKinds: ["list"],
        label: "Loop source",
        issues,
      });
      continue;
    }
    if (object.kind !== "actionActivity") {
      continue;
    }
    const action = object.action;
    if (action.kind === "retrieve" && action.retrieveSource.kind === "association") {
      checkVariableReference({
        schema,
        metadata,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.retrieveSource.startVariableName",
        value: action.retrieveSource.startVariableName,
        expectedKinds: ["object"],
        label: "Association retrieve start",
        issues,
      });
    }
    if (action.kind === "changeMembers") {
      checkVariableReference({
        schema,
        metadata,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.changeVariableName",
        value: action.changeVariableName,
        expectedKinds: ["object"],
        label: "Change members",
        issues,
      });
    }
    if (action.kind === "commit" || action.kind === "delete" || action.kind === "rollback") {
      checkVariableReference({
        schema,
        metadata,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.objectOrListVariableName",
        value: action.objectOrListVariableName,
        expectedKinds: ["object", "list"],
        label: action.kind,
        issues,
      });
    }
    if (action.kind === "changeVariable") {
      checkVariableReference({
        schema,
        metadata,
        objectId: object.id,
        actionId: action.id,
        fieldPath: "action.targetVariableName",
        value: action.targetVariableName,
        expectedKinds: ["boolean", "integer", "long", "decimal", "string", "dateTime", "enumeration", "object", "list", "fileDocument", "json", "unknown"],
        label: "Change variable",
        issues,
      });
    }
  }
  return issues;
}
