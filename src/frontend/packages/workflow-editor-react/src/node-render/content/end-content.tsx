interface EndContentProps {
  terminateMode?: string;
}

export function EndContent(props: EndContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">类型: 结束节点</div>
      <div className="wf-node-render-kv">结束方式: {props.terminateMode ?? "return"}</div>
    </div>
  );
}
