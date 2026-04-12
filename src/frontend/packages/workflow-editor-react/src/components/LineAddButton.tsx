import { PlusOutlined } from "@ant-design/icons";

interface LineAddButtonProps {
  x: number;
  y: number;
  visible: boolean;
  onClick: () => void;
}

export function LineAddButton(props: LineAddButtonProps) {
  if (!props.visible) {
    return null;
  }
  return (
    <button
      type="button"
      className="wf-react-line-add-btn"
      style={{ left: props.x, top: props.y }}
      onClick={props.onClick}
      aria-label="在线上插入节点"
    >
      <PlusOutlined />
    </button>
  );
}
