import { Input, Switch, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { Field } from "../panel-shared";

const { Text } = Typography;

export function ObjectBaseForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  return (
    <>
      <Field label="ID">
        <Input value={object.id} disabled />
      </Field>
      <Field label="Kind">
        <Input value={object.kind} disabled />
      </Field>
      <Field label="Caption">
        <Input
          value={object.caption ?? ""}
          disabled={readonly}
          onChange={caption => patch({ ...object, caption } as MicroflowObject)}
        />
        {!object.caption?.trim() ? <Text type="warning" size="small">Caption 为空时画布会使用节点类型作为显示名称；不会写入示例值。</Text> : null}
      </Field>
      <Field label="Description">
        <TextArea value={object.documentation ?? ""} autosize disabled={readonly} onChange={documentation => patch({ ...object, documentation } as MicroflowObject)} />
      </Field>
      <Field label="Position">
        <Input value={`x=${object.relativeMiddlePoint.x}, y=${object.relativeMiddlePoint.y}`} disabled />
      </Field>
      <Field label="Disabled">
        <Switch checked={Boolean(object.disabled)} disabled={readonly} onChange={disabled => patch({ ...object, disabled } as MicroflowObject)} />
      </Field>
    </>
  );
}
