import { useMemo } from "react";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";

interface DragTooltipProps {
  dragging: boolean;
  message?: string;
}

export function DragTooltip(props: DragTooltipProps) {
  const selectedNodeKey = useWorkflowEditorStore((state) => state.selectedNodeKeys[0] ?? "");
  const text = useMemo(() => {
    if (!props.dragging) {
      return "";
    }
    if (props.message) {
      return props.message;
    }
    return selectedNodeKey ? `拖拽中（当前选中: ${selectedNodeKey}）` : "拖拽中";
  }, [props.dragging, props.message, selectedNodeKey]);

  if (!props.dragging) {
    return null;
  }

  return <div className="wf-react-drag-tooltip">{text}</div>;
}
