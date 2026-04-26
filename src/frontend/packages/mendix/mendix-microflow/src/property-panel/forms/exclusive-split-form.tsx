import { Input, Select } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { FieldError } from "../common";
import { ExpressionEditor } from "../expression";
import { EnumerationSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { expression, Field } from "../panel-shared";

export function ExclusiveSplitForm({ props, object, issues, metadata, variableIndex, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "exclusiveSplit") {
    return null;
  }
  return (
    <>
      <Field label="Decision Type">
        <Select
          value={object.splitCondition.kind}
          disabled={props.readonly}
          style={{ width: "100%" }}
          onChange={kind => {
            const nextKind = String(kind);
            patch({
              ...object,
              splitCondition: nextKind === "rule"
                ? { kind: "rule", ruleQualifiedName: "", parameterMappings: [], resultType: "boolean" }
                : { kind: "expression", expression: expression("true", { kind: "boolean" }), resultType: "boolean" },
            });
          }}
          optionList={[{ label: "expression", value: "expression" }, { label: "rule", value: "rule" }]}
        />
      </Field>
      {object.splitCondition.kind === "expression" ? (
        <>
          <Field label="Result Type">
            <Select
              value={object.splitCondition.resultType}
              disabled={props.readonly}
              style={{ width: "100%" }}
              onChange={resultType => patch({
                ...object,
                splitCondition: {
                  ...object.splitCondition,
                  resultType: String(resultType) as "boolean" | "enumeration",
                  expression: expression(object.splitCondition.expression.raw, String(resultType) === "enumeration"
                    ? { kind: "enumeration", enumerationQualifiedName: object.splitCondition.enumerationQualifiedName ?? "" }
                    : { kind: "boolean" }),
                },
              })}
              optionList={[{ label: "boolean", value: "boolean" }, { label: "enumeration", value: "enumeration" }]}
            />
          </Field>
          {object.splitCondition.resultType === "enumeration" ? (
            <Field label="Enumeration Type">
              <EnumerationSelector value={object.splitCondition.enumerationQualifiedName} disabled={props.readonly} onChange={enumerationQualifiedName => patch({ ...object, splitCondition: { ...object.splitCondition, enumerationQualifiedName } })} />
              <FieldError issues={getIssuesForField(issues, "splitCondition.enumerationQualifiedName")} />
            </Field>
          ) : null}
          <Field label="Split Expression">
            <ExpressionEditor
              value={object.splitCondition.expression}
              schema={props.schema}
              metadata={metadata}
              variableIndex={variableIndex}
              objectId={object.id}
              fieldPath="splitCondition.expression"
              expectedType={object.splitCondition.resultType === "enumeration"
                ? { kind: "enumeration", enumerationQualifiedName: object.splitCondition.enumerationQualifiedName ?? "" }
                : { kind: "boolean" }}
              required
              readonly={props.readonly}
              onChange={nextExpression => patch({
                ...object,
                splitCondition: {
                  ...object.splitCondition,
                  expression: nextExpression
                } as typeof object.splitCondition
              })}
            />
          </Field>
        </>
      ) : (
        <Field label="Rule Reference">
          <Input value={object.splitCondition.ruleQualifiedName} disabled={props.readonly} onChange={ruleQualifiedName => patch({ ...object, splitCondition: { ...object.splitCondition, ruleQualifiedName } })} />
        </Field>
      )}
    </>
  );
}
