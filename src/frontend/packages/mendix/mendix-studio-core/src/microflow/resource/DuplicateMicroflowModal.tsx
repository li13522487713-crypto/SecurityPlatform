import { useEffect, useState } from "react";
import { Input, Modal, Space, Toast } from "@douyinfe/semi-ui";

import type { MicroflowDuplicateInput, MicroflowResource } from "./resource-types";

interface DuplicateMicroflowModalProps {
  resource?: MicroflowResource;
  visible: boolean;
  onClose: () => void;
  onSubmit: (input: MicroflowDuplicateInput) => Promise<void>;
}

export function DuplicateMicroflowModal({ resource, visible, onClose, onSubmit }: DuplicateMicroflowModalProps) {
  const [name, setName] = useState("");
  const [moduleId, setModuleId] = useState("");
  const [tags, setTags] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (visible && resource) {
      setName(`${resource.name}Copy`);
      setModuleId(resource.moduleId);
      setTags(resource.tags.join(", "));
    }
  }, [resource, visible]);

  async function handleSubmit() {
    if (!name.trim()) {
      Toast.warning("复制后的名称不能为空");
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit({
        name: name.trim(),
        displayName: `${resource?.displayName || resource?.name || "Microflow"} Copy`,
        moduleId: moduleId.trim() || resource?.moduleId,
        moduleName: moduleId.trim() || resource?.moduleName,
        tags: tags.split(",").map(tag => tag.trim()).filter(Boolean)
      });
      onClose();
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="复制微流" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting}>
      <Space vertical style={{ width: "100%" }}>
        <Input value={name} onChange={setName} prefix="Name" />
        <Input value={moduleId} onChange={setModuleId} prefix="模块" />
        <Input value={tags} onChange={setTags} prefix="标签" />
      </Space>
    </Modal>
  );
}
