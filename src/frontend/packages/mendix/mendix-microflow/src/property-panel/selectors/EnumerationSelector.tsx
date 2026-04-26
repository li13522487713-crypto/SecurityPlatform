import { Select } from "@douyinfe/semi-ui";
import { getEnumerationValues, searchEnumerations, useMicroflowMetadata } from "../../metadata";

export function EnumerationSelector({
  value,
  onChange,
  disabled,
  placeholder = "Select enumeration",
}: {
  value?: string;
  onChange: (enumerationQualifiedName?: string) => void;
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
      optionList={searchEnumerations(catalog).map(enumeration => ({
        label: `${enumeration.name} (${enumeration.qualifiedName}) - ${enumeration.values.length} values`,
        value: enumeration.qualifiedName,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}

export function EnumerationValueSelector({
  enumerationQualifiedName,
  value,
  onChange,
  disabledValues = [],
  includeEmpty,
  disabled,
  placeholder = "Select value",
}: {
  enumerationQualifiedName?: string;
  value?: string;
  onChange: (value?: string) => void;
  disabledValues?: string[];
  includeEmpty?: boolean;
  disabled?: boolean;
  placeholder?: string;
}) {
  const catalog = useMicroflowMetadata();
  const values = getEnumerationValues(catalog, enumerationQualifiedName);
  const options = [
    ...(includeEmpty ? [{ label: "empty", value: "empty", disabled: disabledValues.includes("empty") }] : []),
    ...values.map(item => ({ label: `${item.caption} (${item.key})`, value: item.key, disabled: disabledValues.includes(item.key) })),
  ];
  return (
    <Select
      filter
      allowClear
      disabled={disabled || !enumerationQualifiedName}
      value={value}
      style={{ width: "100%" }}
      placeholder={enumerationQualifiedName ? placeholder : "Select enumeration first"}
      optionList={options}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
