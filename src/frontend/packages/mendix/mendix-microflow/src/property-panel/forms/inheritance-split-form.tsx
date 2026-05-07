import { Input, Select, Tooltip } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { getSpecializations, type MicroflowMetadataCatalog } from "../../metadata";
import { FieldError } from "../common";
import { EntitySelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { Field } from "../panel-shared";

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

export function InheritanceSplitForm({ props, object, issues, metadata, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  metadata: MicroflowMetadataCatalog;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = props.readonly ? "Readonly mode cannot edit inheritance split settings." : "";
  if (object.kind !== "inheritanceSplit") {
    return null;
  }
  return (
    <>
      <Field label="Input Object Variable">
        {withDisabledReason(
          readonlyDisabledReason,
          "Input object variable",
          <Input value={object.inputObjectVariableName} disabled={props.readonly} onChange={inputObjectVariableName => patch({ ...object, inputObjectVariableName })} />
        )}
      </Field>
      <Field label="Generalized Entity">
        {withDisabledReason(
          readonlyDisabledReason,
          "Generalized entity",
          <EntitySelector value={object.generalizedEntityQualifiedName} disabled={props.readonly} onChange={generalizedEntityQualifiedName => {
            const specializations = getSpecializations(metadata, generalizedEntityQualifiedName);
            patch({
              ...object,
              generalizedEntityQualifiedName: generalizedEntityQualifiedName ?? "",
              allowedSpecializations: specializations,
              entity: { generalizedEntityQualifiedName: generalizedEntityQualifiedName ?? "", allowedSpecializations: specializations },
            });
          }} />
        )}
        <FieldError issues={getIssuesForField(issues, "generalizedEntityQualifiedName")} />
      </Field>
      <Field label="Allowed Specializations">
        {withDisabledReason(
          props.readonly ? "Readonly mode cannot edit inheritance split settings." : (!object.generalizedEntityQualifiedName ? "Select generalized entity first." : ""),
          "Allowed specializations",
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
        )}
        <FieldError issues={getIssuesForField(issues, "allowedSpecializations")} />
      </Field>
    </>
  );
}
