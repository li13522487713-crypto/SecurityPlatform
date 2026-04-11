interface QaContentProps {
  answerType?: string;
  answerPath?: string;
}

export function QaContent(props: QaContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">回答类型: {props.answerType || "free_text"}</div>
      <div className="wf-node-render-kv">输出路径: {props.answerPath || "-"}</div>
    </div>
  );
}

