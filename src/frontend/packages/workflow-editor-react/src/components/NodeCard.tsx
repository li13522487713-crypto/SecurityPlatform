import clsx from "classnames";
import { Tag, Tooltip } from "antd";
import type { PortRuntime } from "../editor/connection-rules";

interface NodeCardProps {
  nodeKey: string;
  title: string;
  color: string;
  iconText: string;
  selected: boolean;
  subtitle: string;
  inputPorts: PortRuntime[];
  outputPorts: PortRuntime[];
  connectableInputPortKeys?: Set<string>;
  connectingFromNodeKey?: string;
  onClick: (event: React.MouseEvent<HTMLButtonElement>) => void;
  onPointerDown: (event: React.PointerEvent<HTMLButtonElement>) => void;
  onPortPointerDown: (event: React.PointerEvent<HTMLSpanElement>, port: PortRuntime) => void;
  executionState?: "idle" | "running" | "success" | "failed" | "skipped";
  executionHint?: string;
}

function renderPort(
  props: NodeCardProps,
  port: PortRuntime,
  side: "left" | "right",
  index: number,
  total: number
) {
  const topPercent = ((index + 1) / (total + 1)) * 100;
  const isTargetCandidate = side === "left" && props.connectableInputPortKeys?.has(port.key);
  const isConnectingSource = side === "right" && props.connectingFromNodeKey === props.nodeKey;
  const classes = clsx(
    "wf-react-port",
    side === "left" ? "wf-react-port-left" : "wf-react-port-right",
    isTargetCandidate && "wf-react-port-connectable",
    isConnectingSource && "wf-react-port-source-active"
  );

  return (
    <Tooltip key={`${side}-${port.key}`} title={`${port.name} (${port.dataType})`}>
      <span
        className={classes}
        style={{ top: `${topPercent}%` }}
        data-wf-port="true"
        data-node-key={props.nodeKey}
        data-port-kind={port.direction}
        data-port-key={port.key}
        data-port-type={port.dataType}
        onPointerDown={(event) => props.onPortPointerDown(event, port)}
      />
    </Tooltip>
  );
}

export function NodeCard(props: NodeCardProps) {
  const statusClass =
    props.executionState === "running"
      ? "wf-react-node-status-running"
      : props.executionState === "success"
      ? "wf-react-node-status-success"
      : props.executionState === "failed"
      ? "wf-react-node-status-failed"
      : props.executionState === "skipped"
      ? "wf-react-node-status-skipped"
      : "wf-react-node-status-idle";

  const statusText =
    props.executionState === "running"
      ? "running"
      : props.executionState === "success"
      ? "success"
      : props.executionState === "failed"
      ? "failed"
      : props.executionState === "skipped"
      ? "skipped"
      : "ready";

  return (
    <button
      type="button"
      className={clsx("wf-react-node", props.selected && "wf-react-node-selected")}
      onClick={(event) => props.onClick(event)}
      onPointerDown={props.onPointerDown}
    >
      <div className="wf-react-node-header" style={{ background: `linear-gradient(${props.color}14 0%, #ffffff 100%)` }}>
        <span className="wf-react-node-icon" style={{ color: props.color, borderColor: `${props.color}66` }}>
          {props.iconText}
        </span>
        <span className="wf-react-node-title">{props.title}</span>
        <Tag color={props.executionState === "failed" ? "error" : props.executionState === "success" ? "success" : props.executionState === "running" ? "processing" : "default"}>
          {statusText}
        </Tag>
      </div>
      <div className={clsx("wf-react-node-status", statusClass)} title={props.executionHint ?? statusText} />
      <div className="wf-react-node-content">
        <div className="wf-react-node-row">
          <span className="wf-react-node-label">input</span>
          <span className="wf-react-node-value">{props.inputPorts.map((port) => port.key).join(", ") || "-"}</span>
        </div>
        <div className="wf-react-node-row">
          <span className="wf-react-node-label">output</span>
          <span className="wf-react-node-value">{props.outputPorts.map((port) => port.key).join(", ") || "-"}</span>
        </div>
        <div className="wf-react-node-row">
          <span className="wf-react-node-label">type</span>
          <span className="wf-react-node-value">{props.subtitle}</span>
        </div>
      </div>
      {props.inputPorts.map((port, index) => renderPort(props, port, "left", index, props.inputPorts.length))}
      {props.outputPorts.map((port, index) => renderPort(props, port, "right", index, props.outputPorts.length))}
    </button>
  );
}
