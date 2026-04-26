import { Input, Select } from "@douyinfe/semi-ui";
import type { MicroflowIterableListLoopSource, MicroflowObject, MicroflowWhileLoopCondition } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import { FieldError } from "../common";
import { ExpressionEditor } from "../expression";
import { VariableSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { expression, Field } from "../panel-shared";

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
  return (
    <>
      <Field label="Loop Source">
        <Select
          value={object.loopSource.kind}
          disabled={props.readonly}
          style={{ width: "100%" }}
          onChange={kind => patch({
            ...object,
            loopSource: String(kind) === "whileCondition"
              ? { kind: "whileCondition", officialType: "Microflows$WhileLoopCondition", expression: expression("true", { kind: "boolean" }) }
              : { kind: "iterableList", officialType: "Microflows$IterableList", listVariableName: "Items", iteratorVariableName: "Item", currentIndexVariableName: "$currentIndex" } as MicroflowIterableListLoopSource | MicroflowWhileLoopCondition
          })}
          optionList={[{ label: "Iterable List", value: "iterableList" }, { label: "While Condition", value: "whileCondition" }]}
        />
      </Field>
      {object.loopSource.kind === "iterableList" ? (
        <>
          <Field label="List Variable">
            <VariableSelector
              schema={props.schema}
              objectId={object.id}
              fieldPath="loopSource.listVariableName"
              allowedTypeKinds={["list"]}
              value={object.loopSource.listVariableName}
              disabled={props.readonly}
              onChange={listVariableName => patch({ ...object, loopSource: { ...object.loopSource, listVariableName: listVariableName ?? "" } as MicroflowIterableListLoopSource })}
            />
            <FieldError issues={getIssuesForField(issues, "loopSource.listVariableName")} />
          </Field>
          <Field label="Iterator Variable">
            <Input value={object.loopSource.iteratorVariableName} disabled={props.readonly} onChange={iteratorVariableName => patch({ ...object, loopSource: { ...object.loopSource, iteratorVariableName } as MicroflowIterableListLoopSource })} />
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
      <Field label="Body Summary">
        <Input
          value={[
            `${object.objectCollection.objects.length} nodes`,
            `${(object.objectCollection.flows ?? []).length || collectFlowsRecursive(props.schema).filter(flow =>
              object.objectCollection.objects.some(child => child.id === flow.originObjectId) &&
              object.objectCollection.objects.some(child => child.id === flow.destinationObjectId)
            ).length} flows`,
            `${object.objectCollection.objects.filter(child => child.kind === "breakEvent").length} break`,
            `${object.objectCollection.objects.filter(child => child.kind === "continueEvent").length} continue`,
          ].join(" · ")}
          disabled
        />
      </Field>
    </>
  );
}
