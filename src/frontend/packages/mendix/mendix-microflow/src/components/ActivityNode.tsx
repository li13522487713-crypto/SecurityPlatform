import type { CSSProperties, ReactNode } from "react";

export type ActivityNodeRuntimeState = "idle" | "running" | "success" | "failed" | "skipped" | "unsupported";

export interface ActivityNodeRuntimeInfo {
  state: ActivityNodeRuntimeState;
  durationMs?: number;
  errorMessage?: string;
}

export interface ActivityNodeProps {
  title: string;
  subtitle: string;
  icon: ReactNode;
  iconStyle?: CSSProperties;
  showRuntimeErrorDot?: boolean;
  runtimeInfo?: ActivityNodeRuntimeInfo;
  ioSummary?: string;
}

function RuntimeBadge({ info }: { info: ActivityNodeRuntimeInfo }) {
  if (info.state === "idle") {
    return null;
  }
  if (info.state === "running") {
    return (
      <span
        className="microflow-activity-runtime-badge microflow-activity-runtime-badge--running"
        aria-label="执行中"
        title="执行中"
      >
        <span className="microflow-activity-runtime-badge__spinner" aria-hidden />
      </span>
    );
  }
  if (info.state === "success") {
    const label = info.durationMs != null ? `✓ ${info.durationMs}ms` : "✓";
    return (
      <span
        className="microflow-activity-runtime-badge microflow-activity-runtime-badge--success"
        aria-label={label}
        title={label}
      >
        {label}
      </span>
    );
  }
  if (info.state === "failed") {
    return (
      <span
        className="microflow-activity-runtime-badge microflow-activity-runtime-badge--failed"
        aria-label={info.errorMessage ?? "失败"}
        title={info.errorMessage ?? "失败"}
      >
        ✕
      </span>
    );
  }
  if (info.state === "skipped") {
    return (
      <span
        className="microflow-activity-runtime-badge microflow-activity-runtime-badge--skipped"
        aria-label="已跳过"
        title="已跳过"
      >
        –
      </span>
    );
  }
  return null;
}

export function ActivityNode({
  title,
  subtitle,
  icon,
  iconStyle,
  showRuntimeErrorDot = false,
  runtimeInfo,
  ioSummary,
}: ActivityNodeProps) {
  return (
    <div className="microflow-activity-compact">
      <div className="microflow-activity-compact__icon" aria-hidden="true" style={iconStyle}>
        {icon}
        {showRuntimeErrorDot && (!runtimeInfo || runtimeInfo.state === "idle") ? (
          <span className="microflow-node-runtime-error-dot" aria-hidden />
        ) : null}
        {runtimeInfo && runtimeInfo.state !== "idle" ? (
          <RuntimeBadge info={runtimeInfo} />
        ) : null}
      </div>
      <div className="microflow-activity-compact__text">
        <div className="microflow-activity-compact__title" title={title}>{title}</div>
        <div className="microflow-activity-compact__subtitle" title={subtitle}>{subtitle}</div>
        {ioSummary ? (
          <div className="microflow-activity-compact__io-summary" title={ioSummary}>{ioSummary}</div>
        ) : null}
      </div>
    </div>
  );
}
