import { useState, type MouseEvent, type ReactNode } from "react";

import { Tag, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconEdit, IconTickCircle } from "@douyinfe/semi-icons";
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
    const formValue = formModel?.getFormItemValueByPath("/") as Partial<FlowGramMicroflowNodeData> | undefined;
    const jsonData = (props.node as unknown as { toJSON?: () => { data?: FlowGramMicroflowNodeData } })
      .toJSON?.()
      ?.data;
    const normalizedJsonData = jsonData as Partial<FlowGramMicroflowNodeData> | undefined;
    const nodeMeta = props.node.getNodeMeta?.() as { nodeDTOType?: string; type?: string } | undefined;
    const fallbackKind = nodeMeta?.nodeDTOType ?? nodeMeta?.type;
    const primary = formValue?.objectKind ? formValue : normalizedJsonData?.objectKind ? normalizedJsonData : undefined;
    const objectKind = primary?.objectKind ?? fallbackKind;
    if (!objectKind) {
      return undefined;
    }
    return {
      ...(normalizedJsonData ?? {}),
      ...(primary ?? {}),
      objectId: String(primary?.objectId ?? normalizedJsonData?.objectId ?? props.node.id),
      objectKind: objectKind as FlowGramMicroflowNodeData["objectKind"],
      collectionId: String(primary?.collectionId ?? normalizedJsonData?.collectionId ?? ""),
      title: String(primary?.title ?? normalizedJsonData?.title ?? objectKind),
      subtitle: primary?.subtitle ?? normalizedJsonData?.subtitle ?? undefined,
      officialType: String(primary?.officialType ?? normalizedJsonData?.officialType ?? objectKind),
      disabled: Boolean(primary?.disabled ?? normalizedJsonData?.disabled ?? false),
      validationState: (primary?.validationState ?? normalizedJsonData?.validationState ?? "valid") as FlowGramMicroflowNodeData["validationState"],
      runtimeState: (primary?.runtimeState ?? normalizedJsonData?.runtimeState ?? "idle") as FlowGramMicroflowNodeData["runtimeState"],
      issueCount: Number(primary?.issueCount ?? normalizedJsonData?.issueCount ?? 0),
    };
  } catch {
    return undefined;
  }
}

const OBJECT_KIND_LABEL: Record<string, string> = {
  startEvent: "开始",
  endEvent: "结束",
  errorEvent: "错误结束",
  exclusiveSplit: "条件分支",
  inheritanceSplit: "继承分支",
  loopedActivity: "循环",
  annotation: "注释",
  parameterObject: "参数对象",
  httpRequest: "HTTP 请求",
  javaAction: "Java 动作",
  microflowCall: "调用微流",
  nanoflowCall: "调用纳流",
  actionActivity: "动作",
  mergeActivity: "合并",
  tryCatch: "异常捕获",
  parallelSplit: "并行分支",
  parallelMerge: "并行合并",
};

function nodeKindLabel(kind: string): string {
  return OBJECT_KIND_LABEL[kind] ?? kind;
}

/** 每种节点类型的语义 SVG 图标（16×16 viewBox） */
function NodeIcon({ kind }: { kind: string }) {
  const base = { width: 14, height: 14, viewBox: "0 0 16 16", fill: "currentColor", "aria-hidden": true as const, style: { display: "block" } };
  switch (kind) {
    case "startEvent":
      // 实心播放三角
      return <svg {...base}><polygon points="3,2 14,8 3,14" /></svg>;
    case "endEvent":
      // 实心正方形
      return <svg {...base}><rect x="2" y="2" width="12" height="12" rx="1" /></svg>;
    case "errorEvent":
      // X 形
      return <svg {...base}><path d="M3 3l10 10M13 3L3 13" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" fill="none" /></svg>;
    case "exclusiveSplit":
    case "inheritanceSplit":
      // 菱形
      return <svg {...base}><polygon points="8,1 15,8 8,15 1,8" /></svg>;
    case "loopedActivity":
      // 循环箭头
      return <svg {...base}><path d="M8 2a6 6 0 1 1-4.24 1.76M8 2V6M8 2L5 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "annotation":
      // 文档/注释
      return <svg {...base}><rect x="2" y="1" width="10" height="14" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" /><line x1="5" y1="5" x2="9" y2="5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /><line x1="5" y1="8" x2="9" y2="8" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /><line x1="5" y1="11" x2="7" y2="11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>;
    case "httpRequest":
      // 地球/网络
      return <svg {...base}><circle cx="8" cy="8" r="6" fill="none" stroke="currentColor" strokeWidth="1.5" /><ellipse cx="8" cy="8" rx="2.5" ry="6" fill="none" stroke="currentColor" strokeWidth="1.2" /><line x1="2" y1="8" x2="14" y2="8" stroke="currentColor" strokeWidth="1.2" /></svg>;
    case "javaAction":
    case "microflowCall":
    case "nanoflowCall":
      // 闪电/执行
      return <svg {...base}><polygon points="9,1 3,9 8,9 7,15 13,7 8,7" /></svg>;
    case "parallelSplit":
    case "parallelMerge":
      // 双竖线（并行）
      return <svg {...base}><rect x="3" y="2" width="3" height="12" rx="1" /><rect x="10" y="2" width="3" height="12" rx="1" /></svg>;
    case "tryCatch":
      // 盾牌
      return <svg {...base}><path d="M8 1L2 4v4c0 3 2.7 5.7 6 7 3.3-1.3 6-4 6-7V4L8 1z" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" /></svg>;
    case "mergeActivity":
      // 汇聚箭头
      return <svg {...base}><path d="M2 4l6 4-6 4M14 4l-6 4 6 4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    default:
      // 通用动作：齿轮
      return <svg {...base}><circle cx="8" cy="8" r="2.5" fill="none" stroke="currentColor" strokeWidth="1.5" /><path d="M8 1v2M8 13v2M1 8h2M13 8h2M3.1 3.1l1.4 1.4M11.5 11.5l1.4 1.4M3.1 12.9l1.4-1.4M11.5 4.5l1.4-1.4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>;
  }
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
  const resolvedNodeId = String(data.objectId || props.node.id);
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
    emitInlineNodeToggle({ nodeId: resolvedNodeId, runtimeNodeId: String(props.node.id), expanded: !isExpanded });
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
        <span className="microflow-flowgram-node__icon">
          <NodeIcon kind={data.objectKind} />
        </span>
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
        <Tooltip content={isExpanded ? "收起（Esc）" : "展开编辑（双击节点也可）"} position="top">
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
          >
            {isExpanded
              ? <><IconTickCircle style={{ marginRight: 3, verticalAlign: "middle" }} />完成</>
              : <><IconEdit style={{ marginRight: 3, verticalAlign: "middle" }} />编辑</>}
          </button>
        </Tooltip>
      </div>
      <div className="microflow-flowgram-node__meta">
        <StaticTag>{nodeKindLabel(data.actionKind || data.objectKind)}</StaticTag>
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
              nodeId: resolvedNodeId,
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
              nodeId: resolvedNodeId,
              fieldPath: field.fieldPath,
              editType: field.editType,
              value,
            });
          }}
          onApplyQuickFix={suggestion => {
            emitInlineQuickFix({
              nodeId: resolvedNodeId,
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
