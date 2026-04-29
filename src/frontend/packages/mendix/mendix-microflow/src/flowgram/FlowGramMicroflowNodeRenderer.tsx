import { useRef } from "react";

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
import "./styles/flowgram-microflow-node.css";

function readNodeData(props: WorkflowNodeRenderProps): FlowGramMicroflowNodeData {
  const formData = props.node.getData(FlowNodeFormData);
  const formModel = formData?.getFormModel<FormModelV2>();
  return formModel?.getFormItemValueByPath("/") as FlowGramMicroflowNodeData;
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

export function FlowGramMicroflowNodeRenderer(props: WorkflowNodeRenderProps) {
  const { selected, ports, selectNode, nodeRef, startDrag, onFocus, onBlur } = useNodeRender();
  const readonly = usePlaygroundReadonlyState();
  const draggingRef = useRef(false);
  const data = readNodeData(props);
  const tone = nodeTone(data.objectKind);

  return (
    <div
      ref={nodeRef}
      className={[
        "microflow-flowgram-node",
        `microflow-flowgram-node--${tone}`,
        selected ? "is-selected" : "",
        data.disabled ? "is-disabled" : "",
        data.validationState !== "valid" ? `is-${data.validationState}` : "",
        data.runtimeState && data.runtimeState !== "idle" ? `is-runtime-${data.runtimeState}` : "",
      ].filter(Boolean).join(" ")}
      draggable={!readonly}
      onDragStart={event => {
        if (readonly) {
          event.preventDefault();
          return;
        }
        draggingRef.current = true;
        startDrag(event);
      }}
      onDragEnd={() => {
        window.setTimeout(() => {
          draggingRef.current = false;
        }, 0);
      }}
      onClick={event => {
        if (draggingRef.current) {
          event.preventDefault();
          event.stopPropagation();
          return;
        }
        selectNode(event);
      }}
      onFocus={onFocus}
      onBlur={onBlur}
      data-testid={`microflow-node-${data.objectId}`}
      data-microflow-object-id={data.objectId}
      data-microflow-collection-id={data.collectionId}
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
        {data.actionKind ? <Tag size="small">{data.actionKind}</Tag> : null}
        {data.availability === "beta" ? <Tag size="small" color="blue">Beta</Tag> : null}
        {data.availability === "deprecated" ? <Tag size="small" color="orange">Deprecated</Tag> : null}
        {data.availability === "requiresConnector" ? <Tag size="small" color="grey">Connector Required</Tag> : null}
        {data.availability === "nanoflowOnlyDisabled" ? <Tag size="small" color="grey">Nanoflow Only</Tag> : null}
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
