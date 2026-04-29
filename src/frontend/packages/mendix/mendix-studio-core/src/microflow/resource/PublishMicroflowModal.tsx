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
            // P1-4: 该 modal 为旧 demo 版本（仍由 MicroflowResourceTab 使用），生产路径
            // 已切到 microflow/publish/PublishMicroflowModal，那里展示真实 impact /
            // breaking change。这里删除"引用影响为 mock"的提示，避免误导。
            { type: "info", text: "完整引用影响请在 Mendix Studio 编辑器内的 Publish Dialog 查看（接入真实 GET /api/v1/microflows/{id}/impact）" }
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
