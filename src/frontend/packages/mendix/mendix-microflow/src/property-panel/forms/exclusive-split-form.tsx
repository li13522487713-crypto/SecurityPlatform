import { Input, Select, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import { FieldError } from "../common";
import { ExpressionEditor } from "../expression";
import { EnumerationSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text } = Typography;

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
              onChange={resultType => {
                if (object.splitCondition.kind !== "expression") {
                  return;
                }
                const nextResultType = String(resultType) as "boolean" | "enumeration";
                patch({
                  ...object,
                  splitCondition: {
                    kind: "expression",
                    resultType: nextResultType,
                    enumerationQualifiedName: nextResultType === "enumeration" ? object.splitCondition.enumerationQualifiedName ?? "" : undefined,
                    expression: expression(object.splitCondition.expression.raw, nextResultType === "enumeration"
                      ? { kind: "enumeration", enumerationQualifiedName: object.splitCondition.enumerationQualifiedName ?? "" }
                      : { kind: "boolean" }),
                  },
                });
              }}
              optionList={[{ label: "boolean", value: "boolean" }, { label: "enumeration", value: "enumeration" }]}
            />
          </Field>
          {object.splitCondition.resultType === "enumeration" ? (
            <Field label="Enumeration Type">
              <EnumerationSelector
                value={object.splitCondition.enumerationQualifiedName}
                disabled={props.readonly}
                onChange={enumerationQualifiedName => {
                  if (object.splitCondition.kind !== "expression") {
                    return;
                  }
                  patch({ ...object, splitCondition: { ...object.splitCondition, enumerationQualifiedName } });
                }}
              />
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
            {!object.splitCondition.expression.raw.trim() ? <Text type="warning" size="small">Decision expression 为空，true/false 分支仍会保存，但条件需要后续配置。</Text> : null}
          </Field>
          <Field label="Branch Summary">
            <TextArea
              disabled
              autosize
              value={collectFlowsRecursive(props.schema)
                .filter(flow => flow.originObjectId === object.id)
                .map(flow => `${flow.id}: ${flow.kind === "sequence" ? flow.caseValues.map(caseValue => caseValue.kind === "boolean" ? String(caseValue.value) : caseValue.kind === "enumeration" ? caseValue.value : caseValue.kind).join(", ") || "pending" : flow.kind}`)
                .join("\n") || (object.splitCondition.resultType === "boolean" ? "Expected: true / false branches" : "Expected: enumeration value branches")}
            />
          </Field>
        </>
      ) : (
        <Field label="Rule Reference">
          <Input
            value={object.splitCondition.ruleQualifiedName}
            disabled={props.readonly}
            onChange={ruleQualifiedName => {
              if (object.splitCondition.kind !== "rule") {
                return;
              }
              patch({ ...object, splitCondition: { ...object.splitCondition, ruleQualifiedName } });
            }}
          />
        </Field>
      )}
    </>
  );
}
