import { useMemo } from "react";

import { Tag, Typography } from "@douyinfe/semi-ui";
import {
  FlowNodeFormData,
  type FormModelV2,
  type WorkflowNodeRenderProps,
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
  const { selected, ports, selectNode } = useNodeRender();
  const data = readNodeData(props);
  const tone = nodeTone(data.objectKind);
  const validationTag = useMemo(() => {
    if (data.validationState === "error") {
      return <Tag color="red">Error</Tag>;
    }
    if (data.validationState === "warning") {
      return <Tag color="orange">Warning</Tag>;
    }
    return null;
  }, [data.validationState]);

  return (
    <div
      className={`microflow-flowgram-node microflow-flowgram-node--${tone}${selected ? " is-selected" : ""}${data.disabled ? " is-disabled" : ""}`}
      onClick={event => selectNode(event)}
      data-microflow-object-id={data.objectId}
    >
      <div className="microflow-flowgram-node__header">
        <span className="microflow-flowgram-node__icon" />
        <div className="microflow-flowgram-node__text">
          <Typography.Text strong ellipsis={{ showTooltip: true }}>
            {data.title}
          </Typography.Text>
          {data.subtitle ? (
            <Typography.Text type="tertiary" size="small" ellipsis={{ showTooltip: true }}>
              {data.subtitle}
            </Typography.Text>
          ) : null}
        </div>
      </div>
      <div className="microflow-flowgram-node__meta">
        {data.actionKind ? <Tag size="small">{data.actionKind}</Tag> : null}
        {data.runtimeState && data.runtimeState !== "idle" ? <Tag color="green">{data.runtimeState}</Tag> : null}
        {validationTag}
      </div>
      {data.objectKind === "loopedActivity" && data.loopSummary ? (
        <div className="microflow-flowgram-node__loop-body" aria-label="Loop body summary">
          <Typography.Text type="tertiary" size="small">
            {data.loopSummary.childCount === 0 ? "Empty body" : `${data.loopSummary.childCount} body nodes`}
          </Typography.Text>
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
