import type { MicroflowTraceFrame } from "../debug/trace-types";
import { buildRuntimeValueGroups } from "../debug/runtime-value-view-model";
import type { MicroflowNodeRuntimeInlineState } from "../flowgram/FlowGramMicroflowTypes";
import type { MicroflowValidationIssue } from "../schema/types";
import { runtimeInputPreview, runtimeOutputPreview } from "./inline-formatters";

function mapIssueQuickFix(issue: MicroflowValidationIssue) {
  return (issue.quickFixes ?? [])
    .filter(item => item?.kind === "createMissingFlow" || item?.kind === "setField")
    .map(item => {
      const payload = (item.payload ?? {}) as { caseKind?: string; value?: unknown; fieldPath?: string };
      const isBooleanBranch = item.kind === "createMissingFlow" && payload.caseKind === "boolean" && typeof payload.value === "boolean";
      return {
        id: `${issue.id}:${item.kind}`,
        label: isBooleanBranch ? `补全${payload.value ? "true" : "false"}分支` : "应用修复",
        actionKind: item.kind === "createMissingFlow" ? "createMissingFlow" : "setFieldValue",
        fieldPath: payload.fieldPath,
        value: isBooleanBranch ? payload.value : payload.value,
        editType: isBooleanBranch ? "branch" as const : "text" as const,
      };
    });
}

function shortQualifiedName(value: string | undefined): string {
  const normalized = String(value ?? "").trim();
  if (!normalized) {
    return "";
  }
  const parts = normalized.split(".");
  return parts[parts.length - 1] ?? normalized;
}

function selectedBranchLabel(frame: MicroflowTraceFrame | undefined): string | undefined {
  const selected = frame?.selectedCaseValue;
  if (!selected) {
    return undefined;
  }
  if (selected.kind === "boolean") {
    return String(selected.value);
  }
  if (selected.kind === "enumeration") {
    return selected.value;
  }
  if (selected.kind === "inheritance") {
    return shortQualifiedName(selected.entityQualifiedName);
  }
  if (selected.kind === "empty" || selected.kind === "fallback" || selected.kind === "noCase") {
    return "(empty)";
  }
  if (selected.kind === "expression") {
    return selected.condition?.trim() || selected.expression?.trim() || "expression";
  }
  return undefined;
}

export function deriveRuntimeInlineState(
  frame?: MicroflowTraceFrame,
  issues?: MicroflowValidationIssue[],
  objectId?: string,
): MicroflowNodeRuntimeInlineState | undefined {
  const issueScoped = (issues ?? []).filter(item => !objectId || item.objectId === objectId);
  const issueFixSuggestions = issueScoped.flatMap(mapIssueQuickFix);

  if (!frame && issueScoped.length === 0) {
    return undefined;
  }
  if (!frame && issueScoped.length > 0) {
    const primary = issueScoped[0];
    return {
      failed: primary.severity === "error",
      error: {
        code: primary.code,
        message: primary.message,
        fixSuggestions: issueFixSuggestions,
      },
    };
  }

  if (!frame) {
    return undefined;
  }
  const errorMessage = frame.error?.message ?? frame.message;
  const valueGroups = buildRuntimeValueGroups(frame);
  return {
    visited: frame.status === "success" || frame.status === "failed" || frame.status === "skipped",
    running: frame.status === "running",
    success: frame.status === "success",
    failed: frame.status === "failed",
    skipped: frame.status === "skipped",
    durationMs: frame.durationMs,
    executionIndex: Number.isFinite(Number(frame.frameId)) ? Number(frame.frameId) : undefined,
    inputCount: valueGroups.inputs.values.length,
    outputCount: valueGroups.outputs.values.length,
    selectedBranchLabel: selectedBranchLabel(frame),
    inputPreview: runtimeInputPreview(frame),
    outputPreview: runtimeOutputPreview(frame),
    outputSummaries: valueGroups.outputSummaries,
    inputGroup: valueGroups.inputs,
    outputGroup: valueGroups.outputs,
    variableGroup: valueGroups.variables,
    rawTraceJson: JSON.stringify(frame, null, 2),
    variableSnapshot: Object.values(frame.variablesSnapshot ?? {}).map(item => ({
      name: item.name,
      type: item.type.kind,
      valuePreview: item.valuePreview,
    })),
    error: errorMessage
      ? {
          code: frame.error?.code,
          message: errorMessage,
          stackPreview: frame.error?.callStack?.slice(0, 3).join("\n"),
          fixSuggestions: [{
            id: "open-error",
            label: "展开错误详情",
            actionKind: "expandError",
          }, ...issueFixSuggestions],
        }
      : undefined,
  };
}
