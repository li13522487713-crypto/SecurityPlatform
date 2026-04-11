interface LoopContentProps {
  mode?: string;
  maxIterations?: number;
  collectionPath?: string;
  condition?: string;
}

export function LoopContent(props: LoopContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">模式: {props.mode || "count"}</div>
      <div className="wf-node-render-kv">最大迭代: {props.maxIterations ?? 10}</div>
      {props.collectionPath ? <div className="wf-node-render-kv">集合: {props.collectionPath}</div> : null}
      {props.condition ? <div className="wf-node-render-kv">条件: {props.condition}</div> : null}
    </div>
  );
}

