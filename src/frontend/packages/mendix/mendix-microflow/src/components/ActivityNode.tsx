import type { CSSProperties, ReactNode } from "react";

export interface ActivityNodeProps {
  title: string;
  subtitle: string;
  icon: ReactNode;
  iconStyle?: CSSProperties;
  showRuntimeErrorDot?: boolean;
}

export function ActivityNode({
  title,
  subtitle,
  icon,
  iconStyle,
  showRuntimeErrorDot = false,
}: ActivityNodeProps) {
  return (
    <div className="microflow-activity-compact">
      <div className="microflow-activity-compact__icon" aria-hidden="true" style={iconStyle}>
        {icon}
        {showRuntimeErrorDot ? <span className="microflow-node-runtime-error-dot" aria-hidden /> : null}
      </div>
      <div className="microflow-activity-compact__text">
        <div className="microflow-activity-compact__title" title={title}>{title}</div>
        <div className="microflow-activity-compact__subtitle" title={subtitle}>{subtitle}</div>
      </div>
    </div>
  );
}
