import type { CSSProperties, ReactNode } from "react";

export interface AnnotationNodeProps {
  title: string;
  icon?: ReactNode;
  style?: CSSProperties;
}

export function AnnotationNode({ title, icon, style }: AnnotationNodeProps) {
  return (
    <div className="microflow-annotation-compact" title={title} style={style}>
      {icon}
      <span>{title}</span>
    </div>
  );
}
