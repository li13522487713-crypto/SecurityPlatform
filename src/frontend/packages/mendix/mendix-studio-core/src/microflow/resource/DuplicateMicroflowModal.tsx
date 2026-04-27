import { useEffect, useState } from "react";
import { Input, Modal, Space, Toast } from "@douyinfe/semi-ui";

import { getMicroflowErrorUserMessage } from "../adapter/http/microflow-api-error";
import type { MicroflowDuplicateInput, MicroflowResource } from "./resource-types";

type DuplicateMicroflowSource = Pick<MicroflowResource, "name" | "displayName" | "moduleId"> & Partial<Pick<MicroflowResource, "moduleName" | "tags">>;

interface DuplicateMicroflowModalProps {
  resource?: DuplicateMicroflowSource;
  visible: boolean;
  onClose: () => void;
  onSubmit: (input: MicroflowDuplicateInput) => Promise<void>;
}

export function DuplicateMicroflowModal({ resource, visible, onClose, onSubmit }: DuplicateMicroflowModalProps) {
  const [name, setName] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [moduleId, setModuleId] = useState("");
  const [tags, setTags] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (visible && resource) {
      setName(`${resource.name}_Copy`);
      setDisplayName(`${resource.displayName || resource.name} Copy`);
      setModuleId(resource.moduleId);
      setTags((resource.tags ?? []).join(", "));
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
        displayName: displayName.trim() || name.trim(),
        moduleId: moduleId.trim() || resource?.moduleId,
        moduleName: moduleId.trim() || resource?.moduleName,
        tags: tags.split(",").map(tag => tag.trim()).filter(Boolean)
      });
      onClose();
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal visible={visible} title="复制微流" onCancel={onClose} onOk={() => void handleSubmit()} confirmLoading={submitting}>
      <Space vertical style={{ width: "100%" }}>
        <Input value={name} onChange={setName} prefix="Name" />
        <Input value={displayName} onChange={setDisplayName} prefix="显示名称" />
        <Input value={moduleId} onChange={setModuleId} prefix="模块" />
        <Input value={tags} onChange={setTags} prefix="标签" />
      </Space>
    </Modal>
  );
}
