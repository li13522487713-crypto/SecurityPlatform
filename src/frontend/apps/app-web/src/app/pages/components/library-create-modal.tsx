import { useCallback, useEffect, useState } from "react";
import { Button, Form, Input, Modal, Select, Space, TextArea, Toast, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import {
  chatflowEditorPath,
  orgWorkspaceDatabaseDetailPath,
  orgWorkspaceKnowledgeBaseDetailPath,
  orgWorkspacePluginDetailPath,
  workflowEditorPath
} from "@atlas/app-shell-shared";
import { createKnowledgeBase } from "../../../services/api-knowledge";
import { createAiDatabase, AiDatabaseChannelScope, AiDatabaseQueryMode } from "../../../services/api-ai-database";
import { createAiPlugin } from "../../../services/api-explore";
import { createWorkflow } from "../../../services/api-workflow";
import { createVoiceAsset } from "../../../services/api-ai-voice";
import { createAgentCard } from "../../../services/api-ai-card";
import { createLongTermMemoryItem } from "../../../services/api-ai-memory";
import type { LibraryResourceType } from "../../../services/api-ai-workspace";
import { useAppI18n } from "../../i18n";
import { useWorkspaceContext } from "../../workspace-context";

export interface LibraryCreateModalProps {
  visible: boolean;
  createType: LibraryResourceType | null;
  onClose: () => void;
  onCreated: () => void;
}

type KbKind = 0 | 1 | 2;
type DatabaseWizardStep = "base" | "table";

interface DatabaseFieldDraft {
  name: string;
  description: string;
  type: string;
  required: boolean;
}

const DATABASE_FIELD_TYPE_OPTIONS = [
  { value: "string", label: "文本" },
  { value: "number", label: "数字" },
  { value: "integer", label: "整数" },
  { value: "boolean", label: "布尔" },
  { value: "date", label: "日期" },
  { value: "json", label: "对象" },
  { value: "array", label: "数组" }
];

function createDefaultDatabaseFields(): DatabaseFieldDraft[] {
  return [
    { name: "name", description: "名称", type: "string", required: false }
  ];
}

export function LibraryCreateModal({ visible, createType, onClose, onCreated }: LibraryCreateModalProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const [submitting, setSubmitting] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [kbType, setKbType] = useState<KbKind>(0);
  const [flowMode, setFlowMode] = useState<"workflow" | "chatflow">("workflow");
  const [voiceLanguage, setVoiceLanguage] = useState("zh-CN");
  const [voiceGender, setVoiceGender] = useState("");
  const [databaseStep, setDatabaseStep] = useState<DatabaseWizardStep>("base");
  const [databaseFields, setDatabaseFields] = useState<DatabaseFieldDraft[]>(createDefaultDatabaseFields);

  useEffect(() => {
    if (!visible) {
      return;
    }
    setName("");
    setDescription("");
    setKbType(0);
    setFlowMode("workflow");
    setVoiceLanguage("zh-CN");
    setVoiceGender("");
    setDatabaseStep("base");
    setDatabaseFields(createDefaultDatabaseFields());
  }, [visible, createType]);

  const workspaceId = workspace.id;
  const orgId = workspace.orgId;

  const handleClose = useCallback(() => {
    if (createType === "database" && databaseStep === "table" && !submitting) {
      setDatabaseStep("base");
      return;
    }

    onClose();
  }, [createType, databaseStep, onClose, submitting]);

  const handleSubmit = useCallback(async () => {
    if (!createType) {
      return;
    }
    const trimmed = name.trim();
    if (!trimmed) {
      Toast.warning(t("cozeLibraryCreateNameRequired"));
      return;
    }

    if (createType === "workflow" && !description.trim()) {
      Toast.warning(t("cozeLibraryCreateDescriptionRequired"));
      return;
    }

    if (createType === "database" && databaseStep === "base") {
      setDatabaseStep("table");
      return;
    }

    setSubmitting(true);
    try {
      switch (createType) {
        case "knowledge-base": {
          const id = await createKnowledgeBase({
            name: trimmed,
            description: description.trim() || undefined,
            type: kbType,
            workspaceId: Number(workspaceId)
          });
          Toast.success(t("cozeLibraryCreateSuccess"));
          onCreated();
          onClose();
          navigate(orgWorkspaceKnowledgeBaseDetailPath(orgId, workspaceId, id));
          return;
        }
        case "database": {
          const normalizedFields = databaseFields
            .map((field, index) => ({
              name: field.name.trim(),
              description: field.description.trim() || undefined,
              type: field.type || "string",
              required: field.required,
              indexed: false,
              sortOrder: index
            }))
            .filter(field => field.name);

          if (normalizedFields.length === 0) {
            Toast.warning(t("cozeLibraryCreateDatabaseFieldsRequired"));
            return;
          }

          const fieldNames = new Set<string>();
          for (const field of normalizedFields) {
            if (fieldNames.has(field.name)) {
              Toast.warning(t("cozeLibraryCreateDatabaseDuplicateField"));
              return;
            }

            fieldNames.add(field.name);
          }

          const id = await createAiDatabase({
            name: trimmed,
            description: description.trim() || undefined,
            workspaceId,
            fields: normalizedFields,
            queryMode: AiDatabaseQueryMode.SingleUser,
            channelScope: AiDatabaseChannelScope.ChannelIsolated
          });
          Toast.success(t("cozeLibraryCreateSuccess"));
          onCreated();
          onClose();
          navigate(orgWorkspaceDatabaseDetailPath(orgId, workspaceId, id));
          return;
        }
        case "workflow": {
          const res = await createWorkflow({
            name: trimmed,
            description: description.trim(),
            mode: flowMode === "chatflow" ? 1 : 0,
            workspaceId
          });
          if (!res.success || !res.data) {
            throw new Error(res.message || t("cozeLibraryQueryFailed"));
          }
          const wid = res.data;
          Toast.success(t("cozeLibraryCreateSuccess"));
          onCreated();
          onClose();
          navigate(flowMode === "chatflow" ? chatflowEditorPath(wid) : workflowEditorPath(wid));
          return;
        }
        case "plugin": {
          const id = await createAiPlugin({
            name: trimmed,
            description: description.trim() || undefined,
            type: 0,
            sourceType: 0,
            authType: 0,
            definitionJson: "{}",
            workspaceId: String(Number(workspaceId) || workspaceId)
          });
          Toast.success(t("cozeLibraryCreateSuccess"));
          onCreated();
          onClose();
          navigate(orgWorkspacePluginDetailPath(orgId, workspaceId, id));
          return;
        }
        case "voice": {
          await createVoiceAsset({
            name: trimmed,
            description: description.trim() || undefined,
            language: voiceLanguage.trim() || undefined,
            gender: voiceGender.trim() || undefined
          });
          Toast.success(t("cozeLibraryCreateSuccess"));
          onCreated();
          onClose();
          return;
        }
        case "card": {
          try {
            await createAgentCard({ name: trimmed, description: description.trim() || undefined });
          } catch (e) {
            if ((e as Error).message === "AGENT_CARD_CREATE_NOT_AVAILABLE") {
              Toast.info(t("cozeLibraryCreateNotSupported"));
            } else {
              throw e;
            }
          }
          onClose();
          return;
        }
        case "memory": {
          try {
            await createLongTermMemoryItem({ memoryKey: trimmed, content: description.trim() || undefined });
          } catch (e) {
            if ((e as Error).message === "LONG_TERM_MEMORY_CREATE_NOT_AVAILABLE") {
              Toast.info(t("cozeLibraryCreateNotSupported"));
            } else {
              throw e;
            }
          }
          onClose();
          return;
        }
        case "prompt": {
          Toast.info(t("cozeLibraryCreateNotSupported"));
          onClose();
          return;
        }
        default: {
          Toast.info(t("cozeLibraryCreateNotSupported"));
        }
      }
    } catch (error) {
      Toast.error((error as Error).message || t("cozeLibraryQueryFailed"));
    } finally {
      setSubmitting(false);
    }
  }, [
    createType,
    databaseFields,
    databaseStep,
    description,
    flowMode,
    kbType,
    name,
    navigate,
    onClose,
    onCreated,
    orgId,
    t,
    voiceGender,
    voiceLanguage,
    workspaceId
  ]);

  if (!createType) {
    return null;
  }

  const title = t("cozeLibraryCreateTitle");
  const showKb = createType === "knowledge-base";
  const showFlow = createType === "workflow";
  const showVoice = createType === "voice";
  const showDatabaseWizard = createType === "database";
  const modalTitle = showDatabaseWizard
    ? t(databaseStep === "base" ? "cozeLibraryCreateDatabaseBaseTitle" : "cozeLibraryCreateDatabaseTableTitle")
    : title;
  const okText = showDatabaseWizard
    ? t(databaseStep === "base" ? "cozeCommonNext" : "cozeCommonConfirm")
    : t("cozeCommonConfirm");
  const cancelText = showDatabaseWizard
    ? t(databaseStep === "base" ? "cozeCommonCancel" : "cozeCommonBack")
    : t("cozeCommonCancel");

  return (
    <Modal
      title={modalTitle}
      visible={visible}
      onCancel={handleClose}
      onOk={() => void handleSubmit()}
      confirmLoading={submitting}
      okText={okText}
      cancelText={cancelText}
      width={showDatabaseWizard && databaseStep === "table" ? 920 : undefined}
    >
      <Form labelPosition="top">
        {showDatabaseWizard ? (
          <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            {databaseStep === "base" ? (
              <>
                <Typography.Text type="tertiary">
                  {t("cozeLibraryCreateDatabaseBaseHint")}
                </Typography.Text>
                <Form.Slot label={t("cozeLibraryCreateName")}>
                  <Input
                    value={name}
                    onChange={v => setName(v)}
                    placeholder={t("cozeLibraryCreateNameRequired")}
                  />
                </Form.Slot>
                <Form.Slot label={t("cozeLibraryCreateDescription")}>
                  <TextArea
                    value={description}
                    onChange={v => setDescription(v)}
                    rows={3}
                  />
                </Form.Slot>
              </>
            ) : (
              <>
                <Typography.Text type="tertiary">
                  {t("cozeLibraryCreateDatabaseTableHint")}
                </Typography.Text>
                {databaseFields.map((field, index) => (
                  <div
                    key={`${field.name}-${index}`}
                    style={{ display: "grid", gridTemplateColumns: "2fr 2fr 1.2fr 1fr auto", gap: 12 }}
                  >
                    <Input
                      value={field.name}
                      placeholder={t("cozeLibraryCreateDatabaseFieldName")}
                      onChange={value => setDatabaseFields(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, name: value } : item))}
                    />
                    <Input
                      value={field.description}
                      placeholder={t("cozeLibraryCreateDatabaseFieldDescription")}
                      onChange={value => setDatabaseFields(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, description: value } : item))}
                    />
                    <Select
                      value={field.type}
                      optionList={DATABASE_FIELD_TYPE_OPTIONS}
                      onChange={value => setDatabaseFields(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, type: String(value) } : item))}
                    />
                    <Select
                      value={field.required ? "true" : "false"}
                      optionList={[
                        { value: "false", label: t("cozeLibraryCreateDatabaseOptional") },
                        { value: "true", label: t("cozeLibraryCreateDatabaseRequired") }
                      ]}
                      onChange={value => setDatabaseFields(current => current.map((item, itemIndex) => itemIndex === index ? { ...item, required: String(value) === "true" } : item))}
                    />
                    <Button
                      type="danger"
                      theme="borderless"
                      disabled={databaseFields.length <= 1}
                      onClick={() => setDatabaseFields(current => current.filter((_, itemIndex) => itemIndex !== index))}
                    >
                      {t("cozeLibraryCreateDatabaseDeleteField")}
                    </Button>
                  </div>
                ))}
                <Space>
                  <Button
                    onClick={() => setDatabaseFields(current => [
                      ...current,
                      { name: "", description: "", type: "string", required: false }
                    ])}
                  >
                    {t("cozeLibraryCreateDatabaseAddField")}
                  </Button>
                </Space>
              </>
            )}
          </div>
        ) : (
          <>
            {showFlow ? (
              <Form.Slot label={t("cozeLibraryCreateWorkflowMode")}>
                <Select
                  value={flowMode}
                  onChange={v => setFlowMode(v as "workflow" | "chatflow")}
                  style={{ width: "100%" }}
                  optionList={[
                    { value: "workflow", label: t("cozeLibraryCreateModeWorkflow") },
                    { value: "chatflow", label: t("cozeLibraryCreateModeChatflow") }
                  ]}
                />
              </Form.Slot>
            ) : null}
            {showKb ? (
              <Form.Slot label={t("cozeLibraryCreateKbType")}>
                <Select
                  value={kbType}
                  onChange={v => setKbType(v as KbKind)}
                  style={{ width: "100%" }}
                  optionList={[
                    { value: 0, label: t("cozeLibrarySubTypeKbText") },
                    { value: 1, label: t("cozeLibrarySubTypeKbTable") },
                    { value: 2, label: t("cozeLibrarySubTypeKbImage") }
                  ]}
                />
              </Form.Slot>
            ) : null}
            <Form.Slot label={t("cozeLibraryCreateName")}>
              <Input
                value={name}
                onChange={v => setName(v)}
                placeholder={t("cozeLibraryCreateNameRequired")}
              />
            </Form.Slot>
            {showVoice ? (
              <>
                <Form.Slot label={t("cozeLibraryCreateVoiceLanguage")}>
                  <Input value={voiceLanguage} onChange={v => setVoiceLanguage(v)} />
                </Form.Slot>
                <Form.Slot label={t("cozeLibraryCreateVoiceGender")}>
                  <Input value={voiceGender} onChange={v => setVoiceGender(v)} />
                </Form.Slot>
              </>
            ) : null}
            <Form.Slot label={t("cozeLibraryCreateDescription")}>
              <TextArea
                value={description}
                onChange={v => setDescription(v)}
                rows={flowMode ? 3 : 2}
              />
            </Form.Slot>
          </>
        )}
      </Form>
    </Modal>
  );
}
