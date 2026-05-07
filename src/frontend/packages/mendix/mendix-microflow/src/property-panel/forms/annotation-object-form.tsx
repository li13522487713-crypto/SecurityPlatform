import { Input, Switch, TextArea, Tooltip } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { Field, updateObjectAdvanced } from "../panel-shared";

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

export function AnnotationObjectForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "annotation") {
    return null;
  }
  const readonlyDisabledReason = readonly ? "Readonly mode cannot edit annotation settings." : "";
  return (
    <>
      <Field label="Content">
        {withDisabledReason(
          readonlyDisabledReason,
          "Content",
          <TextArea value={object.text} autosize disabled={readonly} onChange={text => patch({ ...object, text })} />
        )}
      </Field>
      <Field label="Color Token">
        {withDisabledReason(
          readonlyDisabledReason,
          "Color token",
          <Input value={object.editor.colorToken ?? ""} disabled={readonly} onChange={colorToken => patch({ ...object, editor: { ...object.editor, colorToken } })} />
        )}
      </Field>
      <Field label="Pinned">
        {withDisabledReason(
          readonlyDisabledReason,
          "Pinned",
          <Switch checked={Boolean((object.editor as unknown as { pinned?: boolean }).pinned)} disabled={readonly} onChange={pinned => patch(updateObjectAdvanced(object, { pinned }))} />
        )}
      </Field>
      <Field label="Export To Documentation">
        {withDisabledReason(
          readonlyDisabledReason,
          "Export to documentation",
          <Switch checked={(object.editor as unknown as { exportToDocumentation?: boolean }).exportToDocumentation ?? true} disabled={readonly} onChange={exportToDocumentation => patch(updateObjectAdvanced(object, { exportToDocumentation }))} />
        )}
      </Field>
    </>
  );
}
