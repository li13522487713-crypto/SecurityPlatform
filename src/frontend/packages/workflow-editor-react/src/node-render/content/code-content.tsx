interface CodeContentProps {
  language?: string;
}

export function CodeContent(props: CodeContentProps) {
  return <div className="wf-node-render-content">Language: {props.language ?? "javascript"}</div>;
}
