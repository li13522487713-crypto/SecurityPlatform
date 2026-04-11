interface IfContentProps {
  conditions?: unknown[];
}

export function IfContent(props: IfContentProps) {
  const count = Array.isArray(props.conditions) ? props.conditions.length : 0;
  return <div className="wf-node-render-content">If conditions: {count}</div>;
}
