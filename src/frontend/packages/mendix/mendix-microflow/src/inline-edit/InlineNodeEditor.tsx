import { Typography } from "@douyinfe/semi-ui";
import type { MicroflowInlineEditableField, MicroflowNodeInlineConfig, MicroflowNodeRuntimeInlineState } from "../flowgram/FlowGramMicroflowTypes";
import { InlineErrorBlock } from "./InlineErrorBlock";
import { InlineRuntimePreview } from "./InlineRuntimePreview";
import { InlineSection } from "./InlineSection";

const { Text } = Typography;

export function InlineNodeEditor(props: {
  inlineConfig?: MicroflowNodeInlineConfig;
  readonly?: boolean;
  onCommitField: (field: MicroflowInlineEditableField, value: string) => void;
  onApplyQuickFix?: (suggestion: NonNullable<NonNullable<MicroflowNodeRuntimeInlineState["error"]>["fixSuggestions"]>[number]) => void;
}) {
  const config = props.inlineConfig;
  if (!config) {
    return null;
  }
  return (
    <div className="microflow-inline-editor">
      {config.sections.map(section => (
        <InlineSection key={section.id} section={section} readonly={props.readonly} onCommitField={props.onCommitField} />
      ))}
      <InlineRuntimePreview runtime={config.runtime} />
      <InlineErrorBlock runtime={config.runtime} onApplyQuickFix={props.onApplyQuickFix} />
      {config.summaryLines.length === 0 ? <Text type="tertiary" size="small">无可编辑字段</Text> : null}
    </div>
  );
}
