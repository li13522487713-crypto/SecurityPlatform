import type { MicroflowNodeRuntimeInlineState } from "../flowgram/FlowGramMicroflowTypes";
import { InlineQuickFix } from "./InlineQuickFix";

export function InlineErrorBlock(props: {
  runtime?: MicroflowNodeRuntimeInlineState;
  onApplyQuickFix?: (suggestion: NonNullable<NonNullable<MicroflowNodeRuntimeInlineState["error"]>["fixSuggestions"]>[number]) => void;
}) {
  const error = props.runtime?.error;
  if (!error) {
    return null;
  }
  return (
    <div className="microflow-error-inline">
      <div style={{ border: "1px solid #ff4d4f", borderRadius: 6, padding: "8px 10px", background: "#fff2f0", color: "#a8071a", fontSize: 12 }}>
        <strong>{error.code ?? "Runtime Error"}</strong>
        <div>{error.message}</div>
      </div>
      {error.stackPreview ? <pre style={{ margin: 0, whiteSpace: "pre-wrap", fontSize: 11 }}>{error.stackPreview}</pre> : null}
      <InlineQuickFix suggestions={error.fixSuggestions} onApply={props.onApplyQuickFix} />
    </div>
  );
}
