interface LlmContentProps {
  provider?: string;
  model?: string;
}

export function LlmContent(props: LlmContentProps) {
  return <div className="wf-node-render-content">{props.provider ?? "-"} / {props.model ?? "-"}</div>;
}
