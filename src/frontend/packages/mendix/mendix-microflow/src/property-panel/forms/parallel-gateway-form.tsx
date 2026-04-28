import { Input, Select, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowPropertyPanelProps } from "../types";
import { Field } from "../panel-shared";

const { Text } = Typography;

/**
 * Parallel Gateway 节点属性面板。
 *
 * 该节点目前只参与建模/可视化，不进入 testRun 主路径；用户可以配置 split/join
 * 模式、分支命名和合并策略，运行时引擎将在后续轮次实现真实并发执行。
 */
export function ParallelGatewayForm({ object, readonly, patch }: {
  object: MicroflowObject;
  readonly?: boolean;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "parallelGateway") {
    return null;
  }
  return (
    <>
      <Field label="Gateway Mode">
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
      </Field>
      <Field label="Join Policy">
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
      </Field>
      <Field label="Branch Count">
        <Input value={String(object.branches.length)} disabled />
      </Field>
      <Text type="warning" size="small">
        Parallel Gateway 节点目前只在画布建模中使用；testRun 引擎执行其分支时仍按顺序运行。
      </Text>
    </>
  );
}
