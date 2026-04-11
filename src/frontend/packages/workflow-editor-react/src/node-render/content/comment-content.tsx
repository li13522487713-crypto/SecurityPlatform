interface CommentContentProps {
  content?: string;
}

export function CommentContent(props: CommentContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv wf-node-render-ellipsis">注释: {props.content || "-"}</div>
    </div>
  );
}
