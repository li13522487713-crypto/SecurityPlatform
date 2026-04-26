import { Select } from "@douyinfe/semi-ui";
import { searchEntities, useMicroflowMetadata } from "../../metadata";

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
  const catalog = useMicroflowMetadata();
  const entities = searchEntities(catalog)
    .filter(entity => !onlyPersistable || entity.isPersistable)
    .filter(entity => allowSystemEntity || !entity.isSystemEntity)
    .filter(entity => !allowedEntityQualifiedNames?.length || allowedEntityQualifiedNames.includes(entity.qualifiedName));
  return (
    <Select
      filter
      allowClear
      disabled={disabled}
      value={value}
      style={{ width: "100%" }}
      placeholder={placeholder}
      optionList={entities.map(entity => ({
        label: `${entity.name} (${entity.qualifiedName})${entity.isSystemEntity ? " [system]" : ""}${entity.generalization ? " [inherited]" : ""}`,
        value: entity.qualifiedName,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
