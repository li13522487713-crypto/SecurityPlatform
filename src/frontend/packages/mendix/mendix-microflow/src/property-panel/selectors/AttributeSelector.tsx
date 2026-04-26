import { Select, Typography } from "@douyinfe/semi-ui";
import type { MicroflowDataType } from "../../schema";
import { resolveStoredEntityQualifiedName, searchAttributes, useMicroflowMetadata } from "../../metadata";

const { Text } = Typography;

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
  const { catalog, loading, error, version } = useMicroflowMetadata();
  const resolvedEntity = catalog && entityQualifiedName
    ? resolveStoredEntityQualifiedName(catalog, entityQualifiedName)
    : entityQualifiedName;
  const attributes = catalog
    ? searchAttributes(catalog, resolvedEntity)
      .filter(attribute => !allowedTypes?.length || allowedTypes.includes(attribute.type.kind))
      .filter(attribute => !writableOnly || !attribute.isReadonly)
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
      disabled={disabled || !resolvedEntity}
      value={value}
      key={version}
      style={{ width: "100%" }}
      placeholder={resolvedEntity ? placeholder : "Select entity first"}
      optionList={attributes.map(attribute => ({
        label: `${attribute.name} (${attribute.qualifiedName}) : ${attribute.type.kind}`,
        value: attribute.qualifiedName,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
