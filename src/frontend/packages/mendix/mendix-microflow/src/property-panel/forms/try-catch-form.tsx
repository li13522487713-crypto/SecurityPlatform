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
      <Field label="Try Branch Key">
        {withDisabledReason(
          readonlyDisabledReason,
          "Try branch key",
          <Input value={object.tryBranchKey} disabled={readonly} onChange={value => patch({ ...object, tryBranchKey: value })} />
        )}
      </Field>
      <Field label="Catch Branch Key">
        {withDisabledReason(
          readonlyDisabledReason,
          "Catch branch key",
          <Input value={object.catchBranchKey} disabled={readonly} onChange={value => patch({ ...object, catchBranchKey: value })} />
        )}
      </Field>
      <Field label="Finally Branch Key">
        {withDisabledReason(
          readonlyDisabledReason,
          "Finally branch key",
          <Input value={object.finallyBranchKey ?? ""} disabled={readonly} onChange={value => patch({ ...object, finallyBranchKey: value || undefined })} placeholder="optional" />
        )}
      </Field>
      <Field label="Error Variable Name">
        {withDisabledReason(
          readonlyDisabledReason,
          "Error variable name",
          <Input value={object.errorVariableName} disabled={readonly} onChange={value => patch({ ...object, errorVariableName: value })} />
        )}
      </Field>
      <Text type="warning" size="small">
        Try/Catch 节点暂不在 testRun 主路径解释；如需错误处理请在 Activity 节点的 errorHandling 字段或 Error Handler 节点配置策略。
      </Text>
    </>
  );
}
