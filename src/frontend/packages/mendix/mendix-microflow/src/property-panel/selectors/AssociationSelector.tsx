import { Select } from "@douyinfe/semi-ui";
import { searchAssociations, useMicroflowMetadata } from "../../metadata";

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
  const catalog = useMicroflowMetadata();
  const associations = searchAssociations(catalog, startEntityQualifiedName)
    .filter(association => !allowedTargetEntityQualifiedNames?.length || allowedTargetEntityQualifiedNames.includes(association.targetEntityQualifiedName));
  return (
    <Select
      filter
      allowClear
      disabled={disabled || !startEntityQualifiedName}
      value={value}
      style={{ width: "100%" }}
      placeholder={startEntityQualifiedName ? placeholder : "Select start entity first"}
      optionList={associations.map(association => ({
        label: `${association.name} (${association.sourceEntityQualifiedName} -> ${association.targetEntityQualifiedName}, ${association.multiplicity})`,
        value: association.qualifiedName,
      }))}
      onChange={selected => onChange(selected ? String(selected) : undefined)}
    />
  );
}
