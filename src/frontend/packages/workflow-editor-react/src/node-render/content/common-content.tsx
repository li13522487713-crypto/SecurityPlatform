interface CommonContentProps {
  summary?: string;
}

export function CommonContent(props: CommonContentProps) {
  return <div className="wf-node-render-content">{props.summary ?? "No summary"}</div>;
}
