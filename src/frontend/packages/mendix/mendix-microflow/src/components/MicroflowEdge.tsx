import type { MouseEventHandler, ReactNode } from "react";

export interface MicroflowEdgeProps {
  className: string;
  flowId: string;
  edgeKind: string;
  label: string;
  warningMissingTarget?: boolean;
  readonly?: boolean;
  onMouseDown?: MouseEventHandler<HTMLButtonElement>;
  onClick?: MouseEventHandler<HTMLButtonElement>;
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
  editAdornment,
}: MicroflowEdgeProps) {
  return (
    <button
      type="button"
      className={className}
      data-testid="microflow-flowgram-line-label"
      data-flow-id={flowId}
      data-edge-kind={edgeKind}
      onMouseDown={onMouseDown}
      onClick={onClick}
      title={warningMissingTarget ? "缺少目标节点" : label}
    >
      {label}
      {!readonly ? editAdornment : null}
      {warningMissingTarget ? <span aria-hidden="true" className="microflow-branch-label__warning-dot" /> : null}
    </button>
  );
}
