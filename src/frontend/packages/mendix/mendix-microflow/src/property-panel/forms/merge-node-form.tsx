import { Input, Select, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { getMergeFlowSummary } from "../../schema/utils";
import type { MicroflowPropertyPanelProps } from "../types";
import { Field } from "../panel-shared";

const { Text } = Typography;

export function MergeNodeForm({ props, object, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "exclusiveMerge") {
    return null;
  }
  const summary = getMergeFlowSummary(props.schema, object.id);
  return (
    <>
      <Field label="Merge Strategy">
        <Select
          value={object.mergeBehavior.strategy}
          disabled={props.readonly}
          style={{ width: "100%" }}
          optionList={[{ label: "firstArrived", value: "firstArrived" }]}
          onChange={strategy => patch({ ...object, mergeBehavior: { strategy: String(strategy) as typeof object.mergeBehavior.strategy } })}
        />
      </Field>
      <Field label="Incoming Count">
        <Input value={String(summary.incoming.length)} disabled />
        {summary.incoming.length < 2 ? <Text type="warning" size="small">Merge usually requires multiple incoming flows.</Text> : null}
      </Field>
      <Field label="Outgoing Count">
        <Input value={String(summary.outgoing.length)} disabled />
        {summary.outgoing.length === 0 ? <Text type="warning" size="small">Merge should have an outgoing flow.</Text> : null}
        {summary.outgoing.length > 1 ? <Text type="warning" size="small">ExclusiveMerge usually has a single outgoing flow.</Text> : null}
      </Field>
      <Field label="Flow Summary">
        <TextArea
          value={[
            ...summary.incoming.map(flow => `in: ${flow.id} from ${flow.originObjectId}`),
            ...summary.outgoing.map(flow => `out: ${flow.id} to ${flow.destinationObjectId}`),
          ].join("\n") || "No connected flows"}
          autosize
          disabled
        />
      </Field>
    </>
  );
}
