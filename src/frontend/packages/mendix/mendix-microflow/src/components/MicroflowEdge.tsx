import { useState, type MouseEventHandler, type ReactNode } from "react";

export interface MicroflowEdgeProps {
  className: string;
  flowId: string;
  edgeKind: string;
  label: string;
  warningMissingTarget?: boolean;
  readonly?: boolean;
  onMouseDown?: MouseEventHandler<HTMLDivElement>;
  onClick?: MouseEventHandler<HTMLDivElement>;
  onEdit?: MouseEventHandler<HTMLButtonElement>;
  onDelete?: MouseEventHandler<HTMLButtonElement>;
  editAdornment?: ReactNode;
}

export function MicroflowEdge({
  className,
  flowId,
  edgeKind,
  label,
  warningMissingTarget = false,
  readonly = false,
  onMouseDown,
  onClick,
  onEdit,
  onDelete,
  editAdornment,
}: MicroflowEdgeProps) {
  const [hovered, setHovered] = useState(false);
  return (
    <div
      className="microflow-edge-label"
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
      {!readonly && hovered && onEdit ? (
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
      {!readonly && hovered && onDelete ? (
        <button
          type="button"
          className="microflow-flowgram-line__delete-btn"
          aria-label={`删除连线 ${label}`}
          onClick={event => {
            event.stopPropagation();
            onDelete(event);
          }}
        >
          ×
        </button>
      ) : null}
    </div>
  );
}
