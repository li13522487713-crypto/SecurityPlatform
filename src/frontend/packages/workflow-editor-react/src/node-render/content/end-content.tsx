interface EndContentProps {
  terminateMode?: string;
}

export function EndContent(props: EndContentProps) {
  return <div className="wf-node-render-content">Terminate: {props.terminateMode ?? "return"}</div>;
}
