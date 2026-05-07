import { Input, Select, Switch, Tooltip, Typography } from "@douyinfe/semi-ui";
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
 * Error Handler 节点属性面板。
 *
 * 该节点装饰一段流程的错误处理策略；当前由 Activity errorHandling 字段承担
 * 真实运行时语义，此节点仅作为可视化辅助。
 */
export function ErrorHandlerForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = readonly ? "Readonly mode cannot edit error handler settings." : "";
  if (object.kind !== "errorHandler") {
    return null;
  }
  return (
    <>
      <Field label="Policy">
        {withDisabledReason(
          readonlyDisabledReason,
          "Policy",
          <Select
            value={object.policy}
            disabled={readonly}
            style={{ width: "100%" }}
            optionList={[
              { label: "Rollback", value: "rollback" },
              { label: "Continue", value: "continue" },
              { label: "Custom", value: "custom" }
            ]}
            onChange={value => patch({ ...object, policy: String(value) as typeof object.policy })}
          />
        )}
      </Field>
      <Field label="Custom Handler Variable">
        {withDisabledReason(
          readonly ? "Readonly mode cannot edit error handler settings." : (object.policy !== "custom" ? "Set policy to custom before editing this field." : ""),
          "Custom handler variable",
          <Input
            value={object.customHandlerVariable ?? ""}
            disabled={readonly || object.policy !== "custom"}
            onChange={value => patch({ ...object, customHandlerVariable: value || undefined })}
          />
        )}
      </Field>
      <Field label="Continue On Error">
        {withDisabledReason(
          readonlyDisabledReason,
          "Continue on error",
          <Switch
            checked={object.continueOnError}
            disabled={readonly}
            onChange={value => patch({ ...object, continueOnError: value })}
          />
        )}
      </Field>
      <Text type="tertiary" size="small">
        Error Handler 节点本身在 runtime 中目前透明；同等语义可通过 Activity errorHandling 字段（rollback / continue / customWithRollback）落地。
      </Text>
    </>
  );
}
