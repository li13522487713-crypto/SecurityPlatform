import { Input, Select, Switch, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import { Field } from "../panel-shared";

const { Text } = Typography;

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
  if (object.kind !== "errorHandler") {
    return null;
  }
  return (
    <>
      <Field label="Policy">
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
      </Field>
      <Field label="Custom Handler Variable">
        <Input
          value={object.customHandlerVariable ?? ""}
          disabled={readonly || object.policy !== "custom"}
          onChange={value => patch({ ...object, customHandlerVariable: value || undefined })}
        />
      </Field>
      <Field label="Continue On Error">
        <Switch
          checked={object.continueOnError}
          disabled={readonly}
          onChange={value => patch({ ...object, continueOnError: value })}
        />
      </Field>
      <Text type="tertiary" size="small">
        Error Handler 节点本身在 runtime 中目前透明；同等语义可通过 Activity errorHandling 字段（rollback / continue / customWithRollback）落地。
      </Text>
    </>
  );
}
