interface StartContentProps {
  variable?: string;
}

export function StartContent(props: StartContentProps) {
  return <div className="wf-node-render-content">Entry variable: {props.variable ?? "-"}</div>;
}
