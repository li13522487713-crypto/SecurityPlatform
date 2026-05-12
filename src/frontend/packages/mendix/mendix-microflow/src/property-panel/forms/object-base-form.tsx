import { Input, Select, Switch, TextArea, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowActionActivityColor, MicroflowObject } from "../../schema";
import { Field } from "../panel-shared";

const { Text } = Typography;
const EVENT_KINDS = new Set(["startEvent", "endEvent", "errorEvent", "breakEvent", "continueEvent"]);
const BACKGROUND_COLOR_OPTIONS: Array<{ label: string; value: MicroflowActionActivityColor }> = [
  { label: "default", value: "default" },
  { label: "blue", value: "blue" },
  { label: "green", value: "green" },
  { label: "yellow", value: "yellow" },
  { label: "orange", value: "orange" },
  { label: "red", value: "red" },
  { label: "purple", value: "purple" },
  { label: "gray", value: "gray" },
];

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

export function ObjectBaseForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = readonly ? "Readonly mode cannot edit object settings." : "";
  const supportsBackgroundColor = object.kind !== "actionActivity" && !EVENT_KINDS.has(object.kind);
  return (
    <>
      <Field label="ID">
        <Input value={object.id} disabled />
      </Field>
      <Field label="Kind">
        <Input value={object.kind} disabled />
      </Field>
      <Field label="Caption">
        {withDisabledReason(
          readonlyDisabledReason,
          "Caption",
          <Input
            value={object.caption ?? ""}
            disabled={readonly}
            onChange={caption => patch({ ...object, caption } as MicroflowObject)}
          />
        )}
        {!object.caption?.trim() ? <Text type="warning" size="small">Caption 为空时画布会使用节点类型作为显示名称；不会写入示例值。</Text> : null}
      </Field>
      <Field label="Description">
        {withDisabledReason(
          readonlyDisabledReason,
          "Description",
          <TextArea value={object.documentation ?? ""} autosize disabled={readonly} onChange={documentation => patch({ ...object, documentation } as MicroflowObject)} />
        )}
      </Field>
      {supportsBackgroundColor ? (
        <Field label="Background Color">
          {withDisabledReason(
            readonlyDisabledReason,
            "Background color",
            <Select
              value={object.backgroundColor ?? "default"}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={backgroundColor => patch({ ...object, backgroundColor: String(backgroundColor) as MicroflowActionActivityColor } as MicroflowObject)}
              optionList={BACKGROUND_COLOR_OPTIONS}
            />
          )}
        </Field>
      ) : null}
      <Field label="Position">
        <Input value={`x=${object.relativeMiddlePoint.x}, y=${object.relativeMiddlePoint.y}`} disabled />
      </Field>
      <Field label="Disabled">
        {withDisabledReason(
          readonlyDisabledReason,
          "Disabled",
          <Switch checked={Boolean(object.disabled)} disabled={readonly} onChange={disabled => patch({ ...object, disabled } as MicroflowObject)} />
        )}
      </Field>
    </>
  );
}
