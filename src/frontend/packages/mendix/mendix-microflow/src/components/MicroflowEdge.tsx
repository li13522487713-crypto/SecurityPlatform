import { useState, type MouseEventHandler, type ReactNode } from "react";

export interface MicroflowEdgeProps {
  className: string;
  flowId: string;
  edgeKind: string;
  label: string;
  /** 当前连线是否被选中（来自 schema.editor.selection.flowId） */
  selected?: boolean;
  warningMissingTarget?: boolean;
  readonly?: boolean;
  onMouseDown?: MouseEventHandler<HTMLDivElement>;
  onClick?: MouseEventHandler<HTMLDivElement>;
  onEdit?: MouseEventHandler<HTMLButtonElement>;
  editAdornment?: ReactNode;
}

export function MicroflowEdge({
  className,
  flowId,
  edgeKind,
  label,
  selected = false,
  warningMissingTarget = false,
  readonly = false,
  onMouseDown,
  onClick,
  onEdit,
  editAdornment,
}: MicroflowEdgeProps) {
  const [hovered, setHovered] = useState(false);
  const showActions = (hovered || selected) && !readonly;
  return (
    <div
      className={["microflow-edge-label", selected ? "is-selected" : ""].filter(Boolean).join(" ")}
      data-testid="microflow-flowgram-line-label"
      data-flow-id={flowId}
      data-edge-kind={edgeKind}
      onMouseDown={onMouseDown}
      onClick={onClick}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      title={warningMissingTarget ? "缺少目标节点" : label}
    >
      <span className={className}>
        {label}
        {warningMissingTarget ? <span aria-hidden="true" className="microflow-branch-label__warning-dot" /> : null}
      </span>
      {showActions && onEdit ? (
        <button
          type="button"
          className="microflow-branch-label__edit-btn"
          aria-label={`编辑分支标签 ${label}`}
          onClick={event => {
            event.stopPropagation();
            onEdit(event);
          }}
        >
          {editAdornment}
        </button>
      ) : null}
    </div>
  );
}
