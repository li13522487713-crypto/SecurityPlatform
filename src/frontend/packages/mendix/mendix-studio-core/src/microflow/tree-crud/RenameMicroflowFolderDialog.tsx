import { useEffect, useState } from "react";
import { Input, Modal, Toast } from "@douyinfe/semi-ui";

import { getMicroflowErrorUserMessage } from "../adapter/http/microflow-api-error";
import type { MicroflowFolder } from "../folders/microflow-folder-types";

interface RenameMicroflowFolderDialogProps {
  visible: boolean;
  folder?: MicroflowFolder;
  onClose: () => void;
  onSubmit: (name: string) => Promise<void>;
}

export function RenameMicroflowFolderDialog({ visible, folder, onClose, onSubmit }: RenameMicroflowFolderDialogProps) {
  const [name, setName] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (visible) {
      setName(folder?.name ?? "");
    }
  }, [folder, visible]);

  async function handleSubmit() {
    const trimmed = name.trim();
    if (!trimmed) {
      Toast.warning("文件夹名称不能为空");
      return;
    }
    if (!/^[A-Za-z][A-Za-z0-9_ -]*$/u.test(trimmed)) {
      Toast.warning("文件夹名称必须以字母开头，且只能包含字母、数字、空格、下划线和短横线");
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit(trimmed);
      onClose();
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="重命名微流文件夹" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting} okText="保存">
      <Input value={name} onChange={setName} prefix="名称" placeholder="Validation" />
    </Modal>
  );
}
