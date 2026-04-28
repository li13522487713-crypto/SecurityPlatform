import { Select, Typography } from "@douyinfe/semi-ui";
import { resolveStoredEntityQualifiedName, searchAssociations, useMicroflowMetadata } from "../../metadata";

const { Text } = Typography;

export function AssociationSelector({
  startEntityQualifiedName,
  value,
  onChange,
  allowedTargetEntityQualifiedNames,
  disabled,
  placeholder = "Select association",
}: {
  startEntityQualifiedName?: string;
  value?: string;
  onChange: (associationQualifiedName?: string) => void;
  allowedTargetEntityQualifiedNames?: string[];
  disabled?: boolean;
  placeholder?: string;
}) {
  const { catalog, loading, error, version } = useMicroflowMetadata();
  const resolvedStart = catalog && startEntityQualifiedName
    ? resolveStoredEntityQualifiedName(catalog, startEntityQualifiedName)
    : startEntityQualifiedName;
  const associations = catalog
    ? searchAssociations(catalog, resolvedStart)
      .filter(association => !allowedTargetEntityQualifiedNames?.length || allowedTargetEntityQualifiedNames.includes(association.targetEntityQualifiedName))
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
      showClear
      disabled={disabled || !resolvedStart}
      value={value}
      key={version}
      style={{ width: "100%" }}
      placeholder={resolvedStart ? placeholder : "Select start entity first"}
      optionList={associations.map(association => ({
        label: `${association.name} (${association.sourceEntityQualifiedName} -> ${association.targetEntityQualifiedName}, ${association.multiplicity})`,
        value: association.qualifiedName,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
