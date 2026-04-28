import { useState } from "react";
import { Form, Modal, Toast } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { createLowcodeProjectAppGateway } from "../gateways/project-app-gateway";
import { notifyWorkspaceResourceCreated } from "../workspace-resource-events";

interface CreateAppModalProps {
  visible: boolean;
  workspaceId: string;
  onClose: () => void;
  onCreated?: (appId: string) => void;
}

interface AppFormValues {
  name: string;
  description?: string;
}

export function CreateAppModal({ visible, workspaceId, onClose, onCreated }: CreateAppModalProps) {
  const { t, locale } = useAppI18n();
  const navigate = useNavigate();
  const appGateway = createLowcodeProjectAppGateway({ navigate });
  const [submitting, setSubmitting] = useState(false);
  const [values, setValues] = useState<AppFormValues>({ name: "", description: "" });

  const handleSubmit = async () => {
    const trimmed = values.name.trim();
    if (!trimmed) {
      Toast.warning(t("cozeCreateAppNamePlaceholder"));
      return;
    }
    setSubmitting(true);
    try {
      const result = await appGateway.create({
        name: trimmed,
        description: values.description?.trim() || undefined,
        workspaceId,
        locale
      });
      notifyWorkspaceResourceCreated({
        workspaceId,
        resourceType: "app",
        resourceId: result.appId,
        resourceName: trimmed
      });
      Toast.success(t("cozeCreateSuccess"));
      onCreated?.(result.appId);
      onClose();
      setValues({ name: "", description: "" });
      appGateway.open(result.appId);
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title={t("cozeCreateAppTitle")}
      visible={visible}
      onCancel={() => {
        if (!submitting) {
          onClose();
        }
      }}
      onOk={() => void handleSubmit()}
      okText={t("homeEnter")}
      confirmLoading={submitting}
      okButtonProps={{ disabled: !values.name.trim() }}
      maskClosable={!submitting}
      width={560}
      data-testid="coze-create-app-modal"
    >
      <Form
        labelPosition="top"
        labelWidth="100%"
        initValues={values}
        onValueChange={next => setValues(next as AppFormValues)}
      >
        <Form.Input
          field="name"
          label={t("cozeCreateAppNameLabel")}
          placeholder={t("cozeCreateAppNamePlaceholder")}
          maxLength={50}
          showClear
          required
        />
        <Form.TextArea
          field="description"
          label={t("cozeCreateAppDescLabel")}
          placeholder={t("cozeCreateAppDescPlaceholder")}
          maxLength={500}
          rows={4}
        />
      </Form>
    </Modal>
  );
}
