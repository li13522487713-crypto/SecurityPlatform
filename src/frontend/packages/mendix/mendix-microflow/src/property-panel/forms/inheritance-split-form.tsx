import { Input, Select } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { getSpecializations, type MicroflowMetadataCatalog } from "../../metadata";
import { FieldError } from "../common";
import { EntitySelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { Field } from "../panel-shared";

export function InheritanceSplitForm({ props, object, issues, metadata, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  metadata: MicroflowMetadataCatalog;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "inheritanceSplit") {
    return null;
  }
  return (
    <>
      <Field label="Input Object Variable">
        <Input value={object.inputObjectVariableName} disabled={props.readonly} onChange={inputObjectVariableName => patch({ ...object, inputObjectVariableName })} />
      </Field>
      <Field label="Generalized Entity">
        <EntitySelector value={object.generalizedEntityQualifiedName} disabled={props.readonly} onChange={generalizedEntityQualifiedName => {
          const specializations = getSpecializations(metadata, generalizedEntityQualifiedName);
          patch({
            ...object,
            generalizedEntityQualifiedName: generalizedEntityQualifiedName ?? "",
            allowedSpecializations: specializations,
            entity: { generalizedEntityQualifiedName: generalizedEntityQualifiedName ?? "", allowedSpecializations: specializations },
          });
        }} />
        <FieldError issues={getIssuesForField(issues, "generalizedEntityQualifiedName")} />
      </Field>
      <Field label="Allowed Specializations">
        <Select
          multiple
          filter
          value={object.allowedSpecializations}
          disabled={props.readonly || !object.generalizedEntityQualifiedName}
          style={{ width: "100%" }}
          optionList={getSpecializations(metadata, object.generalizedEntityQualifiedName).map(value => ({ label: value, value }))}
          onChange={selected => {
            const allowedSpecializations = Array.isArray(selected) ? selected.map(String) : [];
            patch({ ...object, allowedSpecializations, entity: { ...object.entity, allowedSpecializations } });
          }}
        />
        <FieldError issues={getIssuesForField(issues, "allowedSpecializations")} />
      </Field>
    </>
  );
}
