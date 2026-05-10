import { useState } from "react";

import { Input } from "@douyinfe/semi-ui";
import {
  type LineRenderProps,
  usePlaygroundReadonlyState,
} from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";
import { emitInlineLineLabelCommit } from "./inline-events";

function edgeDataFromLine(line: LineRenderProps["line"]): FlowGramMicroflowEdgeData | undefined {
  const maybeLine = line as unknown as {
    data?: FlowGramMicroflowEdgeData;
    info?: { data?: FlowGramMicroflowEdgeData };
    toJSON?: () => { data?: FlowGramMicroflowEdgeData };
  };
  const data = maybeLine.data ?? maybeLine.info?.data ?? maybeLine.toJSON?.().data;
  return typeof data?.flowId === "string" ? data : undefined;
}

export function lineLabelFromEdgeData(data: FlowGramMicroflowEdgeData): string {
  if (data.label) {
    return data.label;
  }
  if (typeof data.sourcePortId === "string" && data.sourcePortId.includes(":")) {
    const inferred = data.sourcePortId.split(":").at(-1) ?? "";
    if (["approved", "rejected", "timeout", "body", "done", "break", "continue", "error", "fallback", "rethrow", "handled"].includes(inferred)) {
      return inferred;
    }
  }
  if (data.edgeKind === "errorHandler") {
    return "error";
  }
  const firstCase = data.caseValues[0];
  if (!firstCase) {
    return "else";
  }
  if (firstCase.kind === "boolean") {
    return String(firstCase.value);
  }
  if (firstCase.kind === "fallback") {
    return "else";
  }
  if (firstCase.kind === "enumeration") {
    return firstCase.value;
  }
  return "else";
}

function runtimeClass(state: FlowGramMicroflowEdgeData["runtimeState"]): string {
  if (state === "selectedCase" || state === "visited") {
    return "is-runtime-active";
  }
  if (state === "skipped") {
    return "is-runtime-skipped";
  }
  if (state === "failed" || state === "errorHandlerVisited") {
    return "is-runtime-error";
  }
  return "";
}

export function lineClassNameFromEdgeData(data: FlowGramMicroflowEdgeData): string {
  return [
    "microflow-flowgram-line",
    `microflow-flowgram-line--${data.edgeKind}`,
    `is-validation-${data.validationState}`,
    data.runtimeState ? `is-runtime-${data.runtimeState}` : "",
  ].filter(Boolean).join(" ");
}

export function FlowGramMicroflowLineRenderer({ line }: LineRenderProps) {
  const readonly = usePlaygroundReadonlyState();
  const data = edgeDataFromLine(line);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");

  if (!data) {
    return null;
  }
  const label = lineLabelFromEdgeData(data);
  const warningMissingTarget = !data.targetNodeId && (
    data.edgeKind === "decisionCondition"
    || data.edgeKind === "objectTypeCondition"
    || data.edgeKind === "loopBody"
    || data.edgeKind === "sequence"
    || data.edgeKind === "errorHandler"
  );
  const className = [
    "microflow-branch-label",
    label === "true" ? "is-true" : "",
    label === "false" ? "is-false" : "",
    label === "else" ? "is-else" : "",
    editing ? "is-editing" : "",
    runtimeClass(data.runtimeState),
    data.validationState === "warning" || warningMissingTarget ? "is-warning" : "",
  ].filter(Boolean).join(" ");

  const commit = (value: string) => {
    const trimmed = value.trim();
    emitInlineLineLabelCommit({
      flowId: data.flowId,
      value: trimmed || label,
    });
    setEditing(false);
  };

  if (editing && !readonly) {
    return (
      <Input
        className="microflow-branch-label is-editing"
        autoFocus
        value={draft}
        onChange={setDraft}
        onKeyDown={event => {
          if (event.key === "Enter") {
            commit(draft);
          }
          if (event.key === "Escape") {
            setEditing(false);
            setDraft(label);
          }
        }}
        onBlur={() => commit(draft)}
      />
    );
  }

  return (
    <button
      type="button"
      className={className}
      data-testid="microflow-flowgram-line-label"
      data-flow-id={data.flowId}
      data-edge-kind={data.edgeKind}
      onMouseDown={event => {
        event.stopPropagation();
      }}
      onClick={event => {
        event.stopPropagation();
        if (readonly) {
          return;
        }
        setDraft(label);
        setEditing(true);
      }}
      title={warningMissingTarget ? "缺少目标节点" : label}
    >
      {label}
      {!readonly ? <span className="microflow-branch-label__edit" aria-hidden="true">✎</span> : null}
      {warningMissingTarget ? <span aria-hidden="true" className="microflow-branch-label__warning-dot" /> : null}
    </button>
  );
}
