import { Select } from "@douyinfe/semi-ui";
import { searchPages, useMicroflowMetadata } from "../../metadata";

export function PageSelector({ value, onChange, disabled, placeholder = "Select page" }: {
  value?: string;
  onChange: (pageId?: string) => void;
  disabled?: boolean;
  placeholder?: string;
}) {
  const catalog = useMicroflowMetadata();
  return (
    <Select
      filter
      allowClear
      disabled={disabled}
      value={value}
      style={{ width: "100%" }}
      placeholder={placeholder}
      optionList={searchPages(catalog).map(page => ({ label: `${page.qualifiedName} (${page.parameters.length} params)`, value: page.id }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
