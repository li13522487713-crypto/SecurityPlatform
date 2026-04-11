interface AssignVariableContentProps {
  variableName?: string;
  expression?: string;
}

export function AssignVariableContent(props: AssignVariableContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">目标变量: {props.variableName || "-"}</div>
      <div className="wf-node-render-kv wf-node-render-ellipsis">表达式: {props.expression || "-"}</div>
    </div>
  );
}

