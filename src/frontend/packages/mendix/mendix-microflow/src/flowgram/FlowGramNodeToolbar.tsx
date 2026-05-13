import { useState } from "react";
import { Button, Tooltip } from "@douyinfe/semi-ui";
import { IconCopy, IconDelete, IconPlus } from "@douyinfe/semi-icons";
import type { MicroflowNodeRegistryItem } from "../node-registry";
import { MicroflowQuickConnectPicker } from "./MicroflowQuickConnectPicker";
import { useMicroflowCanvasActions } from "./MicroflowCanvasActionsContext";

export interface FlowGramNodeToolbarProps {
  x: number;
  y: number;
  nodeId: string;
  onEdit?: () => void;
  onQuickAdd?: () => void;
  onQuickConnect?: (item: MicroflowNodeRegistryItem) => void;
  onDuplicate: () => void;
}

export function FlowGramNodeToolbar({ x, y, nodeId, onQuickAdd, onQuickConnect, onDuplicate }: FlowGramNodeToolbarProps) {
  const [pickerVisible, setPickerVisible] = useState(false);
  const actions = useMicroflowCanvasActions();

  return (
    <div
      data-flow-editor-selectable="false"
      onMouseDown={e => e.stopPropagation()}
      onPointerDown={e => e.stopPropagation()}
      style={{
        position: "absolute",
        left: x,
        top: y,
        zIndex: 100,
        padding: "4px",
        background: "var(--semi-color-bg-1)",
        borderRadius: "4px",
        boxShadow: "0 2px 8px rgba(0,0,0,0.15)",
        display: "flex",
        gap: "4px",
      }}
    >
      {onQuickConnect ? (
        <MicroflowQuickConnectPicker
          visible={pickerVisible}
          onVisibleChange={setPickerVisible}
          onPick={onQuickConnect}
        >
          <Tooltip content="添加并连接下一个节点" position="top">
            <Button
              icon={<IconPlus />}
              size="small"
              type="primary"
              theme="light"
              onClick={() => setPickerVisible(v => !v)}
            />
          </Tooltip>
        </MicroflowQuickConnectPicker>
      ) : onQuickAdd ? (
        <Tooltip content="Quick Add">
          <Button icon={<IconPlus />} size="small" onClick={onQuickAdd} />
        </Tooltip>
      ) : null}
      <Tooltip content="Duplicate"><Button icon={<IconCopy />} size="small" onClick={onDuplicate} /></Tooltip>
      <Tooltip content="Delete"><Button icon={<IconDelete />} size="small" type="danger" onClick={() => actions.deleteNode(nodeId)} /></Tooltip>
    </div>
  );
}
