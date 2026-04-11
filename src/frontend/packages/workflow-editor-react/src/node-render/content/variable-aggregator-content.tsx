interface VariableAggregatorContentProps {
  outputKey?: string;
  variableKeys?: string[];
  strategy?: string;
  groupSize?: number;
  order?: string;
}

export function VariableAggregatorContent(props: VariableAggregatorContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">变量数: {props.variableKeys?.length ?? 0}</div>
      <div className="wf-node-render-kv">输出: {props.outputKey || "aggregated"}</div>
      {props.strategy ? <div className="wf-node-render-kv">策略: {props.strategy}</div> : null}
      {typeof props.groupSize === "number" ? <div className="wf-node-render-kv">分组: {props.groupSize}</div> : null}
      {props.order ? <div className="wf-node-render-kv">顺序: {props.order}</div> : null}
    </div>
  );
}
