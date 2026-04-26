import { Select } from "@douyinfe/semi-ui";
import type { MicroflowDataType } from "../../schema";
import { searchAttributes, useMicroflowMetadata } from "../../metadata";

export function AttributeSelector({
  entityQualifiedName,
  value,
  onChange,
  allowedTypes,
  writableOnly,
  disabled,
  placeholder = "Select attribute",
}: {
  entityQualifiedName?: string;
  value?: string;
  onChange: (attributeQualifiedName?: string) => void;
  allowedTypes?: MicroflowDataType["kind"][];
  writableOnly?: boolean;
  disabled?: boolean;
  placeholder?: string;
}) {
  const catalog = useMicroflowMetadata();
  const attributes = searchAttributes(catalog, entityQualifiedName)
    .filter(attribute => !allowedTypes?.length || allowedTypes.includes(attribute.type.kind))
    .filter(attribute => !writableOnly || !attribute.isReadonly);
  return (
    <Select
      filter
      allowClear
      disabled={disabled || !entityQualifiedName}
      value={value}
      style={{ width: "100%" }}
      placeholder={entityQualifiedName ? placeholder : "Select entity first"}
      optionList={attributes.map(attribute => ({
        label: `${attribute.name} (${attribute.qualifiedName}) : ${attribute.type.kind}`,
        value: attribute.qualifiedName,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
