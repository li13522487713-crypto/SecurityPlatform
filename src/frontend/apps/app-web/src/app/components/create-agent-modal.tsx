import { useMemo, useState } from "react";
import { Avatar, Form, Modal, TabPane, Tabs, TextArea, Toast } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { createAiAssistantInWorkspace } from "../../services/api-ai-assistant";
import { agentEditorPath } from "@atlas/app-shell-shared";
import { notifyWorkspaceResourceCreated } from "../workspace-resource-events";

interface CreateAgentModalProps {
  visible: boolean;
  workspaceId: string;
  onClose: () => void;
  onCreated?: (agentId: string) => void;
}

interface StandardFormValues {
  name: string;
  description?: string;
}

export function CreateAgentModal({ visible, workspaceId, onClose, onCreated }: CreateAgentModalProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const [tab, setTab] = useState<"standard" | "ai">("standard");
  const [submitting, setSubmitting] = useState(false);
  const [standardValues, setStandardValues] = useState<StandardFormValues>({ name: "", description: "" });
  const [aiPrompt, setAiPrompt] = useState("");

  const isAiMode = tab === "ai";
  const submitDisabled = useMemo(() => {
    if (isAiMode) {
      return !aiPrompt.trim();
    }
    return !standardValues.name.trim();
  }, [aiPrompt, isAiMode, standardValues.name]);

  const reset = () => {
    setStandardValues({ name: "", description: "" });
    setAiPrompt("");
    setTab("standard");
  };

  const submitStandard = async () => {
    const trimmed = standardValues.name.trim();
    if (!trimmed) {
      Toast.warning(t("cozeCreateAgentNamePlaceholder"));
      return;
    }
    if (!workspaceId || Number(workspaceId) <= 0) {
      Toast.error(t("cozeCreateFailed"));
      return;
    }
    setSubmitting(true);
    try {
      const agentId = await createAiAssistantInWorkspace(
        {
          name: trimmed,
          description: standardValues.description?.trim() || undefined
        },
        workspaceId
      );
      notifyWorkspaceResourceCreated({
        workspaceId,
        resourceType: "agent",
        resourceId: agentId,
        resourceName: trimmed
      });
      Toast.success(t("cozeCreateSuccess"));
      onCreated?.(agentId);
      onClose();
      reset();
      navigate(agentEditorPath(agentId));
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  const submitAi = async () => {
    const trimmed = aiPrompt.trim();
    if (!trimmed) {
      Toast.warning(t("cozeCreateAgentAiPromptPlaceholder"));
      return;
    }
    if (!workspaceId || Number(workspaceId) <= 0) {
      Toast.error(t("cozeCreateFailed"));
      return;
    }
    setSubmitting(true);
    try {
      // 第一阶段：AI 创建链路尚未接通后端，先作为标准创建落库占位
      // （第二阶段对接 generateByAiAssistant 流程后再补完整 LLM 生成）。
      const agentId = await createAiAssistantInWorkspace(
        {
          name: trimmed.slice(0, 40),
          description: trimmed.slice(0, 200)
        },
        workspaceId
      );
      notifyWorkspaceResourceCreated({
        workspaceId,
        resourceType: "agent",
        resourceId: agentId,
        resourceName: trimmed.slice(0, 40)
      });
      Toast.success(t("cozeCreateSuccess"));
      onCreated?.(agentId);
      onClose();
      reset();
      navigate(agentEditorPath(agentId));
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmit = () => {
    if (isAiMode) {
      void submitAi();
    } else {
      void submitStandard();
    }
  };

  return (
    <Modal
      title={t("cozeCreateAgentTitle")}
      visible={visible}
      onCancel={() => {
        if (!submitting) {
          onClose();
        }
      }}
      onOk={handleSubmit}
      okText={isAiMode ? t("cozeCreateAgentAiGenerate") : t("homeEnter")}
      confirmLoading={submitting}
      okButtonProps={{ disabled: submitDisabled }}
      maskClosable={!submitting}
      width={620}
      data-testid="coze-create-agent-modal"
    >
      <Tabs type="line" activeKey={tab} onChange={key => setTab((key as "standard" | "ai") ?? "standard")}>
        <TabPane tab={t("cozeCreateAgentTabStandard")} itemKey="standard">
          <Form
            labelPosition="top"
            labelWidth="100%"
            initValues={standardValues}
            onValueChange={next => setStandardValues(next as StandardFormValues)}
          >
            <Form.Input
              field="name"
              label={t("cozeCreateAgentNameLabel")}
              placeholder={t("cozeCreateAgentNamePlaceholder")}
              maxLength={50}
              showClear
              required
            />
            <Form.TextArea
              field="description"
              label={t("cozeCreateAgentDescLabel")}
              placeholder={t("cozeCreateAgentDescPlaceholder")}
              maxLength={500}
              rows={4}
            />
          </Form>
          <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 16 }}>
            <Avatar size="default" color="light-blue">
              {(standardValues.name || "A").slice(0, 1).toUpperCase()}
            </Avatar>
            <span style={{ color: "var(--semi-color-text-2)" }}>{t("cozeCreateAgentIconLabel")}</span>
          </div>
        </TabPane>
        <TabPane tab={t("cozeCreateAgentTabAi")} itemKey="ai">
          <TextArea
            placeholder={t("cozeCreateAgentAiPromptPlaceholder")}
            maxCount={500}
            rows={6}
            value={aiPrompt}
            onChange={value => setAiPrompt(String(value))}
          />
        </TabPane>
      </Tabs>
    </Modal>
  );
}
