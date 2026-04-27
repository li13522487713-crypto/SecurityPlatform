import { Select, Space } from "@douyinfe/semi-ui";
import type { MicroflowDataType } from "../../schema";
import { EntitySelector } from "./EntitySelector";
import { EnumerationSelector } from "./EnumerationSelector";

const primitiveKinds: MicroflowDataType["kind"][] = ["void", "boolean", "integer", "long", "decimal", "string", "dateTime", "enumeration", "object", "list", "fileDocument", "json"];

function defaultType(kind: MicroflowDataType["kind"]): MicroflowDataType {
  if (kind === "enumeration") {
    return { kind, enumerationQualifiedName: "" };
  }
  if (kind === "object") {
    return { kind, entityQualifiedName: "" };
  }
  if (kind === "list") {
    return { kind, itemType: { kind: "string" } };
  }
  if (kind === "fileDocument") {
    return { kind };
  }
  if (kind === "binary" || kind === "unknown") {
    return { kind };
  }
  return { kind };
}

export function DataTypeSelector({ value, onChange, disabled, allowVoid }: {
  value: MicroflowDataType;
  onChange: (value: MicroflowDataType) => void;
  disabled?: boolean;
  allowVoid?: boolean;
}) {
  const optionKinds = disabled ? primitiveKinds : primitiveKinds.filter(kind => allowVoid !== false || kind !== "void");
  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      <Select
        disabled={disabled}
        value={value.kind}
        style={{ width: "100%" }}
        optionList={optionKinds.map(kind => ({ label: kind, value: kind }))}
        onChange={kind => onChange(defaultType(String(kind) as MicroflowDataType["kind"]))}
      />
      {value.kind === "enumeration" ? (
        <EnumerationSelector value={value.enumerationQualifiedName} disabled={disabled} onChange={enumerationQualifiedName => onChange({ kind: "enumeration", enumerationQualifiedName: enumerationQualifiedName ?? "" })} />
      ) : null}
      {value.kind === "object" ? (
        <EntitySelector value={value.entityQualifiedName} disabled={disabled} onChange={entityQualifiedName => onChange({ kind: "object", entityQualifiedName: entityQualifiedName ?? "" })} />
      ) : null}
      {value.kind === "list" ? (
        <DataTypeSelector value={value.itemType} disabled={disabled} onChange={itemType => onChange({ kind: "list", itemType })} />
      ) : null}
    </Space>
  );
}
