interface NodeRenderHeaderProps {
  title: string;
  type: string;
}

export function NodeRenderHeader(props: NodeRenderHeaderProps) {
  return (
    <div className="wf-node-render-header">
      <span className="wf-node-render-title">{props.title}</span>
      <span className="wf-node-render-type">{props.type}</span>
    </div>
  );
}
