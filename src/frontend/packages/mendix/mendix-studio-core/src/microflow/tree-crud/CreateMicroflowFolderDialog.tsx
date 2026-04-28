import { useEffect, useState } from "react";
import { Input, Modal, Space, Toast, Typography } from "@douyinfe/semi-ui";

import { getMicroflowErrorUserMessage } from "../adapter/http/microflow-api-error";
import type { MicroflowFolder } from "../folders/microflow-folder-types";

const { Text } = Typography;

export interface CreateMicroflowFolderDialogProps {
  visible: boolean;
  moduleId?: string;
  workspaceId?: string;
  parentFolderId?: string;
  parentFolderPath?: string;
  onClose: () => void;
  onSubmit: (input: { workspaceId?: string; moduleId: string; parentFolderId?: string; name: string }) => Promise<MicroflowFolder>;
}

export function CreateMicroflowFolderDialog({
  visible,
  moduleId,
  workspaceId,
  parentFolderId,
  parentFolderPath,
  onClose,
  onSubmit
}: CreateMicroflowFolderDialogProps) {
  const [name, setName] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (visible) {
      setName("");
      setError(undefined);
      setSubmitting(false);
    }
  }, [visible]);

  async function handleSubmit() {
    const trimmedName = name.trim();
    if (!moduleId) {
      setError("缺少模块上下文，无法创建文件夹。");
      return;
    }
    if (!/^[A-Za-z][A-Za-z0-9_ -]*$/u.test(trimmedName)) {
      setError("文件夹名称必须以字母开头，且只能包含字母、数字、空格、下划线和短横线。");
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit({ workspaceId, moduleId, parentFolderId, name: trimmedName });
      Toast.success("微流文件夹已创建");
      onClose();
    } catch (caught) {
      const message = getMicroflowErrorUserMessage(caught);
      setError(message);
      Toast.error(message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal
      visible={visible}
      title="新建微流文件夹"
      onCancel={onClose}
      onOk={() => void handleSubmit()}
      confirmLoading={submitting}
      okText="创建"
      cancelText="取消"
    >
      <Space vertical style={{ width: "100%" }}>
        <Text type="tertiary">位置：{parentFolderPath || "模块根目录"}</Text>
        <Input value={name} onChange={setName} prefix="名称" placeholder="Validation" />
        {error ? <Text type="danger" size="small">{error}</Text> : null}
      </Space>
    </Modal>
  );
}
