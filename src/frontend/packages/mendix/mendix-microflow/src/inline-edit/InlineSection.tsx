import { Typography } from "@douyinfe/semi-ui";
import type { MicroflowInlineEditableField, MicroflowInlineSection as InlineSectionType } from "../flowgram/FlowGramMicroflowTypes";
import { InlineAssignmentEditor } from "./InlineAssignmentEditor";
import { InlineBranchEditor } from "./InlineBranchEditor";
import { InlineConditionEditor } from "./InlineConditionEditor";
import { InlineEditableSelect } from "./InlineEditableSelect";
import { InlineEditableText } from "./InlineEditableText";
import { InlineExpressionField } from "./InlineExpressionField";
import { InlineHttpEditor } from "./InlineHttpEditor";
import { InlineJsonEditor } from "./InlineJsonEditor";
import { InlineMappingEditor } from "./InlineMappingEditor";
import { InlineVariableField } from "./InlineVariableField";

const { Text } = Typography;

function renderField(field: MicroflowInlineEditableField, readonly: boolean | undefined, onCommit: (field: MicroflowInlineEditableField, value: string) => void) {
  const common = {
    value: field.value,
    placeholder: field.placeholder,
    readonly: readonly || field.readonly,
    invalid: field.invalid,
    onCommit: (value: string) => onCommit(field, value),
  };
  switch (field.editType) {
    case "select":
      return <InlineEditableSelect value={field.value} readonly={readonly || field.readonly} options={field.options} onCommit={(value) => onCommit(field, value)} />;
    case "variable":
      return <InlineVariableField {...common} options={field.options} />;
    case "expression":
      return <InlineExpressionField {...common} options={field.options} />;
    case "condition":
      return <InlineConditionEditor {...common} options={field.options} />;
    case "assignment":
      return <InlineAssignmentEditor {...common} options={field.options} />;
    case "http":
      return <InlineHttpEditor {...common} options={field.options} />;
    case "branch":
      return <InlineBranchEditor {...common} />;
    case "json":
      return <InlineJsonEditor {...common} options={field.options} />;
    case "mapping":
      return <InlineMappingEditor {...common} options={field.options} />;
    case "approval":
    case "loop":
    case "text":
    default:
      return <InlineEditableText {...common} />;
  }
}

export function InlineSection(props: {
  section: InlineSectionType;
  readonly?: boolean;
  onCommitField: (field: MicroflowInlineEditableField, value: string) => void;
}) {
  const section = props.section;
  const visibleRows = section.maxVisibleRows && section.maxVisibleRows > 0
    ? section.fields.slice(0, section.maxVisibleRows)
    : section.fields;
  const hiddenCount = Math.max(0, section.fields.length - visibleRows.length);

  return (
    <section className="microflow-inline-section microflow-mini-section" data-section-id={section.id}>
      <div className="microflow-mini-section__title">
        <Text type="tertiary" size="small">{section.title}</Text>
      </div>
      <div className="microflow-mini-section__rows">
        {visibleRows.map(field => (
          <div key={field.id} className={["microflow-mini-field", field.invalid ? "is-invalid" : ""].join(" ")}>
            <Text size="small" type="tertiary" className="microflow-mini-field__label">{field.label}</Text>
            <div className="microflow-mini-field__editor">{renderField(field, props.readonly, props.onCommitField)}</div>
            {field.errorMessage ? <Text size="small" type="danger">{field.errorMessage}</Text> : null}
          </div>
        ))}
        {hiddenCount > 0 ? <Text type="tertiary" size="small" className="microflow-mini-field__more">+{hiddenCount} more</Text> : null}
      </div>
    </section>
  );
}
