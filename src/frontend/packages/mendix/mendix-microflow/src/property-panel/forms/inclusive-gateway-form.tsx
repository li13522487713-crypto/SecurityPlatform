import { Input, Select, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowPropertyPanelProps } from "../types";
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
 * Inclusive Gateway (OR) 节点属性面板。
 *
 * 用于建模"可同时执行多个条件分支"的场景；testRun 会按分支条件选择可达路径，
 * 并在 trace 中标记 selected / skipped 分支。
 */
export function InclusiveGatewayForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = readonly ? "Readonly mode cannot edit inclusive gateway settings." : "";
  if (object.kind !== "inclusiveGateway") {
    return null;
  }
  return (
    <>
      <Field label="Branch Count">
        <Input value={String(object.branches.length)} disabled />
      </Field>
      <Field label="Default Branch">
        {withDisabledReason(
          readonlyDisabledReason,
          "Default branch",
          <Input
            value={object.defaultBranch ?? ""}
            disabled={readonly}
            onChange={value => patch({ ...object, defaultBranch: value || null })}
            placeholder="branch id"
          />
        )}
      </Field>
      <Field label="Merge Policy">
        {withDisabledReason(
          readonlyDisabledReason,
          "Merge policy",
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
        )}
      </Field>
      <Text type="warning" size="small">
        Inclusive Gateway 已进入 runtime 主路径；testRun 会按条件选择可达分支，并在 trace 中标记分支状态。
      </Text>
    </>
  );
}
