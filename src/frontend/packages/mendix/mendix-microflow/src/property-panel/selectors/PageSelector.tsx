import { Select, Typography } from "@douyinfe/semi-ui";
import { searchPages, useMicroflowMetadata } from "../../metadata";

const { Text } = Typography;

export function PageSelector({ value, onChange, disabled, placeholder = "Select page" }: {
  value?: string;
  onChange: (pageId?: string) => void;
  disabled?: boolean;
  placeholder?: string;
}) {
  const { catalog, loading, error, version } = useMicroflowMetadata();
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
      showClear
      disabled={disabled}
      value={value}
      key={version}
      style={{ width: "100%" }}
      placeholder={placeholder}
      optionList={searchPages(catalog).map(page => ({ label: `${page.qualifiedName} (${page.parameters.length} params)`, value: page.id }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
