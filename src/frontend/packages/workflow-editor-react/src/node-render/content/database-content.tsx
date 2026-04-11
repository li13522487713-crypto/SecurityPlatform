interface DatabaseContentProps {
  operation: string;
  databaseInfoId?: number;
  outputKey?: string;
}

export function DatabaseContent(props: DatabaseContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">操作: {props.operation}</div>
      <div className="wf-node-render-kv">数据源: {typeof props.databaseInfoId === "number" ? props.databaseInfoId : "-"}</div>
      <div className="wf-node-render-kv">输出: {props.outputKey || "-"}</div>
    </div>
  );
}
