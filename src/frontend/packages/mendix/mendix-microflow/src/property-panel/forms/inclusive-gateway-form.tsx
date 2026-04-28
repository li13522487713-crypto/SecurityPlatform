import { Input, Select, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowPropertyPanelProps } from "../types";
import { Field } from "../panel-shared";

const { Text } = Typography;

/**
 * Inclusive Gateway (OR) 节点属性面板。
 *
 * 用于建模"可同时执行多个条件分支"的场景；当前 testRun 引擎不会按 OR 分发，
 * 仅在 toolbox 上标记为 unsupported，避免静默运行错误结果。
 */
export function InclusiveGatewayForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "inclusiveGateway") {
    return null;
  }
  return (
    <>
      <Field label="Branch Count">
        <Input value={String(object.branches.length)} disabled />
      </Field>
      <Field label="Default Branch">
        <Input
          value={object.defaultBranch ?? ""}
          disabled={readonly}
          onChange={value => patch({ ...object, defaultBranch: value || null })}
          placeholder="branch id"
        />
      </Field>
      <Field label="Merge Policy">
        <Select
          value={object.mergePolicy}
          disabled={readonly}
          style={{ width: "100%" }}
          optionList={[
            { label: "Wait Any", value: "waitAny" },
            { label: "Wait All", value: "waitAll" }
          ]}
          onChange={value => patch({ ...object, mergePolicy: String(value) as typeof object.mergePolicy })}
        />
      </Field>
      <Text type="warning" size="small">
        Inclusive Gateway 暂不在 runtime 引擎主路径执行；testRun 会返回 RUNTIME_UNSUPPORTED_ACTION。
      </Text>
    </>
  );
}
