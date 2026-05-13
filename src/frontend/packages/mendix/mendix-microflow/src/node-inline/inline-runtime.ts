import type { MicroflowTraceFrame } from "../debug/trace-types";
import { buildRuntimeValueGroups } from "../debug/runtime-value-view-model";
import type { MicroflowNodeRuntimeInlineState } from "../flowgram/FlowGramMicroflowTypes";
import type { MicroflowValidationIssue } from "../schema/types";
import type { RuntimeNodeOverlay, RuntimeValueSummary } from "../runtime/runtime-overlay";
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

function selectedBranchLabelFromOverlay(overlay: RuntimeNodeOverlay | undefined): string | undefined {
  const selected = overlay?.selectedCaseValue;
  if (!selected) {
    return overlay?.selectedCaseLabel;
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
    return selected.condition?.trim() || selected.expression?.trim() || overlay?.selectedCaseLabel || "expression";
  }
  return overlay?.selectedCaseLabel;
}

function summaryPreview(items?: RuntimeValueSummary[]): string | undefined {
  if (!items?.length) {
    return undefined;
  }
  const compact = items
    .slice(0, 2)
    .map(item => item.preview?.trim() ? `${item.name}=${item.preview}` : item.name)
    .filter(Boolean);
  if (compact.length === 0) {
    return undefined;
  }
  const suffix = items.length > 2 ? ", ..." : "";
  return compact.join(", ") + suffix;
}

function deltaPreviewFromOverlay(overlay: RuntimeNodeOverlay | undefined): string | undefined {
  const delta = overlay?.variableDeltaSummary;
  if (!delta?.length) {
    return undefined;
  }
  const parts = delta
    .slice(0, 4)
    .map(item => {
      if (item.kind === "added") return `+${item.name}`;
      if (item.kind === "changed") return `~${item.name}`;
      return `-${item.name}`;
    });
  if (parts.length === 0) {
    return undefined;
  }
  return parts.join(", ") + (delta.length > parts.length ? ", ..." : "");
}

function loopIterationLabelFromOverlay(overlay: RuntimeNodeOverlay | undefined): string | undefined {
  const iteration = overlay?.loopIteration;
  if (!iteration) {
    return undefined;
  }
  const index = typeof iteration.index === "number" ? iteration.index : undefined;
  const total = typeof iteration.total === "number" ? iteration.total : undefined;
  const iterator = iteration.iteratorName?.trim();
  const iteratorValue = iteration.iteratorValuePreview?.trim();
  const control = iteration.control;
  const parts: string[] = [];
  if (index !== undefined || total !== undefined) {
    parts.push(`Iteration ${index ?? "-"} / ${total ?? "-"}`);
  }
  if (iterator || iteratorValue) {
    parts.push(`${iterator ?? "item"}=${iteratorValue ?? "?"}`);
  }
  if (control) {
    parts.push(control === "break" ? "break" : "continue");
  }
  if (parts.length === 0) {
    return undefined;
  }
  return parts.join(" · ");
}

function gatewayProgressLabelFromOverlay(overlay: RuntimeNodeOverlay | undefined): string | undefined {
  const gateway = overlay?.gatewaySummary;
  if (!gateway) {
    return undefined;
  }
  const total = typeof gateway.totalBranches === "number" ? gateway.totalBranches : undefined;
  const completed = typeof gateway.completedBranches === "number" ? gateway.completedBranches : undefined;
  const skipped = typeof gateway.skippedBranches === "number" ? gateway.skippedBranches : undefined;
  const failed = typeof gateway.failedBranches === "number" ? gateway.failedBranches : undefined;
  const parts: string[] = [];
  if (total !== undefined || completed !== undefined) {
    parts.push(`Branches ${completed ?? 0} / ${total ?? "?"}`);
  }
  if ((skipped ?? 0) > 0) {
    parts.push(`Skipped ${skipped}`);
  }
  if ((failed ?? 0) > 0) {
    parts.push(`Failed ${failed}`);
  }
  if (parts.length === 0) {
    return undefined;
  }
  return parts.join(" · ");
}

function gatewayMergeLabelFromOverlay(overlay: RuntimeNodeOverlay | undefined): string | undefined {
  const preview = overlay?.gatewaySummary?.mergeResultPreview?.trim();
  if (!preview) {
    return undefined;
  }
  return `Merge ${preview}`;
}

function deltaPreviewFromFrame(frame: MicroflowTraceFrame | undefined): string | undefined {
  if (!frame?.variableDelta) {
    return undefined;
  }
  const added = (frame.variableDelta.added ?? []).slice(0, 2).map(name => `+${name}`);
  const changed = (frame.variableDelta.changed ?? []).slice(0, 2).map(name => `~${name}`);
  const removed = (frame.variableDelta.removed ?? []).slice(0, 2).map(name => `-${name}`);
  const parts = [...added, ...changed, ...removed];
  if (parts.length === 0) {
    return undefined;
  }
  const totalCount = (frame.variableDelta.added?.length ?? 0) + (frame.variableDelta.changed?.length ?? 0) + (frame.variableDelta.removed?.length ?? 0);
  return parts.join(", ") + (totalCount > parts.length ? ", ..." : "");
}

function loopIterationLabelFromFrame(frame: MicroflowTraceFrame | undefined): string | undefined {
  const iteration = frame?.loopIteration;
  if (!iteration) {
    return undefined;
  }
  const iteratorName = iteration.iteratorVariableName?.trim();
  const iteratorPreview = iteration.iteratorValuePreview?.trim();
  const parts = [`Iteration ${iteration.index}`];
  if (iteratorName || iteratorPreview) {
    parts.push(`${iteratorName ?? "item"}=${iteratorPreview ?? "?"}`);
  }
  return parts.join(" · ");
}

function gatewayProgressLabelFromFrame(frame: MicroflowTraceFrame | undefined): string | undefined {
  const trace = frame?.output?.branchTrace;
  if (!Array.isArray(trace) || trace.length === 0) {
    return undefined;
  }
  const total = trace.length;
  const completed = trace.filter(item => item.status === "completed" || item.status === "executed").length;
  const skipped = trace.filter(item => item.status === "skipped").length;
  const failed = trace.filter(item => item.status === "failed").length;
  const parts = [`Branches ${completed} / ${total}`];
  if (skipped > 0) {
    parts.push(`Skipped ${skipped}`);
  }
  if (failed > 0) {
    parts.push(`Failed ${failed}`);
  }
  return parts.join(" · ");
}

function compactUnknown(value: unknown): string | undefined {
  if (value === null || value === undefined) {
    return undefined;
  }
  if (typeof value === "string") {
    return value.trim() || undefined;
  }
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  try {
    const raw = JSON.stringify(value);
    if (!raw) {
      return undefined;
    }
    return raw.length > 80 ? `${raw.slice(0, 77)}...` : raw;
  } catch {
    return undefined;
  }
}

function decisionEvaluationFromFrame(frame: MicroflowTraceFrame | undefined): { expression?: string; evaluatedValue?: string } {
  if (!Array.isArray(frame?.evaluatedExpressions)) {
    return {};
  }
  let expression: string | undefined;
  let evaluatedValue: string | undefined;
  for (const item of frame.evaluatedExpressions) {
    if (!item || typeof item !== "object") {
      continue;
    }
    const row = item as Record<string, unknown>;
    expression = expression
      ?? compactUnknown(row.conditionExpression)
      ?? compactUnknown(row.expression)
      ?? compactUnknown(row.condition);
    evaluatedValue = evaluatedValue
      ?? compactUnknown(row.evaluatedValue)
      ?? compactUnknown(row.result)
      ?? compactUnknown(row.value);
    if (expression && evaluatedValue) {
      break;
    }
  }
  return { expression, evaluatedValue };
}

function runtimeFromOverlay(overlay: RuntimeNodeOverlay | undefined): MicroflowNodeRuntimeInlineState | undefined {
  if (!overlay) {
    return undefined;
  }
  const failed = overlay.status === "failed";
  const running = overlay.status === "running" || overlay.status === "queued" || overlay.status === "paused";
  const skipped = overlay.status === "skipped";
  const success = overlay.status === "succeeded";
  const visited = failed || running || skipped || success;
  return {
    visited,
    running,
    success,
    failed,
    skipped,
    durationMs: overlay.durationMs,
    inputCount: overlay.inputSummary?.length,
    outputCount: overlay.outputSummary?.length,
    selectedBranchLabel: selectedBranchLabelFromOverlay(overlay),
    decisionExpression: overlay.conditionExpression,
    decisionEvaluatedValue: overlay.evaluatedValuePreview,
    inputPreview: summaryPreview(overlay.inputSummary),
    outputPreview: summaryPreview(overlay.outputSummary),
    deltaPreview: deltaPreviewFromOverlay(overlay),
    loopIterationLabel: loopIterationLabelFromOverlay(overlay),
    gatewayProgressLabel: gatewayProgressLabelFromOverlay(overlay),
    gatewayMergeLabel: gatewayMergeLabelFromOverlay(overlay),
    outputSummaries: overlay.outputSummary?.map(item => item.preview?.trim() ? `${item.name}=${item.preview}` : item.name),
    rawTraceJson: JSON.stringify(overlay, null, 2),
    variableSnapshot: overlay.outputSummary?.map(item => ({
      name: item.name,
      type: item.type,
      valuePreview: item.preview ?? "",
    })),
    error: overlay.error?.message
      ? {
          code: overlay.error.code,
          message: overlay.error.message,
          fixSuggestions: [{
            id: "open-error",
            label: "展开错误详情",
            actionKind: "expandError",
          }, {
            id: "inspect-runtime",
            label: "查看运行态详情",
            actionKind: "inspectRuntime",
          }],
        }
      : undefined,
  };
}

export function deriveRuntimeInlineState(
  frame?: MicroflowTraceFrame,
  overlay?: RuntimeNodeOverlay,
  issues?: MicroflowValidationIssue[],
  objectId?: string,
): MicroflowNodeRuntimeInlineState | undefined {
  const issueScoped = (issues ?? []).filter(item => !objectId || item.objectId === objectId);
  const issueFixSuggestions = issueScoped.flatMap(mapIssueQuickFix);
  const overlayRuntime = runtimeFromOverlay(overlay);
  if (!frame) {
    if (overlayRuntime) {
      return overlayRuntime;
    }
    if (issueScoped.length > 0) {
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
    return undefined;
  }
  const errorMessage = frame.error?.message ?? frame.message;
  const valueGroups = buildRuntimeValueGroups(frame);
  const decisionEvaluation = decisionEvaluationFromFrame(frame);
  const frameRuntime: MicroflowNodeRuntimeInlineState = {
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
    decisionExpression: decisionEvaluation.expression,
    decisionEvaluatedValue: decisionEvaluation.evaluatedValue,
    inputPreview: runtimeInputPreview(frame),
    outputPreview: runtimeOutputPreview(frame),
    deltaPreview: deltaPreviewFromFrame(frame),
    loopIterationLabel: loopIterationLabelFromFrame(frame),
    gatewayProgressLabel: gatewayProgressLabelFromFrame(frame),
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
  if (!overlayRuntime) {
    return frameRuntime;
  }
  return {
    ...frameRuntime,
    visited: overlayRuntime.visited ?? frameRuntime.visited,
    running: overlayRuntime.running ?? frameRuntime.running,
    success: overlayRuntime.success ?? frameRuntime.success,
    failed: overlayRuntime.failed ?? frameRuntime.failed,
    skipped: overlayRuntime.skipped ?? frameRuntime.skipped,
    durationMs: overlayRuntime.durationMs ?? frameRuntime.durationMs,
    inputCount: overlayRuntime.inputCount ?? frameRuntime.inputCount,
    outputCount: overlayRuntime.outputCount ?? frameRuntime.outputCount,
    selectedBranchLabel: overlayRuntime.selectedBranchLabel ?? frameRuntime.selectedBranchLabel,
    decisionExpression: overlayRuntime.decisionExpression ?? frameRuntime.decisionExpression,
    decisionEvaluatedValue: overlayRuntime.decisionEvaluatedValue ?? frameRuntime.decisionEvaluatedValue,
    inputPreview: overlayRuntime.inputPreview ?? frameRuntime.inputPreview,
    outputPreview: overlayRuntime.outputPreview ?? frameRuntime.outputPreview,
    deltaPreview: overlayRuntime.deltaPreview ?? frameRuntime.deltaPreview,
    loopIterationLabel: overlayRuntime.loopIterationLabel ?? frameRuntime.loopIterationLabel,
    gatewayProgressLabel: overlayRuntime.gatewayProgressLabel ?? frameRuntime.gatewayProgressLabel,
    gatewayMergeLabel: overlayRuntime.gatewayMergeLabel ?? frameRuntime.gatewayMergeLabel,
    outputSummaries: overlayRuntime.outputSummaries?.length ? overlayRuntime.outputSummaries : frameRuntime.outputSummaries,
    variableSnapshot: overlayRuntime.variableSnapshot?.length ? overlayRuntime.variableSnapshot : frameRuntime.variableSnapshot,
    rawTraceJson: overlayRuntime.rawTraceJson ?? frameRuntime.rawTraceJson,
    error: overlayRuntime.error ?? frameRuntime.error,
  };
}
