interface IoContentProps {
  mode: "input" | "output";
  pathOrKey?: string;
}

export function IoContent(props: IoContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">类型: {props.mode === "input" ? "输入节点" : "输出节点"}</div>
      <div className="wf-node-render-kv wf-node-render-ellipsis">{props.mode === "input" ? "输入路径" : "输出变量"}: {props.pathOrKey || "-"}</div>
    </div>
  );
}
