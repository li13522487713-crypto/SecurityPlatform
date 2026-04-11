interface ExecuteStatusBarProps {
  status?: "idle" | "running" | "success" | "failed" | "skipped" | "blocked";
}

export function ExecuteStatusBar(props: ExecuteStatusBarProps) {
  const status = props.status ?? "idle";
  return <div className={`wf-node-render-status wf-node-render-status-${status}`} />;
}
