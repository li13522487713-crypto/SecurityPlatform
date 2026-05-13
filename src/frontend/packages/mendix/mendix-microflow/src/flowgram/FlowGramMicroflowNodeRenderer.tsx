import {
  memo,
  useContext,
  useState,
  type CSSProperties,
  type MouseEvent,
  type ReactNode,
} from "react";

import { Tag, Typography } from "@douyinfe/semi-ui";
import { getMendixMicroflowCopy } from "../i18n/copy";
import { AnnotationNode } from "../components/AnnotationNode";
import { ActivityNode } from "../components/ActivityNode";
import type { MicroflowActionActivityColor } from "../schema";
import {
  FlowNodeFormData,
  type FormModelV2,
  type WorkflowNodeRenderProps,
  usePlaygroundReadonlyState,
  useNodeRender,
  useService,
  WorkflowDragService,
  WorkflowSelectService,
} from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowNodeData } from "./FlowGramMicroflowTypes";
import { MicroflowNodeUsageHighlightsContext } from "./FlowGramMicroflowTypes";
import { FlowGramMicroflowPortRenderer } from "./FlowGramMicroflowPortRenderer";
import { emitInlineNodeInspect } from "./inline-events";
import {
  focusMicroflowNodeDragRoot,
  isMicroflowNodeDragBlockedTarget,
} from "./flowgram-node-drag";
import { NodeIcon } from "./NodeIcon";
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
    // FlowGram's form model can lag behind doc.fromJSON() when the editor projects
    // transient render state such as inlineConfig.viewMode. Prefer the doc JSON
    // snapshot for those projected fields, while still using the form model as a
    // fallback for stable authoring data.
    const merged = {
      ...(primary ?? {}),
      ...(normalizedJsonData ?? {}),
    };
    return {
      ...merged,
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
      usageSourceHighlight: Boolean(primary?.usageSourceHighlight ?? normalizedJsonData?.usageSourceHighlight ?? false),
      usageConsumerHighlight: Boolean(primary?.usageConsumerHighlight ?? normalizedJsonData?.usageConsumerHighlight ?? false),
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

function disabledAwareTitle(title: string, disabled: boolean | undefined): string {
  return disabled ? `${title} [Disabled]` : title;
}

function summarizeWhileExpression(raw: string | undefined): string {
  const text = String(raw ?? "").trim();
  if (!text) {
    return "while (condition)";
  }
  return text.length > 36 ? `while ${text.slice(0, 33)}...` : `while ${text}`;
}

function nodeUsageAliases(nodeId: string, objectId?: string): string[] {
  const aliases = new Set<string>();
  for (const value of [nodeId, objectId]) {
    if (!value) {
      continue;
    }
    aliases.add(value);
    if (value.startsWith("node-")) {
      aliases.add(value.slice("node-".length));
    } else {
      aliases.add(`node-${value}`);
    }
  }
  return [...aliases];
}

type NodeCategory = "data" | "variable" | "list" | "flow" | "event" | "call" | "parallel" | "loop";

type NodeTone = "start" | "end" | "error" | "continue" | "break" | "decision" | "merge" | "loop" | "parameter" | "annotation" | "action";
type ErrorHandlingBadge = { text: "R" | "C" | "!"; className: string };

const NODE_CATEGORY_STYLE: Record<NodeCategory, {
  iconColor: string;
  iconBg: string;
  borderColor: string;
}> = {
  data: { iconColor: "#93c5fd", iconBg: "#1e4490", borderColor: "#1e3a70" },
  variable: { iconColor: "#fcd34d", iconBg: "#4a3000", borderColor: "#3a2800" },
  list: { iconColor: "#6ee7b7", iconBg: "#0d3824", borderColor: "#0a2e1e" },
  flow: { iconColor: "#c4b5fd", iconBg: "#321e5a", borderColor: "#2a1a4a" },
  event: { iconColor: "#f9a8d4", iconBg: "#4a0a2a", borderColor: "#3a0a24" },
  call: { iconColor: "#f9a8d4", iconBg: "#5b21b6", borderColor: "#4c1d95" },
  parallel: { iconColor: "#67e8f9", iconBg: "#0e3a4a", borderColor: "#0a2e3a" },
  loop: { iconColor: "#a78bfa", iconBg: "#2d1a5e", borderColor: "#261655" },
};

const ACTIVITY_BACKGROUND_COLOR_STYLE: Record<Exclude<MicroflowActionActivityColor, "default">, { iconBg: string; iconColor: string }> = {
  blue: { iconBg: "#1e3a70", iconColor: "#93c5fd" },
  green: { iconBg: "#0d3824", iconColor: "#6ee7b7" },
  yellow: { iconBg: "#4a3000", iconColor: "#fcd34d" },
  orange: { iconBg: "#4a2000", iconColor: "#fdba74" },
  red: { iconBg: "#4a0a0a", iconColor: "#f87171" },
  purple: { iconBg: "#321e5a", iconColor: "#c4b5fd" },
  gray: { iconBg: "#1e2235", iconColor: "#d1d5db" },
};

function iconStyleForActionBackgroundColor(
  backgroundColor: MicroflowActionActivityColor | undefined,
  fallback: { iconBg: string; iconColor: string },
): { background: string; color: string } {
  if (!backgroundColor || backgroundColor === "default") {
    return { background: fallback.iconBg, color: fallback.iconColor };
  }
  const mapped = ACTIVITY_BACKGROUND_COLOR_STYLE[backgroundColor];
  return mapped
    ? { background: mapped.iconBg, color: mapped.iconColor }
    : { background: fallback.iconBg, color: fallback.iconColor };
}

function surfacePaletteForBackgroundColor(backgroundColor: MicroflowActionActivityColor | undefined): { background: string; borderColor: string; accentColor: string } | undefined {
  if (!backgroundColor || backgroundColor === "default") {
    return undefined;
  }
  const mapped = ACTIVITY_BACKGROUND_COLOR_STYLE[backgroundColor];
  if (!mapped) {
    return undefined;
  }
  return {
    background: mapped.iconBg,
    borderColor: mapped.iconColor,
    accentColor: mapped.iconColor,
  };
}

function nodeCategory(kind: string): NodeCategory {
  if ([
    "createObject", "changeObject", "retrieveObject", "commitObject", "deleteObject",
    "objectCreate", "objectChange", "objectRetrieve", "objectCommit", "objectDelete",
    "objectRollback", "objectCast",
  ].includes(kind)) {
    return "data";
  }
  if ([
    "createVariable", "changeVariable", "variableCreate", "variableChange",
  ].includes(kind)) {
    return "variable";
  }
  if ([
    "filterList", "sortList", "aggregateList",
    "listFilter", "listSort", "listAggregate", "listCreate", "listChange", "listOperation",
  ].includes(kind)) {
    return "list";
  }
  if ([
    "callMicroflow", "callRest", "javaAction", "microflowCall", "nanoflowCall",
    "httpRequest", "restCall", "logMessage", "throwException",
  ].includes(kind)) {
    return "call";
  }
  if ([
    "parallelSplit", "parallelMerge", "parallelGateway", "inclusiveGateway",
  ].includes(kind)) {
    return "parallel";
  }
  if (kind === "loopedActivity") {
    return "loop";
  }
  return "flow";
}

function nodeTone(kind: FlowGramMicroflowNodeData["objectKind"]): NodeTone {
  if (kind === "startEvent") {
    return "start";
  }
  if (kind === "endEvent") {
    return "end";
  }
  if (kind === "errorEvent") {
    return "error";
  }
  if (kind === "continueEvent") {
    return "continue";
  }
  if (kind === "breakEvent") {
    return "break";
  }
  if (kind === "exclusiveSplit" || kind === "inheritanceSplit") {
    return "decision";
  }
  if (kind === "parallelGateway" || kind === "inclusiveGateway") {
    return "decision";
  }
  if (kind === "exclusiveMerge") {
    return "merge";
  }
  if (kind === "annotation") {
    return "annotation";
  }
  if (kind === "parameterObject") {
    return "parameter";
  }
  if (kind === "loopedActivity") {
    return "loop";
  }
  return "action";
}

function errorHandlingBadge(type: string | undefined): ErrorHandlingBadge | undefined {
  if (type === "customWithRollback") {
    return { text: "R", className: "microflow-node-error-badge--rollback" };
  }
  if (type === "customWithoutRollback") {
    return { text: "C", className: "microflow-node-error-badge--custom" };
  }
  if (type === "continue") {
    return { text: "!", className: "microflow-node-error-badge--continue" };
  }
  return undefined;
}

function StaticTag(props: { children: ReactNode; color?: "blue" | "orange" | "grey" | "green" }) {
  return (
    <span draggable={false} data-flow-editor-selectable="false">
      <Tag size="small" color={props.color}>{props.children}</Tag>
    </span>
  );
}

function FlowGramMicroflowNodeRendererInner(props: WorkflowNodeRenderProps) {
  const { selected, activated, ports, selectNode, nodeRef, startDrag, onFocus, onBlur } = useNodeRender();
  const readonly = usePlaygroundReadonlyState();
  const dragService = useService<WorkflowDragService>(WorkflowDragService);
  const selectService = useService<WorkflowSelectService>(WorkflowSelectService);
  const [focused, setFocused] = useState(false);
  const data = tryReadNodeData(props);
  const resolvedNodeIdForState = String(data?.objectId || props.node.id);
  const usageHighlights = useContext(MicroflowNodeUsageHighlightsContext);

  const canStartNodeDrag = (event: MouseEvent<HTMLDivElement>) => {
    if (readonly || event.button !== 0) {
      return false;
    }
    if (data?.objectKind === "startEvent") {
      return false;
    }
    if (event.detail > 1) {
      return false;
    }
    return !isMicroflowNodeDragBlockedTarget(event.target);
  };

  const handleMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    if (event.button === 0 && !isMicroflowNodeDragBlockedTarget(event.target)) {
      selectNode(event);
    }
    if (!canStartNodeDrag(event)) {
      return;
    }
    focusMicroflowNodeDragRoot(event.currentTarget);
    // When this node is part of a multi-selection, move all selected nodes together.
    const selectionCount = (selectService.selection ?? []).length;
    if (selected && selectionCount > 1) {
      void dragService.startDragSelectedNodes(event.nativeEvent as globalThis.MouseEvent);
      return;
    }
    startDrag(event);
  };

  const handleFallbackClick = (event: MouseEvent<HTMLDivElement>) => {
    selectNode(event);
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
        onClick={handleFallbackClick}
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
  const displayTitle = disabledAwareTitle(data.title, data.disabled);
  const summaryLines = data.inlineConfig?.summaryLines ?? [];
  const compactSummary = summaryLines.slice(0, 3);
  const summaryOverflowCount = Math.max(0, summaryLines.length - compactSummary.length);
  const runtime = data.inlineConfig?.runtime;
  const runtimeCopy = getMendixMicroflowCopy().runtimeInspector;
  const runtimeOutputSummaries = runtime?.outputSummaries ?? [];
  const compactRuntimeOutputs = runtimeOutputSummaries.slice(0, 2);
  const runtimeOutputOverflowCount = Math.max(0, runtimeOutputSummaries.length - compactRuntimeOutputs.length);
  const decisionRuntimeResult = tone === "decision" ? String(runtime?.selectedBranchLabel ?? "").trim() : "";
  const resolvedNodeId = resolvedNodeIdForState;
  const usageSourceHighlight = Boolean(data?.usageSourceHighlight)
    || nodeUsageAliases(String(props.node.id), data?.objectId).some(alias => usageHighlights.sourceNodeIds.includes(alias));
  const usageConsumerHighlight = Boolean(data?.usageConsumerHighlight)
    || nodeUsageAliases(String(props.node.id), data?.objectId).some(alias => usageHighlights.consumerNodeIds.includes(alias));
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
  const runtimeDetailLines: string[] = [];
  if (runtime?.decisionExpression) {
    runtimeDetailLines.push(`Expr: ${runtime.decisionExpression}`);
  }
  if (runtime?.decisionEvaluatedValue) {
    runtimeDetailLines.push(`Result: ${runtime.decisionEvaluatedValue}`);
  }
  if (runtime?.selectedBranchLabel) {
    runtimeDetailLines.push(`${runtimeCopy.selected}: ${runtime.selectedBranchLabel}`);
  }
  if (runtime?.loopIterationLabel) {
    runtimeDetailLines.push(runtime.loopIterationLabel);
  }
  if (runtime?.gatewayProgressLabel) {
    runtimeDetailLines.push(runtime.gatewayProgressLabel);
  }
  if (runtime?.gatewayMergeLabel) {
    runtimeDetailLines.push(runtime.gatewayMergeLabel);
  }
  if (runtime?.inputPreview) {
    runtimeDetailLines.push(`Input: ${runtime.inputPreview}`);
  }
  if (runtime?.outputPreview) {
    runtimeDetailLines.push(`Output: ${runtime.outputPreview}`);
  }
  if (runtime?.deltaPreview) {
    runtimeDetailLines.push(`Delta: ${runtime.deltaPreview}`);
  }
  if (runtimeDetailLines.length === 0 && compactRuntimeOutputs.length > 0) {
    runtimeDetailLines.push(`${compactRuntimeOutputs.join(" · ")}${runtimeOutputOverflowCount > 0 ? ` +${runtimeOutputOverflowCount}` : ""}`);
  }
  if (runtimeDetailLines.length === 0) {
    runtimeDetailLines.push(runtimeCopy.noOutput);
  }

  const handleNodeClick = (event: MouseEvent<HTMLDivElement>) => {
    const target = event.target as Element | null;
    const interactiveTarget = target?.closest?.("button,input,textarea,select,[contenteditable='true']");
    if (interactiveTarget) {
      return;
    }
    selectNode(event);
  };

  const categoryStyle = NODE_CATEGORY_STYLE[nodeCategory(data.objectKind)];
  const actionIconStyle = iconStyleForActionBackgroundColor(data.backgroundColor, categoryStyle);
  const surfacePalette = surfacePaletteForBackgroundColor(data.backgroundColor);
  const activitySubtitle = data.subtitle?.trim()
    ? data.subtitle
    : nodeKindLabel(data.actionKind || data.objectKind);
  const nodeErrorHandling = data.action?.errorHandlingType ?? data.errorHandlingType;
  const nodeErrorBadge = errorHandlingBadge(nodeErrorHandling);
  const decisionSurfaceStyle: CSSProperties | undefined = surfacePalette
    ? { background: surfacePalette.background, borderColor: surfacePalette.borderColor, color: surfacePalette.accentColor }
    : undefined;
  const decisionIconStyle: CSSProperties = { color: surfacePalette?.accentColor ?? categoryStyle.iconColor };
  const loopHeaderStyle: CSSProperties | undefined = surfacePalette
    ? { background: surfacePalette.background, color: surfacePalette.accentColor, borderBottomColor: `${surfacePalette.accentColor}33` }
    : undefined;
  const loopIconStyle: CSSProperties = { color: surfacePalette?.accentColor ?? NODE_CATEGORY_STYLE[nodeCategory(data.objectKind)].iconColor };
  const annotationStyle: CSSProperties | undefined = surfacePalette
    ? { background: surfacePalette.background, borderColor: surfacePalette.borderColor, color: surfacePalette.accentColor }
    : undefined;
  const parameterStyle: CSSProperties | undefined = surfacePalette
    ? ({
        ["--microflow-parameter-bg" as const]: surfacePalette.background,
        ["--microflow-parameter-border" as const]: surfacePalette.borderColor,
        ["--microflow-parameter-accent" as const]: surfacePalette.accentColor,
      } as CSSProperties)
    : undefined;

  return (
    <div
      ref={nodeRef}
      className={[
        "microflow-flowgram-node",
        `microflow-flowgram-node--${tone}`,
        `microflow-flowgram-node--category-${nodeCategory(data.objectKind)}`,
        selected ? "is-selected" : "",
        activated ? "is-active" : "",
        focused ? "is-focused" : "",
        data.disabled ? "is-disabled" : "",
        data.validationState !== "valid" ? `is-${data.validationState}` : "",
        data.runtimeState && data.runtimeState !== "idle" ? `is-runtime-${data.runtimeState}` : "",
        usageSourceHighlight ? "is-usage-source" : "",
        usageConsumerHighlight ? "is-usage-consumer" : "",
      ].filter(Boolean).join(" ")}
      draggable={false}
      onMouseDown={handleMouseDown}
      onClick={handleNodeClick}
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
      data-node-category={nodeCategory(data.objectKind)}
      data-node-usage-source={String(usageSourceHighlight)}
      data-node-usage-consumer={String(usageConsumerHighlight)}
      tabIndex={0}
    >
      <div className="microflow-flowgram-node__compact" data-node-tone={tone}>
        {tone === "start" || tone === "end" || tone === "error" || tone === "continue" || tone === "break" ? (
          <div
            className="microflow-event-dot"
            title={tone === "start" ? `${data.title}（起始节点位置固定）` : data.title}
            data-node-tone={tone}
          >
            <span
              className="microflow-event-dot__core"
              aria-hidden="true"
            />
            {tone === "error" || tone === "continue" || tone === "break" ? (
              <span className="microflow-event-dot__icon" aria-hidden="true">
                <NodeIcon kind={data.objectKind} />
              </span>
            ) : null}
            {tone === "start" ? (
              <span className="microflow-event-dot__lock-badge" aria-label="起始节点位置固定" title="起始节点位置固定">
                <svg width="8" height="8" viewBox="0 0 10 10" fill="currentColor" aria-hidden="true">
                  <rect x="2" y="5" width="6" height="5" rx="1" />
                  <path d="M3 5V3.5a2 2 0 0 1 4 0V5" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" fill="none" />
                </svg>
              </span>
            ) : null}
          </div>
        ) : tone === "parameter" ? (
          <div
            className="microflow-parameter-compact"
            title={data.parameterTypeLabel ? `${displayTitle}\n${data.parameterTypeLabel}` : displayTitle}
            data-parameter-kind={data.parameterKind ?? "primitive"}
            style={parameterStyle}
          >
            <span
              className="microflow-parameter-compact__icon"
              aria-hidden="true"
            >
              <NodeIcon kind={data.objectKind} />
            </span>
            <span className="microflow-parameter-compact__text-wrap">
              <span className="microflow-parameter-compact__name">{displayTitle}</span>
              <span className="microflow-parameter-compact__type">{data.parameterTypeLabel ?? "unknown"}</span>
            </span>
          </div>
        ) : tone === "decision" ? (
          <div className="microflow-decision-compact">
            <div
              className={[
                "microflow-decision-compact__diamond",
                data.objectKind === "inheritanceSplit" ? "is-object-type" : "",
              ].filter(Boolean).join(" ")}
              aria-hidden="true"
              data-decision-kind={data.objectKind === "inheritanceSplit" ? "objectType" : "expression"}
              style={decisionSurfaceStyle}
            >
              <span
                className="microflow-node-compact-icon-wrap"
                style={decisionIconStyle}
              >
                <NodeIcon kind={data.objectKind} />
              </span>
              {data.runtimeState === "failed" ? <span className="microflow-node-runtime-error-dot" aria-hidden /> : null}
            </div>
            <div className="microflow-node-caption" title={displayTitle}>{displayTitle}</div>
            {decisionRuntimeResult ? (
              <div
                className="microflow-decision-result-pill"
                title={`result: ${decisionRuntimeResult}`}
                data-testid={`microflow-decision-result-${data.objectId}`}
              >
                {decisionRuntimeResult}
              </div>
            ) : null}
          </div>
        ) : tone === "merge" ? (
          <div className="microflow-merge-compact">
            <div className="microflow-merge-compact__diamond" aria-hidden="true" style={decisionSurfaceStyle}>
              <span
                className="microflow-node-compact-icon-wrap"
                style={decisionIconStyle}
              >
                <NodeIcon kind={data.objectKind} />
              </span>
              {data.runtimeState === "failed" ? <span className="microflow-node-runtime-error-dot" aria-hidden /> : null}
            </div>
            <div className="microflow-node-caption" title={displayTitle}>{displayTitle}</div>
          </div>
        ) : tone === "loop" ? (
          <div className="microflow-loop-frame" title={displayTitle}>
          <div className="microflow-loop-frame__header" style={loopHeaderStyle}>
            <span
              className="microflow-loop-frame__kind-badge"
              data-loop-kind={data.loopSource?.kind === "iterableList" ? "for" : "while"}
            >
              {data.loopSource?.kind === "iterableList" ? "for" : "while"}
            </span>
            <span
              aria-hidden="true"
              style={loopIconStyle}
            >
              <NodeIcon kind={data.objectKind} />
            </span>
            <span>{displayTitle}</span>
            {data.loopIteration?.iterationIndex != null || data.loopIteration?.totalIterations != null ? (
              <span
                className="microflow-loop-frame__progress"
                data-testid={`microflow-loop-iteration-${data.objectId}`}
                title={`第 ${data.loopIteration?.iterationIndex ?? "-"} / ${data.loopIteration?.totalIterations ?? "-"} 次`}
              >
                第 {data.loopIteration?.iterationIndex ?? "-"} / {data.loopIteration?.totalIterations ?? "-"} 次
              </span>
            ) : null}
          </div>
            <div className="microflow-loop-frame__body">
              <span>
                {data.loopSource?.kind === "iterableList"
                  ? `For each ${data.iteratorVariableName ?? "item"} in ${data.listVariableName ?? "list"}`
                  : summarizeWhileExpression(data.loopSource?.expression?.raw)}
              </span>
            </div>
          </div>
        ) : tone === "annotation" ? (
          <AnnotationNode
            title={displayTitle}
            style={annotationStyle}
            icon={(
              <span
                aria-hidden="true"
                style={{ color: surfacePalette?.accentColor ?? NODE_CATEGORY_STYLE[nodeCategory(data.objectKind)].iconColor }}
              >
                <NodeIcon kind={data.objectKind} />
              </span>
            )}
          />
        ) : (
          <ActivityNode
            title={displayTitle}
            subtitle={activitySubtitle}
            icon={<NodeIcon kind={data.objectKind} />}
            iconStyle={actionIconStyle}
            showRuntimeErrorDot={false}
            runtimeInfo={runtime ? {
              state: runtime.running ? "running" : runtime.success ? "success" : runtime.failed ? "failed" : runtime.skipped ? "skipped" : "idle",
              durationMs: typeof runtime.durationMs === "number" ? runtime.durationMs : undefined,
              errorMessage: runtime.error ?? undefined,
            } : undefined}
          />
        )}
        {nodeErrorBadge && (tone === "action" || tone === "loop" || tone === "decision") ? (
          <span
            className={`microflow-node-error-badge ${nodeErrorBadge.className}`}
            title={nodeErrorBadge.text === "R"
              ? "Custom with Rollback"
              : nodeErrorBadge.text === "C"
                ? "Custom without Rollback"
                : "Continue on Error"}
            aria-label={`error-handling-${nodeErrorBadge.text}`}
          >
            {nodeErrorBadge.text}
          </span>
        ) : null}
        {data.hasBreakpoint ? (
          <span
            className={`microflow-node-breakpoint-dot ${data.breakpointKind === "conditional" ? "is-conditional" : "is-normal"}`}
            title={data.breakpointKind === "conditional" ? "Conditional breakpoint" : "Breakpoint"}
            aria-label={data.breakpointKind === "conditional" ? "conditional-breakpoint" : "breakpoint"}
          />
        ) : null}
      </div>
      {usageConsumerHighlight ? (
        <div className="microflow-node-usage-pill">
          <StaticTag color="green">Usage</StaticTag>
        </div>
      ) : null}
      <div className="microflow-flowgram-node__meta" aria-hidden="true">
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
          }}
        >
          <div className="microflow-mini-runtime__header">
            {runtimeStateLabel ? <Typography.Text type={runtime.error || runtime.failed ? "danger" : "tertiary"} size="small">{runtimeStateLabel}</Typography.Text> : null}
            {typeof runtime.durationMs === "number" ? (
              <Typography.Text type="tertiary" size="small">{runtime.durationMs}ms</Typography.Text>
            ) : null}
          </div>
          {runtimeDetailLines.slice(0, 3).map((line, index) => (
            <Typography.Text key={`${data.objectId}-runtime-${index}`} type="tertiary" size="small">{line}</Typography.Text>
          ))}
          {runtimeDetailLines.length > 3 ? (
            <Typography.Text type="tertiary" size="small">+{runtimeDetailLines.length - 3} more</Typography.Text>
          ) : null}
        </button>
      ) : null}
      {ports.map(port => (
        <FlowGramMicroflowPortRenderer key={port.id} port={port} />
      ))}
    </div>
  );
}

export const FlowGramMicroflowNodeRenderer = memo(FlowGramMicroflowNodeRendererInner);
