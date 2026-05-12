import { TextArea, Tooltip } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { Field } from "../panel-shared";

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
      <Field label="Annotation text">
        {withDisabledReason(
          readonlyDisabledReason,
          "Annotation text",
          <TextArea value={object.text} autosize disabled={readonly} onChange={text => patch({ ...object, text })} />
        )}
      </Field>
    </>
  );
}
