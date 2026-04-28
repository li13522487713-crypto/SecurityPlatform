import { useEffect, useState } from "react";

import { Button, Modal, Radio, RadioGroup, Space, Typography } from "@douyinfe/semi-ui";

import type { MicroflowCaseValue } from "../schema";
import type { MicroflowCaseEditorKind, MicroflowCaseOption } from "./adapters/flowgram-case-options";
import { caseValueKey } from "./adapters/flowgram-case-options";

function titleFor(kind: MicroflowCaseEditorKind): string {
  if (kind === "enumeration") {
    return "Decision Enumeration Case";
  }
  if (kind === "objectType") {
    return "Object Type Case";
  }
  return "Decision Boolean Case";
}

function descriptionFor(kind: MicroflowCaseEditorKind): string {
  if (kind === "enumeration") {
    return "选择这条 Decision 分支对应的枚举值。";
  }
  if (kind === "objectType") {
    return "选择这条对象类型分支对应的 specialization、empty 或 fallback。";
  }
  return "选择这条 Decision 分支对应的布尔值。";
}

function exhaustedMessage(kind: MicroflowCaseEditorKind): string {
  if (kind === "enumeration") {
    return "所有枚举分支都已存在，不能继续创建 enumeration case flow。";
  }
  if (kind === "objectType") {
    return "所有对象类型分支都已存在，不能继续创建 object type case flow。";
  }
  return "true / false 分支都已存在，不能继续创建 boolean case flow。";
}

export function FlowGramMicroflowCaseEditor(props: {
  visible: boolean;
  kind: MicroflowCaseEditorKind;
  options: MicroflowCaseOption[];
  onConfirm: (caseValue: MicroflowCaseValue, label: string) => void;
  onCancel: () => void;
}) {
  const firstAvailable = props.options.find(option => !option.disabled);
  const [selectedKey, setSelectedKey] = useState<string | undefined>(firstAvailable?.key);

  useEffect(() => {
    setSelectedKey(firstAvailable?.key);
  }, [firstAvailable?.key, props.visible]);

  const selectedOption = props.options.find(option => option.key === selectedKey && !option.disabled);
  return (
    <Modal
      title={titleFor(props.kind)}
      visible={props.visible}
      onCancel={props.onCancel}
      footer={null}
      width={420}
      keepDOM={false}
    >
      <Space vertical align="start" spacing={16} style={{ width: "100%" }}>
        <Typography.Text type="secondary">{descriptionFor(props.kind)}</Typography.Text>
        <RadioGroup
          value={selectedKey}
          onChange={event => {
            const key = String(event.target.value);
            const option = props.options.find(item => item.key === key);
            if (option && !option.disabled) {
              setSelectedKey(key);
            }
          }}
        >
          <Space vertical align="start">
            {props.options.map(option => (
              <Radio key={option.key} value={caseValueKey(option.caseValue)} disabled={option.disabled}>
                {option.label}
                {option.reason ? <Typography.Text type="tertiary"> - {option.reason}</Typography.Text> : null}
              </Radio>
            ))}
          </Space>
        </RadioGroup>
        {firstAvailable === undefined ? (
          <Typography.Text type="danger">{exhaustedMessage(props.kind)}</Typography.Text>
        ) : null}
        <Space>
          <Button onClick={props.onCancel}>取消</Button>
          <Button
            type="primary"
            disabled={!selectedOption}
            onClick={() => {
              if (selectedOption) {
                props.onConfirm(selectedOption.caseValue, selectedOption.label);
              }
            }}
          >
            确定
          </Button>
        </Space>
      </Space>
    </Modal>
  );
}
