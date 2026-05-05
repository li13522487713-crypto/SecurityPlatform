import { useState, type MouseEvent, type ReactNode } from "react";

import { Tag, Typography } from "@douyinfe/semi-ui";
import { InlineNodeEditor } from "../inline-edit";
import {
  FlowNodeFormData,
  type FormModelV2,
  type WorkflowNodeRenderProps,
  usePlaygroundReadonlyState,
  useNodeRender,
} from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowNodeData } from "./FlowGramMicroflowTypes";
import { FlowGramMicroflowPortRenderer } from "./FlowGramMicroflowPortRenderer";
import {
  emitInlineFieldCommit,
  emitInlineNodeInspect,
  emitInlineNodeToggle,
  emitInlineQuickFix,
} from "./inline-events";
import {
  focusMicroflowNodeDragRoot,
  isMicroflowNodeDragBlockedTarget,
} from "./flowgram-node-drag";
import "./styles/flowgram-microflow-node.css";

function tryReadNodeData(props: WorkflowNodeRenderProps): FlowGramMicroflowNodeData | undefined {
  try {
    const formData = props.node.getData(FlowNodeFormData);
    const formModel = formData?.getFormModel<FormModelV2>();
    const formValue = formModel?.getFormItemValueByPath("/") as FlowGramMicroflowNodeData | undefined;
    if (formValue?.objectKind) {
      return formValue;
    }
    const jsonData = (props.node as unknown as { toJSON?: () => { data?: FlowGramMicroflowNodeData } })
      .toJSON?.()
      ?.data;
    if (jsonData?.objectKind) {
      return jsonData;
    }
    const nodeMeta = props.node.getNodeMeta?.() as { nodeDTOType?: string; type?: string } | undefined;
    const fallbackKind = nodeMeta?.nodeDTOType ?? nodeMeta?.type;
    if (fallbackKind) {
      return {
        objectId: String(props.node.id),
        objectKind: fallbackKind as FlowGramMicroflowNodeData["objectKind"],
        collectionId: "",
        title: fallbackKind,
        subtitle: undefined,
        officialType: fallbackKind,
        disabled: false,
        validationState: "valid",
        runtimeState: "idle",
        issueCount: 0,
      };
    }
  } catch {
    return undefined;
  }
  return undefined;
}

function nodeTone(kind: FlowGramMicroflowNodeData["objectKind"]): string {
  if (kind === "startEvent") {
    return "start";
  }
  if (kind === "endEvent" || kind === "errorEvent") {
    return "end";
  }
  if (kind === "exclusiveSplit" || kind === "inheritanceSplit") {
    return "decision";
  }
  if (kind === "annotation") {
    return "annotation";
  }
  if (kind === "loopedActivity") {
    return "loop";
  }
  return "action";
}

function StaticTag(props: { children: ReactNode; color?: "blue" | "orange" | "grey" }) {
  return (
    <span draggable={false} data-flow-editor-selectable="false">
      <Tag size="small" color={props.color}>{props.children}</Tag>
    </span>
  );
}

export function FlowGramMicroflowNodeRenderer(props: WorkflowNodeRenderProps) {
  const { selected, activated, ports, selectNode, nodeRef, startDrag, onFocus, onBlur } = useNodeRender();
  const readonly = usePlaygroundReadonlyState();
  const [focused, setFocused] = useState(false);
  const data = tryReadNodeData(props);

  const canStartNodeDrag = (event: MouseEvent<HTMLDivElement>) => {
    if (readonly || event.button !== 0) {
      return false;
    }
    if (event.detail > 1) {
      return false;
    }
    return !isMicroflowNodeDragBlockedTarget(event.target);
  };

  const handleMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    if (!canStartNodeDrag(event)) {
      return;
    }
    focusMicroflowNodeDragRoot(event.currentTarget);
    startDrag(event);
  };

  const handleClick = (event: MouseEvent<HTMLDivElement>) => {
    selectNode(event);
  };

  const handleDoubleClick = (event: MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();
    const expanded = data?.inlineConfig?.viewMode !== "expanded";
    emitInlineNodeToggle({ nodeId: data?.objectId ?? String(props.node.id), runtimeNodeId: String(props.node.id), expanded });
  };

  if (!data?.objectKind) {
    return (
      <div
        ref={nodeRef}
        className={[
          "microflow-flowgram-node",
          "microflow-flowgram-node--fallback",
          selected ? "is-selected" : "",
          activated ? "is-active" : "",
          focused ? "is-focused" : "",
        ].filter(Boolean).join(" ")}
        draggable={false}
        onMouseDown={handleMouseDown}
        onClick={handleClick}
        onFocus={() => {
          setFocused(true);
          onFocus();
        }}
        onBlur={() => {
          setFocused(false);
          onBlur();
        }}
        data-testid={`microflow-node-${String(props.node.id)}`}
        data-node-selected={String(selected)}
        data-node-active={String(activated)}
        tabIndex={0}
      >
        Unknown node
      </div>
    );
  }

  const tone = nodeTone(data.objectKind);
  const isExpanded = data.inlineConfig?.viewMode === "expanded";
  const summaryLines = data.inlineConfig?.summaryLines ?? [];
  const compactSummary = summaryLines.slice(0, 3);
  const summaryOverflowCount = Math.max(0, summaryLines.length - compactSummary.length);
  const runtime = data.inlineConfig?.runtime;
  const runtimeStateLabel = runtime?.error
    ? "× error"
    : runtime?.running
      ? "running"
      : runtime?.failed
        ? "× failed"
        : runtime?.skipped
          ? "skipped"
          : runtime?.success
            ? "✓"
            : "";

  const toggleExpanded = () => {
    emitInlineNodeToggle({ nodeId: data.objectId, runtimeNodeId: String(props.node.id), expanded: !isExpanded });
  };

  return (
    <div
      ref={nodeRef}
      className={[
        "microflow-flowgram-node",
        `microflow-flowgram-node--${tone}`,
        selected ? "is-selected" : "",
        activated ? "is-active" : "",
        focused ? "is-focused" : "",
        data.disabled ? "is-disabled" : "",
        data.validationState !== "valid" ? `is-${data.validationState}` : "",
        data.runtimeState && data.runtimeState !== "idle" ? `is-runtime-${data.runtimeState}` : "",
        isExpanded ? "is-expanded" : "",
      ].filter(Boolean).join(" ")}
      draggable={false}
      onMouseDown={handleMouseDown}
      onClick={handleClick}
      onDoubleClick={handleDoubleClick}
      onFocus={() => {
        setFocused(true);
        onFocus();
      }}
      onBlur={() => {
        setFocused(false);
        onBlur();
      }}
      data-testid={`microflow-node-${data.objectId}`}
      data-microflow-object-id={data.objectId}
      data-microflow-collection-id={data.collectionId}
      data-node-selected={String(selected)}
      data-node-active={String(activated)}
      tabIndex={0}
    >
      <div className="microflow-flowgram-node__header">
        <span className="microflow-flowgram-node__icon" />
        <div className="microflow-flowgram-node__text">
          <Typography.Text
            strong
            title={`${data.title}（双击切换编辑）`}
            style={{ maxWidth: "100%", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
          >
            {data.title}
          </Typography.Text>
          {data.subtitle ? (
            <Typography.Text
              type="tertiary"
              size="small"
              title={`${data.subtitle}（双击编辑）`}
              style={{ maxWidth: "100%", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
            >
              {data.subtitle}
            </Typography.Text>
          ) : null}
        </div>
        <button
          type="button"
          className="microflow-flowgram-node__expand-btn"
          onMouseDown={event => {
            event.preventDefault();
            event.stopPropagation();
          }}
          onClick={event => {
            event.stopPropagation();
            toggleExpanded();
          }}
          aria-label={isExpanded ? "收起节点" : "展开节点"}
          title={isExpanded ? "收起" : "编辑"}
        >
          {isExpanded ? "完成" : "编辑"}
        </button>
      </div>
      <div className="microflow-flowgram-node__meta">
        <StaticTag>{data.actionKind || data.objectKind}</StaticTag>
      </div>
      {compactSummary.length > 0 ? (
        <div className="microflow-mini-summary" data-testid={`microflow-node-summary-${data.objectId}`}>
          {compactSummary.map(line => (
            <div key={line.id} className="microflow-inline-summary__line" title={`${line.label ? `${line.label}: ` : ""}${line.value}`}>
              {line.label ? <Typography.Text type="tertiary" size="small">{line.label}: </Typography.Text> : null}
              <Typography.Text size="small">{line.value}</Typography.Text>
            </div>
          ))}
          {summaryOverflowCount > 0 ? <Typography.Text type="tertiary" size="small">+{summaryOverflowCount} more</Typography.Text> : null}
        </div>
      ) : null}
      {runtime ? (
        <button
          type="button"
          className="microflow-mini-runtime"
          onClick={event => {
            event.stopPropagation();
            emitInlineNodeInspect({
              nodeId: data.objectId,
              runtimeNodeId: String(props.node.id),
              inspect: runtime.error ? "error" : "runtime",
            });
            if (!isExpanded) {
              toggleExpanded();
            }
          }}
        >
          {runtimeStateLabel ? <Typography.Text type={runtime.error || runtime.failed ? "danger" : "tertiary"} size="small">{runtimeStateLabel}</Typography.Text> : null}
          {typeof runtime.durationMs === "number" ? (
            <Typography.Text type="tertiary" size="small">{runtime.durationMs}ms</Typography.Text>
          ) : null}
          {runtime.selectedBranchLabel ? <Typography.Text type="tertiary" size="small">selected: {runtime.selectedBranchLabel}</Typography.Text> : null}
          {!runtime.selectedBranchLabel && runtime.outputPreview ? <Typography.Text type="tertiary" size="small">{runtime.outputPreview}</Typography.Text> : null}
        </button>
      ) : null}
      {isExpanded ? (
        <InlineNodeEditor
          inlineConfig={data.inlineConfig}
          onCommitField={(field, value) => {
            emitInlineFieldCommit({
              nodeId: data.objectId,
              fieldPath: field.fieldPath,
              editType: field.editType,
              value,
            });
          }}
          onApplyQuickFix={suggestion => {
            emitInlineQuickFix({
              nodeId: data.objectId,
              suggestionId: suggestion.id,
              actionKind: suggestion.actionKind,
              fieldPath: suggestion.fieldPath,
              value: suggestion.value,
              editType: suggestion.editType,
            });
          }}
        />
      ) : null}
      {ports.map(port => (
        <FlowGramMicroflowPortRenderer key={port.id} port={port} />
      ))}
    </div>
  );
}
