import {
  memo,
  useContext,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
  type MouseEvent,
  type ReactNode,
  type PointerEvent as ReactPointerEvent,
  type TransitionEvent,
} from "react";

import { Tag, Typography } from "@douyinfe/semi-ui";
import { IconEdit, IconTickCircle } from "@douyinfe/semi-icons";
import { getMendixMicroflowCopy } from "../i18n/copy";
import { InlineNodeEditor } from "../inline-edit";
import { AnnotationNode } from "../components/AnnotationNode";
import { ActivityNode } from "../components/ActivityNode";
import type { MicroflowActionActivityColor } from "../schema";
import {
  FlowNodeFormData,
  type FormModelV2,
  type WorkflowNodeRenderProps,
  usePlaygroundReadonlyState,
  useNodeRender,
} from "@flowgram-adapter/free-layout-editor";

import type { FlowGramMicroflowNodeData } from "./FlowGramMicroflowTypes";
import { MicroflowNodeUsageHighlightsContext, MicroflowNodeViewModesContext } from "./FlowGramMicroflowTypes";
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

function stopEditorControlEvent(event: MouseEvent<HTMLElement> | ReactPointerEvent<HTMLElement>) {
  event.preventDefault();
  event.stopPropagation();
}

function isHeaderInlineControlRegion(event: MouseEvent<HTMLElement> | ReactPointerEvent<HTMLElement>): boolean {
  const rect = event.currentTarget.getBoundingClientRect();
  return event.clientX >= rect.right - 104
    && event.clientX <= rect.right
    && event.clientY >= rect.top
    && event.clientY <= rect.top + 44;
}

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
    case "continueEvent":
      // 继续箭头
      return <svg {...base}><path d="M3 8h8M8 4l4 4-4 4" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "breakEvent":
      // 停止方块
      return <svg {...base}><rect x="4" y="4" width="8" height="8" rx="1.3" /></svg>;
    case "parameterObject":
      // 参数椭圆里的 P
      return <svg {...base}><path d="M5 12V4h4.1a2.9 2.9 0 0 1 0 5.8H5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "exclusiveSplit":
      // 分支语义：主干 + 两条分支
      return <svg {...base}><path d="M4 3v4M12 3v4M8 5v6M4 7h8M8 11l-3 2M8 11l3 2" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "inheritanceSplit":
      // 继承分支（主干 + 子分支）
      return <svg {...base}><path d="M8 2v3M4 7h8M4 7v5M12 7v5" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
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

type NodeCategory = "data" | "variable" | "list" | "flow" | "event";

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
    "createObject",
    "changeObject",
    "retrieveObject",
    "commitObject",
    "deleteObject",
  ].includes(kind)) {
    return "data";
  }
  if ([
    "createVariable",
    "changeVariable",
  ].includes(kind)) {
    return "variable";
  }
  if ([
    "filterList",
    "sortList",
    "aggregateList",
  ].includes(kind)) {
    return "list";
  }
  if ([
    "callMicroflow",
    "callRest",
    "javaAction",
    "microflowCall",
    "nanoflowCall",
    "httpRequest",
    "actionActivity",
    "tryCatch",
    "parallelSplit",
    "parallelMerge",
    "exclusiveSplit",
    "inheritanceSplit",
    "loopedActivity",
    "startEvent",
    "endEvent",
    "errorEvent",
    "breakEvent",
    "continueEvent",
    "annotation",
    "parameterObject",
  ].includes(kind)) {
    return "flow";
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
  const [focused, setFocused] = useState(false);
  const data = tryReadNodeData(props);
  const resolvedNodeIdForState = String(data?.objectId || props.node.id);
  const nodeViewModesCtx = useContext(MicroflowNodeViewModesContext);
  const usageHighlights = useContext(MicroflowNodeUsageHighlightsContext);
  const dataProjectedViewMode = data?.inlineConfig?.viewMode;
  const projectedExpanded =
    dataProjectedViewMode === "expanded" ||
    nodeViewModesCtx[resolvedNodeIdForState] === "expanded" ||
    nodeViewModesCtx[`node-${resolvedNodeIdForState}`] === "expanded" ||
    nodeViewModesCtx[resolvedNodeIdForState.replace(/^node-/, "")] === "expanded";
  const currentExpanded = projectedExpanded;
  const [editorMounted, setEditorMounted] = useState(currentExpanded);
  const [collapsibleOpen, setCollapsibleOpen] = useState(currentExpanded);
  const prevExpandedRef = useRef<boolean | null>(null);
  const expandedForTransitionEndRef = useRef(currentExpanded);
  expandedForTransitionEndRef.current = currentExpanded;

  useLayoutEffect(() => {
    const prev = prevExpandedRef.current;
    prevExpandedRef.current = currentExpanded;

    const reduced =
      typeof window !== "undefined"
      && typeof window.matchMedia === "function"
      && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    if (!currentExpanded) {
      setCollapsibleOpen(false);
      if (reduced) {
        setEditorMounted(false);
      }
      return;
    }

    setEditorMounted(true);
    if (reduced) {
      setCollapsibleOpen(true);
      return;
    }

    const wasExpanded = prev === true;
    if (!wasExpanded && prev !== null) {
      setCollapsibleOpen(false);
      let raf1 = 0;
      let raf2 = 0;
      raf1 = requestAnimationFrame(() => {
        raf2 = requestAnimationFrame(() => {
          setCollapsibleOpen(true);
        });
      });
      return () => {
        cancelAnimationFrame(raf1);
        cancelAnimationFrame(raf2);
      };
    }

    setCollapsibleOpen(true);
  }, [currentExpanded]);

  const handleCollapsibleTransitionEnd = (event: TransitionEvent<HTMLDivElement>) => {
    if (event.propertyName !== "grid-template-rows") {
      return;
    }
    if (expandedForTransitionEndRef.current) {
      return;
    }
    setEditorMounted(false);
  };

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
    if (isHeaderInlineControlRegion(event)) {
      return false;
    }
    return !isMicroflowNodeDragBlockedTarget(event.target);
  };

  const handleMouseDown = (event: MouseEvent<HTMLDivElement>) => {
    if (event.button === 0 && !isHeaderInlineControlRegion(event) && !isMicroflowNodeDragBlockedTarget(event.target)) {
      selectNode(event);
    }
    if (!canStartNodeDrag(event)) {
      return;
    }
    focusMicroflowNodeDragRoot(event.currentTarget);
    startDrag(event);
  };

  const handleFallbackClick = (event: MouseEvent<HTMLDivElement>) => {
    selectNode(event);
  };

  const handleDoubleClick = (event: MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();
    emitInlineNodeToggle({ nodeId: data?.objectId ?? String(props.node.id), runtimeNodeId: String(props.node.id), expanded: !currentExpanded });
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
  const isExpanded = currentExpanded;
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

  const handleNodeClick = (event: MouseEvent<HTMLDivElement>) => {
    const target = event.target as Element | null;
    if (target?.closest?.(".microflow-flowgram-node__expand-btn")) {
      return;
    }
    const interactiveTarget = target?.closest?.("button,input,textarea,select,[contenteditable='true'],.microflow-inline-editor,.microflow-inline-field");
    if (!isExpanded && (selected || activated) && !interactiveTarget) {
      stopEditorControlEvent(event);
      emitInlineNodeToggle({ nodeId: resolvedNodeId, runtimeNodeId: String(props.node.id), expanded: true });
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
    ? {
        ["--microflow-parameter-bg" as "--microflow-parameter-bg"]: surfacePalette.background,
        ["--microflow-parameter-border" as "--microflow-parameter-border"]: surfacePalette.borderColor,
        ["--microflow-parameter-accent" as "--microflow-parameter-accent"]: surfacePalette.accentColor,
      }
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
        isExpanded ? "is-expanded" : "",
      ].filter(Boolean).join(" ")}
      draggable={false}
      onMouseDown={handleMouseDown}
      onClick={handleNodeClick}
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
      data-node-category={nodeCategory(data.objectKind)}
      data-node-usage-source={String(usageSourceHighlight)}
      data-node-usage-consumer={String(usageConsumerHighlight)}
      tabIndex={0}
    >
      <div className="microflow-flowgram-node__compact" data-node-tone={tone}>
        {tone === "start" || tone === "end" || tone === "error" || tone === "continue" || tone === "break" ? (
          <div
            className="microflow-event-dot"
            title={data.title}
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
            showRuntimeErrorDot={data.runtimeState === "failed"}
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
        <button
          type="button"
          className="microflow-flowgram-node__expand-btn"
          data-flow-editor-selectable="false"
          aria-label={isExpanded ? "收起节点" : "展开节点"}
          title={isExpanded ? "收起（Esc）" : "展开编辑（双击节点也可）"}
          style={{ pointerEvents: "auto", position: "relative", zIndex: 5 }}
          onPointerDown={stopEditorControlEvent}
          onMouseDown={stopEditorControlEvent}
          onClick={event => {
            stopEditorControlEvent(event);
            emitInlineNodeToggle({ nodeId: resolvedNodeId, runtimeNodeId: String(props.node.id), expanded: !currentExpanded });
          }}
        >
          {isExpanded
            ? <IconTickCircle style={{ verticalAlign: "middle" }} />
            : <IconEdit style={{ verticalAlign: "middle" }} />}
        </button>
      </div>
      {usageConsumerHighlight ? (
        <div className="microflow-node-usage-pill">
          <StaticTag color="green">Usage</StaticTag>
        </div>
      ) : null}
      <div className="microflow-flowgram-node__meta" aria-hidden={!isExpanded}>
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
              emitInlineNodeToggle({ nodeId: resolvedNodeId, runtimeNodeId: String(props.node.id), expanded: true });
            }
          }}
        >
          {runtimeStateLabel ? <Typography.Text type={runtime.error || runtime.failed ? "danger" : "tertiary"} size="small">{runtimeStateLabel}</Typography.Text> : null}
          {typeof runtime.durationMs === "number" ? (
            <Typography.Text type="tertiary" size="small">{runtime.durationMs}ms</Typography.Text>
          ) : null}
          {runtime.selectedBranchLabel ? <Typography.Text type="tertiary" size="small">{runtimeCopy.selected}: {runtime.selectedBranchLabel}</Typography.Text> : null}
          {!runtime.selectedBranchLabel && compactRuntimeOutputs.length > 0 ? (
            <Typography.Text type="tertiary" size="small">
              {compactRuntimeOutputs.join(" · ")}{runtimeOutputOverflowCount > 0 ? ` +${runtimeOutputOverflowCount}` : ""}
            </Typography.Text>
          ) : null}
          {!runtime.selectedBranchLabel && compactRuntimeOutputs.length === 0 ? <Typography.Text type="tertiary" size="small">{runtimeCopy.noOutput}</Typography.Text> : null}
        </button>
      ) : null}
      {editorMounted ? (
        <div
          className={[
            "microflow-flowgram-node__collapsible",
            collapsibleOpen ? "is-collapsible-open" : "",
          ].filter(Boolean).join(" ")}
          onTransitionEnd={handleCollapsibleTransitionEnd}
        >
          <div className="microflow-flowgram-node__collapsible-inner">
            <InlineNodeEditor
              inlineConfig={data.inlineConfig}
              readonly={readonly}
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
          </div>
        </div>
      ) : null}
      {ports.map(port => (
        <FlowGramMicroflowPortRenderer key={port.id} port={port} />
      ))}
    </div>
  );
}

export const FlowGramMicroflowNodeRenderer = memo(FlowGramMicroflowNodeRendererInner);
