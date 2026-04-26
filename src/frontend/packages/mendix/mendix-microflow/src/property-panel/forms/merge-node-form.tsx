import { Input } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { collectFlowsRecursive } from "../../schema/utils/object-utils";
import type { MicroflowPropertyPanelProps } from "../types";
import { Field } from "../panel-shared";

export function MergeNodeForm({ props, object }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
}) {
  if (object.kind !== "exclusiveMerge") {
    return null;
  }
  return (
    <>
      <Field label="Merge Strategy">
        <Input value={object.mergeBehavior.strategy} disabled />
      </Field>
      <Field label="Incoming Count">
        <Input value={String(collectFlowsRecursive(props.schema).filter(flow => flow.destinationObjectId === object.id).length)} disabled />
      </Field>
      <Field label="Outgoing Count">
        <Input value={String(collectFlowsRecursive(props.schema).filter(flow => flow.originObjectId === object.id).length)} disabled />
      </Field>
    </>
  );
}
