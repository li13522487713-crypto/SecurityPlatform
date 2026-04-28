import { Button, Select, Space, Typography } from "@douyinfe/semi-ui";
import { resolveStoredEntityQualifiedName, searchEntities, useMicroflowMetadata } from "../../metadata";

const { Text } = Typography;

export function EntitySelector({
  value,
  onChange,
  onlyPersistable,
  allowSystemEntity = true,
  allowedEntityQualifiedNames,
  disabled,
  placeholder = "Select entity",
}: {
  value?: string;
  onChange: (qualifiedName?: string) => void;
  onlyPersistable?: boolean;
  allowSystemEntity?: boolean;
  allowedEntityQualifiedNames?: string[];
  disabled?: boolean;
  placeholder?: string;
}) {
  const { catalog, loading, error, reload, version } = useMicroflowMetadata();
  const mockCatalog = catalog?.version?.startsWith("mock") ?? false;
  const resolvedValue = catalog && value ? resolveStoredEntityQualifiedName(catalog, value) : value;
  const entities = catalog
    ? searchEntities(catalog)
      .filter(entity => !onlyPersistable || entity.isPersistable)
      .filter(entity => allowSystemEntity || !entity.isSystemEntity)
      .filter(entity => !allowedEntityQualifiedNames?.length || allowedEntityQualifiedNames.includes(entity.qualifiedName))
    : [];
  if (error) {
    return (
      <Space vertical align="start" spacing={4} style={{ width: "100%" }}>
        <Text type="danger" size="small">元数据加载失败：{error.message}</Text>
        <Button size="small" onClick={() => void reload()}>Retry metadata</Button>
      </Space>
    );
  }
  if (loading && !catalog) {
    return <Select style={{ width: "100%" }} disabled placeholder="元数据加载中…" />;
  }
  if (!catalog || entities.length === 0) {
    return (
      <Select
        style={{ width: "100%" }}
        disabled={disabled}
        placeholder={!catalog ? "元数据未加载" : "No entities available"}
      />
    );
  }
  if (mockCatalog) {
    return (
      <Select
        style={{ width: "100%" }}
        disabled
        value={value}
        placeholder="真实 Domain Model metadata 未接入"
      />
    );
  }
  return (
    <Select
      filter
      showClear
      disabled={disabled}
      value={resolvedValue}
      key={version}
      style={{ width: "100%" }}
      placeholder={placeholder}
      optionList={entities.map(entity => ({
        label: `${entity.name} (${entity.qualifiedName})${entity.isSystemEntity ? " [system]" : ""}${entity.generalization ? " [inherited]" : ""}${entity.attributes.length ? ` - ${entity.attributes.map(attribute => attribute.name).join(", ")}` : ""}`,
        value: entity.qualifiedName,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
