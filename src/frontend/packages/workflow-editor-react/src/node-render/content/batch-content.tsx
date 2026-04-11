interface BatchContentProps {
  concurrentSize?: number;
  batchSize?: number;
  inputArrayPath?: string;
  outputKey?: string;
}

export function BatchContent(props: BatchContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">并发: {props.concurrentSize ?? 4}</div>
      <div className="wf-node-render-kv">批大小: {props.batchSize ?? 1}</div>
      <div className="wf-node-render-kv">输入: {props.inputArrayPath || "-"}</div>
      <div className="wf-node-render-kv">输出: {props.outputKey || "batch_results"}</div>
    </div>
  );
}

