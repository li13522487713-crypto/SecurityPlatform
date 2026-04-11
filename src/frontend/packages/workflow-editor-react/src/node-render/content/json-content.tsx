interface JsonContentProps {
  mode: "serialize" | "deserialize";
  outputKey?: string;
  variableKeys?: string[];
  inputVariable?: string;
}

export function JsonContent(props: JsonContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">模式: {props.mode === "serialize" ? "序列化" : "反序列化"}</div>
      {props.mode === "serialize" ? (
        <>
          <div className="wf-node-render-kv">变量数: {props.variableKeys?.length ?? 0}</div>
          <div className="wf-node-render-kv">输出: {props.outputKey || "json_output"}</div>
        </>
      ) : (
        <div className="wf-node-render-kv wf-node-render-ellipsis">输入: {props.inputVariable || "-"}</div>
      )}
    </div>
  );
}
