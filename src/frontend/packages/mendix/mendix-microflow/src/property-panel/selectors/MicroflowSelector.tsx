import { Select } from "@douyinfe/semi-ui";
import type { MicroflowDataType } from "../../schema";
import { searchMicroflows, useMicroflowMetadata } from "../../metadata";

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
  const catalog = useMicroflowMetadata();
  const microflows = searchMicroflows(catalog)
    .filter(microflow => !expectedReturnType || microflow.returnType.kind === expectedReturnType.kind);
  return (
    <Select
      filter
      allowClear
      disabled={disabled}
      value={value}
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
