import { Select } from "@douyinfe/semi-ui";
import { searchWorkflows, useMicroflowMetadata } from "../../metadata";

export function WorkflowSelector({ value, onChange, contextEntityQualifiedName, disabled, placeholder = "Select workflow" }: {
  value?: string;
  onChange: (workflowId?: string) => void;
  contextEntityQualifiedName?: string;
  disabled?: boolean;
  placeholder?: string;
}) {
  const catalog = useMicroflowMetadata();
  const workflows = searchWorkflows(catalog).filter(workflow => !contextEntityQualifiedName || workflow.contextEntityQualifiedName === contextEntityQualifiedName);
  return (
    <Select
      filter
      allowClear
      disabled={disabled}
      value={value}
      style={{ width: "100%" }}
      placeholder={placeholder}
      optionList={workflows.map(workflow => ({ label: `${workflow.qualifiedName}${workflow.contextEntityQualifiedName ? ` (${workflow.contextEntityQualifiedName})` : ""}`, value: workflow.id }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
