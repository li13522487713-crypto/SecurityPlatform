import React, { useState } from "react";
import { Modal, TextArea, Typography } from "@douyinfe/semi-ui";

export interface PublishConfirmModalProps {
  visible: boolean;
  resourceName: string;
  resourceType: "agent" | "app" | "workflow";
  onCancel: () => void;
  onPublish: (releaseNote: string) => Promise<void>;
  pendingChanges?: string[];
}

export function PublishConfirmModal({
  visible,
  resourceName,
  resourceType,
  onCancel,
  onPublish,
  pendingChanges = []
}: PublishConfirmModalProps) {
  const [submitting, setSubmitting] = useState(false);
  const [releaseNote, setReleaseNote] = useState("");

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      await onPublish(releaseNote.trim());
      setReleaseNote("");
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = () => {
    setReleaseNote("");
    onCancel();
  };

  return (
    <Modal
      title={`发布 ${resourceName}`}
      visible={visible}
      onCancel={handleCancel}
      onOk={handleSubmit}
      okButtonProps={{ loading: submitting }}
      okText="确认发布"
      cancelText="取消"
      width={500}
    >
      <div style={{ marginBottom: 24 }}>
        <Typography.Text>
          您即将发布 {resourceType === "agent" ? "智能体" : resourceType === "app" ? "应用" : "工作流"}。发布后，新版本将替换当前运行版本，外部接入的客户端将立即生效。
        </Typography.Text>
      </div>

      {pendingChanges.length > 0 && (
        <div style={{ marginBottom: 16 }}>
          <Typography.Text strong>未发布变更内容：</Typography.Text>
          <ul>
            {pendingChanges.map((change, idx) => (
              <li key={idx}><Typography.Text type="tertiary">{change}</Typography.Text></li>
            ))}
          </ul>
        </div>
      )}

      <div style={{ marginTop: 12 }}>
        <Typography.Text strong style={{ display: "block", marginBottom: 8 }}>
          发布说明 (可选)
        </Typography.Text>
        <TextArea
          value={releaseNote}
          onChange={setReleaseNote}
          placeholder="简要说明本次版本更新的内容，这有助于后续的版本回溯..."
          rows={4}
        />
      </div>
    </Modal>
  );
}
