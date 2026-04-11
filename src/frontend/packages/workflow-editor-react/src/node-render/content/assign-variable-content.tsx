interface AssignVariableContentProps {
  variableName?: string;
  expression?: string;
}

export function AssignVariableContent(props: AssignVariableContentProps) {
  const normalized = (props.expression ?? "")
    .split(/\r?\n|;/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
  const preview = normalized[0] ?? props.expression ?? "-";

  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">目标变量: {props.variableName || "-"}</div>
      <div className="wf-node-render-kv">赋值条目: {normalized.length > 0 ? normalized.length : "-"}</div>
      <div className="wf-node-render-kv wf-node-render-ellipsis">表达式: {preview}</div>
    </div>
  );
}

