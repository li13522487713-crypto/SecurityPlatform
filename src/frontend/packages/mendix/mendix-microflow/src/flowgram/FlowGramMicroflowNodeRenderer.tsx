import { useState, type MouseEvent, type ReactNode } from "react";

import { Tag, Typography } from "@douyinfe/semi-ui";
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
          <Typography.Text strong title={data.title} style={{ maxWidth: "100%", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {data.title}
          </Typography.Text>
          {data.subtitle ? (
            <Typography.Text type="tertiary" size="small" title={data.subtitle} style={{ maxWidth: "100%", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {data.subtitle}
            </Typography.Text>
          ) : null}
        </div>
      </div>
      <div className="microflow-flowgram-node__meta">
        {data.actionKind ? <StaticTag>{data.actionKind}</StaticTag> : null}
        {data.availability === "beta" ? <StaticTag color="blue">Beta</StaticTag> : null}
        {data.availability === "deprecated" ? <StaticTag color="orange">Deprecated</StaticTag> : null}
        {data.availability === "requiresConnector" ? <StaticTag color="grey">Connector Required</StaticTag> : null}
        {data.availability === "nanoflowOnlyDisabled" ? <StaticTag color="grey">Nanoflow Only</StaticTag> : null}
        {data.runtimeState && data.runtimeState !== "idle" ? (
          <span title={data.runtimeErrorMessage ?? data.runtimeErrorCode}>
            <Tag
              color={
                data.runtimeState === "failed"
                  ? "red"
                  : data.runtimeState === "unsupported"
                    ? "orange"
                    : data.runtimeState === "running"
                      ? "blue"
                      : data.runtimeState === "skipped"
                        ? "grey"
                        : "green"
              }
            >
              {data.runtimeState}
            </Tag>
          </span>
        ) : null}
        {data.validationState === "error" ? <Tag color="red">Error</Tag> : null}
        {data.validationState === "warning" ? <Tag color="orange">Warning</Tag> : null}
      </div>
      {data.objectKind === "loopedActivity" && data.loopSummary ? (
        <div
          className="microflow-flowgram-node__loop-body"
          aria-label="Loop body summary"
          data-microflow-loop-body="true"
          data-microflow-loop-object-id={data.objectId}
        >
          <Typography.Text type="tertiary" size="small">
            {data.loopSummary.childCount === 0 ? "拖入节点配置循环体" : `${data.loopSummary.childCount} body nodes`}
          </Typography.Text>
          {data.loopSource?.kind === "iterableList" ? (
            <div className="microflow-flowgram-node__loop-source">
              <Tag size="small">For Each</Tag>
              <Tag size="small">list {data.listVariableName || "未配置"}</Tag>
              <Tag size="small">iterator {data.iteratorVariableName || "未配置"}</Tag>
              <Tag size="small">{data.currentIndexVariableName ?? "$currentIndex"}</Tag>
            </div>
          ) : (
            <div className="microflow-flowgram-node__loop-source">
              <Tag size="small">While</Tag>
            </div>
          )}
          {data.loopSummary.childCount > 0 ? (
            <div className="microflow-flowgram-node__loop-stats">
              <Tag size="small">Actions {data.loopSummary.actionCount}</Tag>
              <Tag size="small">Events {data.loopSummary.eventCount}</Tag>
              <Tag size="small">Flows {data.loopSummary.flowCount}</Tag>
            </div>
          ) : null}
        </div>
      ) : null}
      {ports.map(port => (
        <FlowGramMicroflowPortRenderer key={port.id} port={port} />
      ))}
    </div>
  );
}
