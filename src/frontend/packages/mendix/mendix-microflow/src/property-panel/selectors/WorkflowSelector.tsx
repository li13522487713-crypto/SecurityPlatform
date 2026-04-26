import { Select, Typography } from "@douyinfe/semi-ui";
import { searchWorkflows, useMicroflowMetadata } from "../../metadata";

const { Text } = Typography;

export function WorkflowSelector({ value, onChange, contextEntityQualifiedName, disabled, placeholder = "Select workflow" }: {
  value?: string;
  onChange: (workflowId?: string) => void;
  contextEntityQualifiedName?: string;
  disabled?: boolean;
  placeholder?: string;
}) {
  const { catalog, loading, error, version } = useMicroflowMetadata();
  const workflows = catalog
    ? searchWorkflows(catalog).filter(workflow => !contextEntityQualifiedName || workflow.contextEntityQualifiedName === contextEntityQualifiedName)
    : [];
  if (error) {
    return <Text type="danger" size="small">元数据加载失败：{error.message}</Text>;
  }
  if (loading && !catalog) {
    return <Select style={{ width: "100%" }} disabled placeholder="元数据加载中…" />;
  }
  if (!catalog) {
    return <Select style={{ width: "100%" }} disabled placeholder="元数据未加载" />;
  }
  return (
    <Select
      filter
      allowClear
      disabled={disabled}
      value={value}
      key={version}
      style={{ width: "100%" }}
      placeholder={placeholder}
      optionList={workflows.map(workflow => ({ label: `${workflow.qualifiedName}${workflow.contextEntityQualifiedName ? ` (${workflow.contextEntityQualifiedName})` : ""}`, value: workflow.id }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
