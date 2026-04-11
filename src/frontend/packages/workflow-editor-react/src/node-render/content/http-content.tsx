interface HttpContentProps {
  method?: string;
  url?: string;
  timeoutMs?: number;
}

export function HttpContent(props: HttpContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">请求: {props.method || "GET"}</div>
      <div className="wf-node-render-kv wf-node-render-ellipsis">{props.url || "-"}</div>
      <div className="wf-node-render-kv">超时: {props.timeoutMs ?? 15000}ms</div>
    </div>
  );
}

