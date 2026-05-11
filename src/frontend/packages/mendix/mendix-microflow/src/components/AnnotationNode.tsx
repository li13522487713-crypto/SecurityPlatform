import type { ReactNode } from "react";

export interface AnnotationNodeProps {
  title: string;
  icon?: ReactNode;
}

export function AnnotationNode({ title, icon }: AnnotationNodeProps) {
  return (
    <div className="microflow-annotation-compact" title={title}>
      {icon}
      <span>{title}</span>
    </div>
  );
}
