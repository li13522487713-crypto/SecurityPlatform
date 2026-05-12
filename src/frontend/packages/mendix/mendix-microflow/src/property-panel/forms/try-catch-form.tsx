import { Input, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { Field } from "../panel-shared";

const { Text } = Typography;

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

/**
 * Try / Catch / Finally 节点属性面板（建模占位）。
 */
export function TryCatchForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = readonly ? "Readonly mode cannot edit try/catch settings." : "";
  if (object.kind !== "tryCatch") {
    return null;
  }
  return (
    <>
      <details open>
        <summary style={{ cursor: "pointer", fontWeight: 600, fontSize: 13, color: "var(--semi-color-text-0, #1d2129)" }}>Try</summary>
        <div style={{ paddingTop: 8 }}>
          <Field label="Try Branch Key">
            {withDisabledReason(
              readonlyDisabledReason,
              "Try branch key",
              <Input value={object.tryBranchKey} disabled={readonly} onChange={value => patch({ ...object, tryBranchKey: value })} />
            )}
          </Field>
        </div>
      </details>
      <details open>
        <summary style={{ cursor: "pointer", fontWeight: 600, fontSize: 13, color: "var(--semi-color-text-0, #1d2129)" }}>Catch</summary>
        <div style={{ paddingTop: 8 }}>
          <Field label="Catch Branch Key">
            {withDisabledReason(
              readonlyDisabledReason,
              "Catch branch key",
              <Input value={object.catchBranchKey} disabled={readonly} onChange={value => patch({ ...object, catchBranchKey: value })} />
            )}
          </Field>
          <Field label="Error Variable Name">
            {withDisabledReason(
              readonlyDisabledReason,
              "Error variable name",
              <Input value={object.errorVariableName} disabled={readonly} onChange={value => patch({ ...object, errorVariableName: value })} />
            )}
          </Field>
        </div>
      </details>
      <details open>
        <summary style={{ cursor: "pointer", fontWeight: 600, fontSize: 13, color: "var(--semi-color-text-0, #1d2129)" }}>Finally</summary>
        <div style={{ paddingTop: 8 }}>
          <Field label="Finally Branch Key">
            {withDisabledReason(
              readonlyDisabledReason,
              "Finally branch key",
              <Input value={object.finallyBranchKey ?? ""} disabled={readonly} onChange={value => patch({ ...object, finallyBranchKey: value || undefined })} placeholder="optional" />
            )}
          </Field>
        </div>
      </details>
      <Text type="tertiary" size="small">Catch branch receives error context variables such as $latestError.</Text>
    </>
  );
}
