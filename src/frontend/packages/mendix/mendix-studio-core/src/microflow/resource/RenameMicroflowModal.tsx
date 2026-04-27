import { useEffect, useState } from "react";
import { Input, Modal, Space, Toast } from "@douyinfe/semi-ui";

import { getMicroflowErrorUserMessage } from "../adapter/http/microflow-api-error";
import type { MicroflowResource } from "./resource-types";

type RenameMicroflowSource = Pick<MicroflowResource, "name" | "displayName">;

interface RenameMicroflowModalProps {
  resource?: RenameMicroflowSource;
  visible: boolean;
  onClose: () => void;
  onSubmit: (name: string, displayName?: string) => Promise<void>;
}

export function RenameMicroflowModal({ resource, visible, onClose, onSubmit }: RenameMicroflowModalProps) {
  const [name, setName] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (visible && resource) {
      setName(resource.name);
      setDisplayName(resource.displayName);
    }
  }, [resource, visible]);

  async function handleSubmit() {
    const trimmedName = name.trim();
    if (!trimmedName) {
      Toast.warning("微流名称不能为空");
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit(trimmedName, displayName.trim() || trimmedName);
      onClose();
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="重命名微流" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting}>
      <Space vertical style={{ width: "100%" }}>
        <Input value={name} onChange={setName} prefix="Name" />
        <Input value={displayName} onChange={setDisplayName} prefix="显示名称" />
      </Space>
    </Modal>
  );
}
