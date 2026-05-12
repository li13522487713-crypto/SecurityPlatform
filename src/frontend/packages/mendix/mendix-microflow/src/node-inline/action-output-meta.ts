import type { MicroflowAction } from "../schema/types";

export interface InlineActionOutputMeta {
  name: string;
  fieldPath: string;
}

function stringValue(record: Record<string, unknown>, key: string): string {
  return typeof record[key] === "string" ? String(record[key] ?? "") : "";
}

export function resolveInlineActionOutputMeta(action: MicroflowAction | Record<string, unknown> | undefined): InlineActionOutputMeta | undefined {
  if (!action) {
    return undefined;
  }
  const record = action as Record<string, unknown>;
  const kind = typeof record.kind === "string" ? record.kind : "";

  if (kind === "callMicroflow" || kind === "callJavaAction" || kind === "callJavaScriptAction" || kind === "callNanoflow") {
    const returnValue = record.returnValue && typeof record.returnValue === "object" ? record.returnValue as Record<string, unknown> : {};
    const name = stringValue(returnValue, "outputVariableName")
      || stringValue(returnValue, "resultVariableName")
      || stringValue(record, "outputVariableName");
    return { name, fieldPath: "data.action.returnValue.outputVariableName" };
  }

  if (kind === "callWorkflow") {
    return {
      name: stringValue(record, "outputWorkflowVariableName"),
      fieldPath: "data.action.outputWorkflowVariableName",
    };
  }

  if (kind === "generateDocument") {
    return {
      name: stringValue(record, "outputFileDocumentVariableName"),
      fieldPath: "data.action.outputFileDocumentVariableName",
    };
  }

  if (kind === "retrieveWorkflows" || kind === "createList") {
    return {
      name: stringValue(record, "outputListVariableName") || stringValue(record, "listVariableName"),
      fieldPath: "data.action.outputListVariableName",
    };
  }

  if (kind === "cast") {
    return {
      name: stringValue(record, "outputVariableName") || stringValue(record, "outputVariable"),
      fieldPath: "data.action.outputVariableName",
    };
  }

  if (kind === "callExternalAction") {
    return {
      name: stringValue(record, "returnVariableName"),
      fieldPath: "data.action.returnVariableName",
    };
  }

  return {
    name: stringValue(record, "outputVariableName")
      || stringValue(record, "resultVariableName")
      || stringValue(record, "outputListVariableName")
      || stringValue(record, "outputWorkflowVariableName")
      || stringValue(record, "outputFileDocumentVariableName")
      || stringValue(record, "returnVariableName"),
    fieldPath: "data.action.outputVariableName",
  };
}
