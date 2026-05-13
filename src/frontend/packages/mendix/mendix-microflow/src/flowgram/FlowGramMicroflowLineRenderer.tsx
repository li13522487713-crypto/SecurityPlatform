import { useContext, useEffect, useRef, useState } from "react";

import { Input } from "@douyinfe/semi-ui";
import {
  type LineRenderProps,
  usePlaygroundReadonlyState,
} from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";
import { MicroflowEdgeDataContext, MicroflowSelectedFlowIdContext } from "./FlowGramMicroflowTypes";
import { emitInlineLineDelete, emitInlineLineLabelCommit } from "./inline-events";
import { MicroflowEdge } from "../components/MicroflowEdge";

function lineInfoKey(input: {
  from?: unknown;
  to?: unknown;
  fromPort?: unknown;
  toPort?: unknown;
}): string {
  return [
    String(input.from ?? ""),
    String(input.fromPort ?? ""),
    String(input.to ?? ""),
    String(input.toPort ?? ""),
  ].join("::");
}

function edgeDataFromLine(
  line: LineRenderProps["line"],
  edgeDataByLineKey: ReadonlyMap<string, FlowGramMicroflowEdgeData>,
): FlowGramMicroflowEdgeData | undefined {
  const maybeLine = line as unknown as {
    data?: FlowGramMicroflowEdgeData;
    info?: { data?: FlowGramMicroflowEdgeData; from?: unknown; to?: unknown; fromPort?: unknown; toPort?: unknown };
    toJSON?: () => {
      data?: FlowGramMicroflowEdgeData;
      sourceNodeID?: unknown;
      targetNodeID?: unknown;
      sourcePortID?: unknown;
      targetPortID?: unknown;
    };
  };
  const json = maybeLine.toJSON?.();
  const data = maybeLine.data ?? maybeLine.info?.data ?? json?.data;
  if (typeof data?.flowId === "string") {
    return data;
  }
  const infoKey = lineInfoKey(maybeLine.info ?? {});
  const jsonKey = lineInfoKey({
    from: json?.sourceNodeID,
    fromPort: json?.sourcePortID,
    to: json?.targetNodeID,
    toPort: json?.targetPortID,
  });
  return edgeDataByLineKey.get(infoKey) ?? edgeDataByLineKey.get(jsonKey);
}

export function lineLabelFromEdgeData(data: FlowGramMicroflowEdgeData): string {
  if (data.edgeKind === "errorHandler") {
    return "Error";
  }
  if (typeof data.sourcePortId === "string" && data.sourcePortId.includes(":")) {
    const inferred = data.sourcePortId.split(":").at(-1) ?? "";
    if (["approved", "rejected", "timeout", "body", "done", "break", "continue", "error", "fallback", "rethrow", "handled"].includes(inferred)) {
      return inferred;
    }
  }
  if (data.edgeKind === "loopBody") {
    return "body";
  }
  const firstCase = data.caseValues[0];
  if (!firstCase) {
    if (data.edgeKind === "objectTypeCondition") {
      return "(empty)";
    }
    if (data.edgeKind === "decisionCondition") {
      return data.label || (data.targetNodeId ? "(empty)" : "else");
    }
    return data.label || "";
  }
  if (firstCase.kind === "boolean") {
    return firstCase.value ? "True" : "False";
  }
  if (firstCase.kind === "fallback") {
    if (data.edgeKind === "objectTypeCondition") {
      return "(empty)";
    }
    return "else";
  }
  if (firstCase.kind === "empty" || firstCase.kind === "noCase") {
    return "(empty)";
  }
  if (firstCase.kind === "enumeration") {
    return firstCase.value;
  }
  if (firstCase.kind === "expression") {
    return firstCase.condition ?? firstCase.expression ?? "case";
  }
  if (firstCase.kind === "inheritance") {
    return firstCase.entityQualifiedName;
  }
  return data.label || "else";
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

function errorHandlingClass(data: FlowGramMicroflowEdgeData): string {
  if (data.edgeKind !== "errorHandler") {
    return "";
  }
  if (data.sourceErrorHandlingType === "customWithoutRollback") {
    return "microflow-flowgram-line--error-handler-customWithoutRollback";
  }
  if (data.sourceErrorHandlingType === "continue") {
    return "microflow-flowgram-line--error-handler-continue";
  }
  if (data.sourceErrorHandlingType === "customWithRollback") {
    return "microflow-flowgram-line--error-handler-customWithRollback";
  }
  return "";
}

export function lineClassNameFromEdgeData(data: FlowGramMicroflowEdgeData): string {
  return [
    "microflow-flowgram-line",
    `microflow-flowgram-line--${data.edgeKind}`,
    errorHandlingClass(data),
    `is-validation-${data.validationState}`,
    data.runtimeState ? `is-runtime-${data.runtimeState}` : "",
  ].filter(Boolean).join(" ");
}

export function FlowGramMicroflowLineRenderer({ line }: LineRenderProps) {
  const readonly = usePlaygroundReadonlyState();
  const edgeDataByLineKey = useContext(MicroflowEdgeDataContext);
  const selectedFlowId = useContext(MicroflowSelectedFlowIdContext);
  const data = edgeDataFromLine(line, edgeDataByLineKey);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");
  const hostRef = useRef<HTMLInputElement | HTMLDivElement | null>(null);

  if (!data) {
    return null;
  }
  useEffect(() => {
    const host = hostRef.current;
    const edgeElement = host?.closest(".gedit-flow-activity-edge");
    if (!edgeElement) {
      return;
    }
    const previousFlowId = edgeElement.getAttribute("data-flow-id");
    edgeElement.setAttribute("data-flow-id", data.flowId);
    const classNames = lineClassNameFromEdgeData(data).split(/\s+/).filter(Boolean);
    for (const className of classNames) {
      edgeElement.classList.add(className);
    }
    return () => {
      for (const className of classNames) {
        edgeElement.classList.remove(className);
      }
      if (edgeElement.getAttribute("data-flow-id") === data.flowId) {
        if (previousFlowId) {
          edgeElement.setAttribute("data-flow-id", previousFlowId);
        } else {
          edgeElement.removeAttribute("data-flow-id");
        }
      }
    };
  }, [data]);
  const label = lineLabelFromEdgeData(data);
  const warningMissingTarget = !data.targetNodeId && (
    data.edgeKind === "decisionCondition"
    || data.edgeKind === "objectTypeCondition"
    || data.edgeKind === "loopBody"
    || data.edgeKind === "errorHandler"
  );
  const branchLabel = label.toLowerCase();
  if (!branchLabel && !warningMissingTarget) {
    return <div ref={hostRef} data-flow-id={data.flowId} style={{ display: "none" }} aria-hidden="true" />;
  }
  const className = [
    "microflow-branch-label",
    branchLabel === "true" ? "is-true" : "",
    branchLabel === "false" ? "is-false" : "",
    branchLabel === "else" ? "is-else" : "",
    branchLabel === "empty" || branchLabel === "(empty)" ? "is-empty" : "",
    editing ? "is-editing" : "",
    runtimeClass(data.runtimeState),
    data.validationState === "warning" || warningMissingTarget ? "is-warning" : "",
    data.edgeKind === "errorHandler" && data.sourceErrorHandlingType === "customWithoutRollback" ? "is-error-handler-customWithoutRollback" : "",
    data.edgeKind === "errorHandler" && data.sourceErrorHandlingType === "customWithRollback" ? "is-error-handler-customWithRollback" : "",
    data.edgeKind === "errorHandler" && data.sourceErrorHandlingType === "continue" ? "is-error-handler-continue" : "",
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
        ref={hostRef}
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

  const isSelected = Boolean(selectedFlowId && selectedFlowId === data.flowId);
  return (
    <div ref={hostRef} data-flow-id={data.flowId}>
      <MicroflowEdge
        className={className}
        flowId={data.flowId}
        edgeKind={data.edgeKind}
        label={label}
        selected={isSelected}
        warningMissingTarget={warningMissingTarget}
        readonly={readonly}
        onMouseDown={event => {
          event.stopPropagation();
        }}
        onClick={event => {
          event.stopPropagation();
        }}
        onEdit={() => {
          if (readonly) {
            return;
          }
          setDraft(label);
          setEditing(true);
        }}
        onDelete={() => {
          emitInlineLineDelete({
            flowId: data.flowId,
          });
        }}
        editAdornment={<span className="microflow-branch-label__edit" aria-hidden="true">✎</span>}
      />
    </div>
  );
}
