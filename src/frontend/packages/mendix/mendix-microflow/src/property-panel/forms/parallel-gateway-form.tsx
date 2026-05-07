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
 * Parallel Gateway 节点属性面板。
 *
 * 该节点进入 testRun 主路径；用户可以配置 split/join 模式、分支命名和合并策略。
 * 当前 runtime 会执行所有可达分支并记录分支 trace，复杂并发隔离按执行器能力降级。
 */
export function ParallelGatewayForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = readonly ? "Readonly mode cannot edit parallel gateway settings." : "";
  if (object.kind !== "parallelGateway") {
    return null;
  }
  return (
    <>
      <Field label="Gateway Mode">
        {withDisabledReason(
          readonlyDisabledReason,
          "Gateway mode",
          <Select
            value={object.gatewayMode}
            disabled={readonly}
            style={{ width: "100%" }}
            optionList={[
              { label: "Auto", value: "auto" },
              { label: "Split", value: "split" },
              { label: "Join", value: "join" }
            ]}
            onChange={value => patch({ ...object, gatewayMode: String(value) as typeof object.gatewayMode })}
          />
        )}
      </Field>
      <Field label="Join Policy">
        {withDisabledReason(
          readonlyDisabledReason,
          "Join policy",
          <Select
            value={object.joinPolicy}
            disabled={readonly}
            style={{ width: "100%" }}
            optionList={[
              { label: "Wait All", value: "waitAll" },
              { label: "Wait Any", value: "waitAny" }
            ]}
            onChange={value => patch({ ...object, joinPolicy: String(value) as typeof object.joinPolicy })}
          />
        )}
      </Field>
      <Field label="Branch Count">
        <Input value={String(object.branches.length)} disabled />
      </Field>
      <Text type="warning" size="small">
        Parallel Gateway 已进入 runtime 主路径；testRun 会执行所有可达分支并记录分支 trace，复杂并发隔离按执行器能力降级。
      </Text>
    </>
  );
}
