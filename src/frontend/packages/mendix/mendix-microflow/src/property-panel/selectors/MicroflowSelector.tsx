import { Select, Typography } from "@douyinfe/semi-ui";
import type { MicroflowDataType } from "../../schema";
import { searchMicroflows, useMicroflowMetadata } from "../../metadata";

const { Text } = Typography;

function dataTypeKind(type: MicroflowDataType): string {
  return type.kind === "enumeration" ? `enumeration:${type.enumerationQualifiedName}` : type.kind === "object" ? `object:${type.entityQualifiedName}` : type.kind;
}

export function MicroflowSelector({
  value,
  onChange,
  expectedReturnType,
  disabled,
  placeholder = "Select microflow",
}: {
  value?: string;
  onChange: (microflowId?: string) => void;
  expectedReturnType?: MicroflowDataType;
  disabled?: boolean;
  placeholder?: string;
}) {
  const { catalog, loading, error, version } = useMicroflowMetadata();
  const microflows = catalog
    ? searchMicroflows(catalog).filter(microflow => !expectedReturnType || microflow.returnType.kind === expectedReturnType.kind)
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
      disabled={disabled}
      value={value}
      key={version}
      style={{ width: "100%" }}
      placeholder={placeholder}
      optionList={microflows.map(microflow => ({
        label: `${microflow.qualifiedName} (${microflow.parameters.length} params -> ${dataTypeKind(microflow.returnType)})`,
        value: microflow.id,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
