import { useState } from "react";
import { Form, Modal, Toast } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import { createFolder } from "../../services/api-folders";

interface CreateFolderModalProps {
  visible: boolean;
  workspaceId: string;
  onClose: () => void;
  onCreated?: (folderId: string) => void;
}

interface FolderFormValues {
  name: string;
  description?: string;
}

export function CreateFolderModal({ visible, workspaceId, onClose, onCreated }: CreateFolderModalProps) {
  const { t } = useAppI18n();
  const [submitting, setSubmitting] = useState(false);
  const [values, setValues] = useState<FolderFormValues>({ name: "", description: "" });

  const handleSubmit = async () => {
    const trimmed = values.name.trim();
    if (!trimmed) {
      Toast.warning(t("cozeCreateFolderNamePlaceholder"));
      return;
    }
    setSubmitting(true);
    try {
      const result = await createFolder(workspaceId, {
        name: trimmed,
        description: values.description?.trim() || undefined
      });
      Toast.success(t("cozeCreateSuccess"));
      onCreated?.(result.folderId);
      setValues({ name: "", description: "" });
      onClose();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title={t("cozeCreateFolderTitle")}
      visible={visible}
      onCancel={onClose}
      onOk={() => void handleSubmit()}
      okText="确定"
      confirmLoading={submitting}
      maskClosable={!submitting}
      data-testid="coze-create-folder-modal"
    >
      <Form labelPosition="top" labelWidth="100%" onValueChange={next => setValues(next as FolderFormValues)} initValues={values}>
        <Form.Input
          field="name"
          label={t("cozeCreateFolderNameLabel")}
          placeholder={t("cozeCreateFolderNamePlaceholder")}
          maxLength={40}
          showClear
          required
        />
        <Form.TextArea
          field="description"
          label={t("cozeCreateFolderDescLabel")}
          placeholder={t("cozeCreateFolderDescPlaceholder")}
          maxLength={800}
          showCounter
          rows={4}
        />
      </Form>
    </Modal>
  );
}
