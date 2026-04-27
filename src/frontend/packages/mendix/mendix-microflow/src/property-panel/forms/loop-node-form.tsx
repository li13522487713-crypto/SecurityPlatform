import { Input, Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowIterableListLoopSource, MicroflowObject, MicroflowWhileLoopCondition } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { collectLoopObjects, getLoopBodyFlows, getLoopBodyReturnFlows, getLoopExitFlows, getLoopIncomingFlows, getLoopWarnings, updateLoopType } from "../../schema/utils";
import { FieldError, FieldRow, VariableNameInput } from "../common";
import { ExpressionEditor } from "../expression";
import { DataTypeSelector, VariableSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { expression, Field } from "../panel-shared";
import { getVariableNameConflicts } from "../../variables";

const { Text } = Typography;

export function LoopNodeForm({ props, object, issues, metadata, variableIndex, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "loopedActivity") {
    return null;
  }
  const warnings = getLoopWarnings(props.schema, object.id);
  const incomingFlows = getLoopIncomingFlows(props.schema, object.id);
  const bodyFlows = getLoopBodyFlows(props.schema, object.id);
  const exitFlows = getLoopExitFlows(props.schema, object.id);
  const bodyReturnFlows = getLoopBodyReturnFlows(props.schema, object.id);
  const loopVariableConflicts = object.loopSource.kind === "iterableList"
    ? getVariableNameConflicts(props.schema, object.loopSource.iteratorVariableName, `loopIterator:${object.id}`)
    : [];
  return (
    <>
      <Field label="Loop Type">
        <Select
          value={object.loopSource.kind === "whileCondition" ? "while" : "forEach"}
          disabled={props.readonly}
          style={{ width: "100%" }}
          onChange={loopType => {
            const nextSchema = updateLoopType(props.schema, object.id, String(loopType) === "while" ? "while" : "forEach");
            const nextObject = collectLoopObjects(nextSchema).find(loop => loop.id === object.id);
            if (nextObject) {
              patch(nextObject);
            }
          }}
          optionList={[
            { label: "forEach", value: "forEach" },
            { label: "while", value: "while" },
          ]}
        />
        <Text type="tertiary" size="small">当前 schema 支持 forEach 与 while；repeatUntil 未在源码契约中建模，本轮不强行扩展。</Text>
      </Field>
      {object.loopSource.kind === "iterableList" ? (
        <>
          <Field label="Source List / Iterable Expression">
            <Input
              value={object.loopSource.listVariableName}
              disabled={props.readonly}
              placeholder="List variable name or expression"
              onChange={listVariableName => patch({ ...object, loopSource: { ...object.loopSource, listVariableName } as MicroflowIterableListLoopSource })}
            />
            <VariableSelector
              schema={props.schema}
              objectId={object.id}
              fieldPath="loopSource.listVariableName"
              allowedTypeKinds={["list"]}
              value={object.loopSource.listVariableName}
              disabled={props.readonly}
              placeholder="Or select a List variable"
              onChange={listVariableName => patch({ ...object, loopSource: { ...object.loopSource, listVariableName: listVariableName ?? "" } as MicroflowIterableListLoopSource })}
            />
            <FieldError issues={getIssuesForField(issues, "loopSource.listVariableName")} />
          </Field>
          <Field label="Iterator Variable">
            <VariableNameInput
              value={object.loopSource.iteratorVariableName}
              schema={props.schema}
              objectId={object.id}
              fieldPath="loopSource.iteratorVariableName"
              suggestedBaseName="currentItem"
              readonly={props.readonly}
              required
              issues={getIssuesForField(issues, "loopSource.iteratorVariableName")}
              onChange={iteratorVariableName => patch({ ...object, loopSource: { ...object.loopSource, iteratorVariableName: iteratorVariableName ?? "" } as MicroflowIterableListLoopSource })}
            />
            {loopVariableConflicts.length ? <Text type="warning" size="small">{loopVariableConflicts.join(" ")}</Text> : null}
            <Text type="warning" size="small">Renaming a loop variable does not rewrite existing expressions.</Text>
          </Field>
          <Field label="Loop Variable Type">
            <DataTypeSelector
              value={object.loopSource.iteratorVariableDataType ?? { kind: "unknown", reason: "loop variable" }}
              disabled={props.readonly}
              allowVoid={false}
              onChange={iteratorVariableDataType => patch({ ...object, loopSource: { ...object.loopSource, iteratorVariableDataType } as MicroflowIterableListLoopSource })}
            />
          </Field>
          <Field label="Current Index Variable">
            <Input value={object.loopSource.currentIndexVariableName} disabled />
          </Field>
        </>
      ) : (
        <Field label="While Expression">
          <ExpressionEditor
            value={object.loopSource.expression}
            schema={props.schema}
            metadata={metadata}
            variableIndex={variableIndex}
            objectId={object.id}
            fieldPath="loopSource.expression"
            expectedType={{ kind: "boolean" }}
            required
            readonly={props.readonly}
            onChange={nextExpression => patch({ ...object, loopSource: { ...object.loopSource, expression: nextExpression } as MicroflowWhileLoopCondition })}
          />
        </Field>
      )}
      <Field label="Flow Summary">
        <TextArea
          value={[
            `incoming: ${incomingFlows.length}`,
            `body: ${bodyFlows.map(flow => flow.id).join(", ") || "missing"}`,
            `body return: ${bodyReturnFlows.map(flow => flow.id).join(", ") || "none"}`,
            `exit: ${exitFlows.map(flow => flow.id).join(", ") || "missing"}`,
            `body nodes: ${object.objectCollection.objects.length}`,
            `break: ${object.objectCollection.objects.filter(child => child.kind === "breakEvent").length}`,
            `continue: ${object.objectCollection.objects.filter(child => child.kind === "continueEvent").length}`,
          ].join("\n")}
          autosize
          disabled
        />
        <Text type="tertiary" size="small">Loop variable scope validation will be completed in Stage 20.</Text>
      </Field>
      <Field label="Warnings">
        <Space vertical align="start" spacing={4}>
          {warnings.length ? warnings.map(warning => <Text key={warning} type="warning" size="small">{warning}</Text>) : <Text type="tertiary" size="small">No Loop warnings.</Text>}
        </Space>
      </Field>
      <FieldRow label="Error Handling" fieldPath="errorHandlingType" issues={getIssuesForField(issues, "errorHandlingType")}>
        <Select
          value={object.errorHandlingType}
          disabled={props.readonly}
          style={{ width: "100%" }}
          onChange={errorHandlingType => patch({ ...object, errorHandlingType: String(errorHandlingType) as typeof object.errorHandlingType })}
          optionList={["rollback", "customWithRollback", "customWithoutRollback", "continue"].map(value => ({ label: value, value }))}
        />
      </FieldRow>
    </>
  );
}
