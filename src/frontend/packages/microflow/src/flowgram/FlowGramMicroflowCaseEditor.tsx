import { Button, Modal, Radio, RadioGroup, Space, Typography } from "@douyinfe/semi-ui";

import type { MicroflowCaseValue } from "../schema";

export interface BooleanCaseOption {
  value: boolean;
  disabled: boolean;
  reason?: string;
}

export function booleanCaseValue(value: boolean): MicroflowCaseValue {
  return { kind: "boolean", value, persistedValue: String(value) };
}

export function FlowGramMicroflowCaseEditor(props: {
  visible: boolean;
  options: BooleanCaseOption[];
  onConfirm: (value: boolean) => void;
  onCancel: () => void;
}) {
  const firstAvailable = props.options.find(option => !option.disabled)?.value;
  return (
    <Modal
      title="Decision Boolean Case"
      visible={props.visible}
      onCancel={props.onCancel}
      footer={null}
      width={360}
      keepDOM={false}
    >
      <Space vertical align="start" spacing={16} style={{ width: "100%" }}>
        <Typography.Text type="secondary">选择这条 Decision 分支对应的布尔值。</Typography.Text>
        <RadioGroup
          defaultValue={firstAvailable}
          onChange={event => {
            const value = event.target.value === true || event.target.value === "true";
            props.onConfirm(value);
          }}
        >
          <Space vertical align="start">
            {props.options.map(option => (
              <Radio key={String(option.value)} value={option.value} disabled={option.disabled}>
                {option.value ? "是 / true" : "否 / false"}
                {option.reason ? <Typography.Text type="tertiary"> - {option.reason}</Typography.Text> : null}
              </Radio>
            ))}
          </Space>
        </RadioGroup>
        {firstAvailable === undefined ? (
          <Typography.Text type="danger">true / false 分支都已存在，不能继续创建 boolean case flow。</Typography.Text>
        ) : null}
        <Space>
          <Button onClick={props.onCancel}>取消</Button>
        </Space>
      </Space>
    </Modal>
  );
}

