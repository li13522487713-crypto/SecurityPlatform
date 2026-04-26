import { useEffect, useState } from "react";
import { Input, List, Modal, Space, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowPublishInput, MicroflowResource } from "./resource-types";
import { nextMicroflowVersion } from "./resource-utils";

const { Text } = Typography;

interface PublishMicroflowModalProps {
  resource?: MicroflowResource;
  visible: boolean;
  onClose: () => void;
  onSubmit: (input: MicroflowPublishInput) => Promise<void>;
}

export function PublishMicroflowModal({ resource, visible, onClose, onSubmit }: PublishMicroflowModalProps) {
  const [version, setVersion] = useState("");
  const [releaseNote, setReleaseNote] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const hasError = Boolean(resource?.archived);

  useEffect(() => {
    if (visible && resource) {
      setVersion(nextMicroflowVersion(resource.version));
      setReleaseNote("");
    }
  }, [resource, visible]);

  async function handleSubmit() {
    if (hasError) {
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit({ version, releaseNote, overwriteCurrent: true });
      onClose();
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="发布微流" onCancel={onClose} onOk={() => void handleSubmit()} okButtonProps={{ disabled: hasError }} confirmLoading={submitting}>
      <Space vertical align="start" style={{ width: "100%" }}>
        <Input value={version} onChange={setVersion} prefix="版本号" />
        <Input value={releaseNote} onChange={setReleaseNote} prefix="发布说明" />
        <Text strong>校验结果摘要</Text>
        <List
          dataSource={[
            { type: hasError ? "error" : "success", text: hasError ? "已归档微流不能发布" : "基础结构校验通过" },
            { type: "warning", text: "引用影响分析为 mock 数据，真实治理将在后续接入" }
          ]}
          renderItem={item => (
            <List.Item>
              <Tag color={item.type === "error" ? "red" : item.type === "warning" ? "orange" : "green"}>{item.type}</Tag>
              <Text>{item.text}</Text>
            </List.Item>
          )}
        />
      </Space>
    </Modal>
  );
}
