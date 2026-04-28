import { Input, Switch, TextArea } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { Field, updateObjectAdvanced } from "../panel-shared";

export function AnnotationObjectForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "annotation") {
    return null;
  }
  return (
    <>
      <Field label="Content">
        <TextArea value={object.text} autosize disabled={readonly} onChange={text => patch({ ...object, text })} />
      </Field>
      <Field label="Color Token">
        <Input value={object.editor.colorToken ?? ""} disabled={readonly} onChange={colorToken => patch({ ...object, editor: { ...object.editor, colorToken } })} />
      </Field>
      <Field label="Pinned">
        <Switch checked={Boolean((object.editor as unknown as { pinned?: boolean }).pinned)} disabled={readonly} onChange={pinned => patch(updateObjectAdvanced(object, { pinned }))} />
      </Field>
      <Field label="Export To Documentation">
        <Switch checked={(object.editor as unknown as { exportToDocumentation?: boolean }).exportToDocumentation ?? true} disabled={readonly} onChange={exportToDocumentation => patch(updateObjectAdvanced(object, { exportToDocumentation }))} />
      </Field>
    </>
  );
}
