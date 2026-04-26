import { Input, Switch, TextArea } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { Field } from "../panel-shared";

export function ObjectBaseForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  return (
    <>
      <Field label="Kind">
        <Input value={object.kind} disabled />
      </Field>
      <Field label="Caption">
        <Input value={object.caption ?? ""} disabled={readonly} onChange={caption => patch({ ...object, caption } as MicroflowObject)} />
      </Field>
      <Field label="Documentation">
        <TextArea value={object.documentation ?? ""} autosize disabled={readonly} onChange={documentation => patch({ ...object, documentation } as MicroflowObject)} />
      </Field>
      <Field label="Disabled">
        <Switch checked={Boolean(object.disabled)} disabled={readonly} onChange={disabled => patch({ ...object, disabled } as MicroflowObject)} />
      </Field>
    </>
  );
}
