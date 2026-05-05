import { Button, Typography } from "@douyinfe/semi-ui";
import { IconChevronDown, IconChevronRight } from "@douyinfe/semi-icons";
import { useEffect, useState } from "react";
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
      return <InlineJsonEditor {...common} />;
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
  const [collapsed, setCollapsed] = useState(Boolean(section.collapsed));

  useEffect(() => {
    setCollapsed(Boolean(section.collapsed));
  }, [section.collapsed]);

  return (
    <section className="microflow-inline-section" data-section-id={section.id}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
        <Text type="tertiary" size="small">{section.title}</Text>
        <Button
          size="small"
          theme="borderless"
          icon={collapsed ? <IconChevronRight /> : <IconChevronDown />}
          onClick={() => setCollapsed(value => !value)}
        >
          {section.kind}
        </Button>
      </div>
      <div
        style={{ display: collapsed ? "none" : "grid", gap: 6 }}
        aria-hidden={collapsed}
      >
        {section.fields.map(field => (
          <div key={field.id} className={["microflow-inline-field-row", field.invalid ? "is-invalid" : ""].join(" ")} style={{ display: "grid", gap: 4 }}>
            <Text size="small" type="tertiary">{field.label}</Text>
            {renderField(field, props.readonly, props.onCommitField)}
            {field.errorMessage ? <Text size="small" type="danger">{field.errorMessage}</Text> : null}
          </div>
        ))}
      </div>
    </section>
  );
}
