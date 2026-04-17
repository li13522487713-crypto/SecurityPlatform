import { useState } from "react";
import { Form, Modal, Toast } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import {
  chatflowEditorPath,
  workflowEditorPath
} from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { createWorkflow } from "../../services/api-workflow";

export type CreateWorkflowMode = "workflow" | "chatflow";

interface CreateWorkflowModalProps {
  visible: boolean;
  mode: CreateWorkflowMode;
  workspaceId: string;
  onClose: () => void;
  onCreated?: (workflowId: string) => void;
}

interface FormValues {
  name: string;
  description?: string;
}

/**
 * 通用工作流 / 对话流创建弹窗。
 *
 * - mode="workflow" 时调用 createWorkflow({ mode: 0 })
 * - mode="chatflow" 时调用 createWorkflow({ mode: 1 })
 *
 * 创建成功后跳转到对应编辑器路由。
 */
export function CreateWorkflowModal({
  visible,
  mode,
  workspaceId,
  onClose,
  onCreated
}: CreateWorkflowModalProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState(false);
  const [values, setValues] = useState<FormValues>({ name: "", description: "" });

  const isChatflow = mode === "chatflow";
  const titleKey = isChatflow ? "cozeCreateChatflowTitle" : "cozeCreateWorkflowTitle";
  const namePlaceholderKey = isChatflow ? "cozeCreateChatflowNamePlaceholder" : "cozeCreateWorkflowNamePlaceholder";
  const descPlaceholderKey = isChatflow ? "cozeCreateChatflowDescPlaceholder" : "cozeCreateWorkflowDescPlaceholder";

  const handleSubmit = async () => {
    const name = values.name.trim();
    const description = values.description?.trim() || "";
    if (!name) {
      Toast.warning(t(namePlaceholderKey));
      return;
    }
    if (!description) {
      Toast.warning(t(descPlaceholderKey));
      return;
    }

    setSubmitting(true);
    try {
      const response = await createWorkflow({
        name,
        description,
        mode: isChatflow ? 1 : 0,
        workspaceId
      });
      if (!response.success || !response.data) {
        throw new Error(response.message || t("cozeCreateFailed"));
      }
      const workflowId = response.data;
      Toast.success(t("cozeCreateSuccess"));
      onCreated?.(workflowId);
      onClose();
      setValues({ name: "", description: "" });
      navigate(isChatflow ? chatflowEditorPath(workflowId) : workflowEditorPath(workflowId));
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title={t(titleKey)}
      visible={visible}
      onCancel={() => {
        if (!submitting) {
          onClose();
        }
      }}
      onOk={() => void handleSubmit()}
      okText={t("homeEnter")}
      confirmLoading={submitting}
      okButtonProps={{ disabled: !values.name.trim() || !(values.description ?? "").trim() }}
      maskClosable={!submitting}
      width={560}
      data-testid={`coze-create-${mode}-modal`}
    >
      <Form
        labelPosition="top"
        labelWidth="100%"
        initValues={values}
        onValueChange={next => setValues(next as FormValues)}
      >
        <Form.Input
          field="name"
          label={t("cozeCreateWorkflowNameLabel")}
          placeholder={t(namePlaceholderKey)}
          maxLength={30}
          showClear
          required
        />
        <Form.TextArea
          field="description"
          label={t("cozeCreateWorkflowDescLabel")}
          placeholder={t(descPlaceholderKey)}
          maxLength={600}
          rows={5}
          required
        />
      </Form>
    </Modal>
  );
}
