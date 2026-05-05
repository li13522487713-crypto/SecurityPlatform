import { Tag, Typography } from "@douyinfe/semi-ui";
import type { MicroflowNodeRuntimeInlineState } from "../flowgram/FlowGramMicroflowTypes";

const { Text } = Typography;

export function InlineRuntimePreview(props: { runtime?: MicroflowNodeRuntimeInlineState }) {
  const runtime = props.runtime;
  if (!runtime) {
    return null;
  }
  return (
    <div className="microflow-runtime-inline">
      <div style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
        {runtime.running ? <Tag color="blue">running</Tag> : null}
        {runtime.success ? <Tag color="green">success</Tag> : null}
        {runtime.failed ? <Tag color="red">failed</Tag> : null}
        {runtime.skipped ? <Tag color="grey">skipped</Tag> : null}
        {typeof runtime.durationMs === "number" ? <Tag>{runtime.durationMs}ms</Tag> : null}
      </div>
      {runtime.inputPreview ? <Text size="small">input: {runtime.inputPreview}</Text> : null}
      {runtime.outputPreview ? <Text size="small">output: {runtime.outputPreview}</Text> : null}
      {runtime.selectedBranchLabel ? <Text size="small">selected: {runtime.selectedBranchLabel}</Text> : null}
    </div>
  );
}
