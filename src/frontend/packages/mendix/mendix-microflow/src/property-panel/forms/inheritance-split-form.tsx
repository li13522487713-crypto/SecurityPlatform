import { Input, Select, TextArea, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowFlow } from "../../schema";
import { getSpecializations, type MicroflowMetadataCatalog } from "../../metadata";
import { createDefaultLine } from "../../schema/utils/flow-utils";
import { createEmptyCaseValue, createInheritanceCaseValue } from "../../schema/utils/case-utils";
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import { collectObjectsRecursive } from "../../schema/utils/object-utils";
import { createStableId } from "../../schema/utils/ids";
import { FieldError } from "../common";
import { EntitySelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { Field } from "../panel-shared";

const { Text } = Typography;

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
  onAddFlow?: (flow: MicroflowFlow) => void;
}) {
  const readonlyDisabledReason = props.readonly ? "Readonly mode cannot edit inheritance split settings." : "";
  if (object.kind !== "inheritanceSplit") {
    return null;
  }
  const outgoing = collectFlowsRecursive(props.schema)
    .filter(flow => flow.kind === "sequence" && flow.originObjectId === object.id);
  const branchedSpecializations = new Set<string>();
  let hasEmptyBranch = false;
  for (const flow of outgoing) {
    for (const caseValue of flow.caseValues) {
      if (caseValue.kind === "inheritance") {
        branchedSpecializations.add(caseValue.entityQualifiedName);
      }
      if (caseValue.kind === "empty" || caseValue.kind === "noCase") {
        hasEmptyBranch = true;
      }
    }
  }
  const expectedSpecializations = object.allowedSpecializations;
  const missingSpecializations = expectedSpecializations.filter(entity => !branchedSpecializations.has(entity));
  const unknownSpecializations = [...branchedSpecializations].filter(entity => !expectedSpecializations.includes(entity));
  const coverageSummary = outgoing.length
    ? outgoing.map(flow => {
      const cases = flow.caseValues.map(caseValue => {
        if (caseValue.kind === "inheritance") {
          return caseValue.entityQualifiedName;
        }
        if (caseValue.kind === "empty" || caseValue.kind === "noCase") {
          return "(empty)";
        }
        if (caseValue.kind === "fallback") {
          return "(fallback)";
        }
        return caseValue.kind;
      });
      return `${flow.id}: ${cases.join(", ") || "pending"}`;
    }).join("\n")
    : "No object type branches yet.";
  const allObjects = collectObjectsRecursive(props.schema.objectCollection);
  const missingTargetHint = allObjects.find(item => item.id !== object.id && item.kind === "endEvent")
    || allObjects.find(item => item.id !== object.id);
  const canAddMissingBranches = !props.readonly && Boolean(missingTargetHint) && (missingSpecializations.length > 0 || !hasEmptyBranch);
  const nextConnectionIndex = { current: outgoing.length };

  const addMissingBranch = (caseValue: ReturnType<typeof createInheritanceCaseValue> | ReturnType<typeof createEmptyCaseValue>) => {
    if (!onAddFlow || !missingTargetHint) {
      return;
    }
    const nextIndex = nextConnectionIndex.current;
    nextConnectionIndex.current += 1;
    const label = caseValue.kind === "inheritance"
      ? caseValue.entityQualifiedName.split(".").at(-1) ?? caseValue.entityQualifiedName
      : caseValue.kind;
    onAddFlow({
      id: createStableId("flow"),
      stableId: createStableId("flow"),
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: object.id,
      destinationObjectId: missingTargetHint.id,
      originConnectionIndex: nextIndex,
      destinationConnectionIndex: 0,
      caseValues: [caseValue],
      isErrorHandler: false,
      line: createDefaultLine(),
      editor: { edgeKind: "objectTypeCondition", label },
    });
  };

  return (
    <>
      <Field label="Decision Type">
        <Input value="Object Type Decision" disabled />
      </Field>
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
      <Field label="Branch Coverage">
        <TextArea value={coverageSummary} disabled autosize />
        <button type="button" disabled={!canAddMissingBranches} onClick={() => {
          if (!missingTargetHint) {
            return;
          }
          if (!hasEmptyBranch) {
            addMissingBranch(createEmptyCaseValue());
          }
          for (const specialization of missingSpecializations) {
            addMissingBranch(createInheritanceCaseValue(specialization));
          }
        }}>
          Add missing branches
        </button>
        {outgoing.length === 0 ? <Text type="warning" size="small">Object Type Decision has no outgoing branches.</Text> : null}
        {missingSpecializations.length > 0 ? (
          <Text type="warning" size="small">Missing specialization branches: {missingSpecializations.join(", ")}</Text>
        ) : null}
        {!hasEmptyBranch ? <Text type="warning" size="small">Missing (empty) branch for unmatched or empty object type.</Text> : null}
        {unknownSpecializations.length > 0 ? (
          <Text type="warning" size="small">Branch targets not in Allowed Specializations: {unknownSpecializations.join(", ")}</Text>
        ) : null}
      </Field>
    </>
  );
}
