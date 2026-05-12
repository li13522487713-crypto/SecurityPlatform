import { Button, Tooltip } from "@douyinfe/semi-ui";
import { IconCopy, IconDelete, IconPlus } from "@douyinfe/semi-icons";
import React from "react";

export interface FlowGramNodeToolbarProps {
  x: number;
  y: number;
  onEdit?: () => void;
  onQuickAdd?: () => void;
  onDelete: () => void;
  onDuplicate: () => void;
}

export const FlowGramNodeToolbar = ({ x, y, onQuickAdd, onDelete, onDuplicate }: FlowGramNodeToolbarProps) => (
  <div
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
    {onQuickAdd ? (
      <Tooltip content="Quick Add">
        <Button icon={<IconPlus />} size="small" onClick={onQuickAdd} />
      </Tooltip>
    ) : null}
    <Tooltip content="Duplicate"><Button icon={<IconCopy />} size="small" onClick={onDuplicate} /></Tooltip>
    <Tooltip content="Delete"><Button icon={<IconDelete />} size="small" type="danger" onClick={onDelete} /></Tooltip>
  </div>
);
