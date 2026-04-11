interface SubWorkflowContentProps {
  workflowId?: string;
  maxDepth?: number;
  outputKey?: string;
}

export function SubWorkflowContent(props: SubWorkflowContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">子流程: {props.workflowId || "-"}</div>
      <div className="wf-node-render-kv">最大深度: {props.maxDepth ?? 5}</div>
      <div className="wf-node-render-kv">输出: {props.outputKey || "sub_workflow_output"}</div>
    </div>
  );
}

