import clsx from "classnames";
import { Tag } from "antd";

interface NodeCardProps {
  nodeKey: string;
  title: string;
  color: string;
  iconText: string;
  selected: boolean;
  subtitle: string;
  onClick: () => void;
  onPointerDown: (event: React.PointerEvent<HTMLButtonElement>) => void;
  onPortPointerDown: (event: React.PointerEvent<HTMLSpanElement>, side: "left" | "right") => void;
}

export function NodeCard(props: NodeCardProps) {
  return (
    <button
      type="button"
      className={clsx("wf-react-node", props.selected && "wf-react-node-selected")}
      onClick={props.onClick}
      onPointerDown={props.onPointerDown}
    >
      <div className="wf-react-node-header" style={{ background: `linear-gradient(${props.color}14 0%, #ffffff 100%)` }}>
        <span className="wf-react-node-icon" style={{ color: props.color, borderColor: `${props.color}66` }}>
          {props.iconText}
        </span>
        <span className="wf-react-node-title">{props.title}</span>
        <Tag color="processing">ready</Tag>
      </div>
      <div className="wf-react-node-content">
        <div className="wf-react-node-row">
          <span className="wf-react-node-label">input</span>
          <span className="wf-react-node-value">{props.subtitle}</span>
        </div>
        <div className="wf-react-node-row">
          <span className="wf-react-node-label">output</span>
          <span className="wf-react-node-value">result</span>
        </div>
      </div>
      <span
        className="wf-react-port wf-react-port-left"
        data-wf-port="true"
        data-node-key={props.nodeKey}
        data-port-kind="input"
        onPointerDown={(event) => props.onPortPointerDown(event, "left")}
      />
      <span
        className="wf-react-port wf-react-port-right"
        data-wf-port="true"
        data-node-key={props.nodeKey}
        data-port-kind="output"
        onPointerDown={(event) => props.onPortPointerDown(event, "right")}
      />
    </button>
  );
}

