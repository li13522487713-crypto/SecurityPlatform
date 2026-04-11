import clsx from "classnames";
import { Tag } from "antd";

interface NodeCardProps {
  title: string;
  color: string;
  iconText: string;
  selected: boolean;
  subtitle: string;
  onClick: () => void;
}

export function NodeCard(props: NodeCardProps) {
  return (
    <button type="button" className={clsx("wf-react-node", props.selected && "wf-react-node-selected")} onClick={props.onClick}>
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
      <span className="wf-react-port wf-react-port-left" />
      <span className="wf-react-port wf-react-port-right" />
    </button>
  );
}

