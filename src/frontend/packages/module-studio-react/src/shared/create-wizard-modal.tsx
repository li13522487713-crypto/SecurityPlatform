import React, { useState } from "react";
import { Modal, Input, TextArea, Space, Button, Typography, Select } from "@douyinfe/semi-ui";

export interface CreateWizardModalTexts {
  okText?: string;
  cancelText?: string;
  blankMode?: string;
  templateMode?: string;
  nameLabel?: string;
  descriptionLabel?: string;
  templateSelectLabel?: string;
  namePlaceholder?: string;
  descriptionPlaceholder?: string;
}

export interface CreateWizardModalProps {
  visible: boolean;
  title: string;
  resourceType: "agent" | "app" | "workflow" | "chatflow";
  onCancel: () => void;
  onSubmit: (values: { name: string; description: string; templateId?: string }) => Promise<void>;
  templates?: Array<{ id: string; name: string; description?: string }>;
  /** Optional UI strings for localization (defaults preserve legacy Chinese copy). */
  texts?: CreateWizardModalTexts;
}

const defaultTexts: Required<CreateWizardModalTexts> = {
  okText: "创建",
  cancelText: "取消",
  blankMode: "从空白创建",
  templateMode: "从模板创建",
  nameLabel: "名称",
  descriptionLabel: "描述 (可选)",
  templateSelectLabel: "选择模板",
  namePlaceholder: "输入名称",
  descriptionPlaceholder: "添加一些描述信息"
};

const fieldStyle: React.CSSProperties = { display: "flex", flexDirection: "column", gap: 8, marginBottom: 16 };

export function CreateWizardModal({
  visible,
  title,
  resourceType,
  onCancel,
  onSubmit,
  templates = [],
  texts: textsProp
}: CreateWizardModalProps) {
  const texts = { ...defaultTexts, ...textsProp };
  const [submitting, setSubmitting] = useState(false);
  const [mode, setMode] = useState<"blank" | "template">("blank");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [selectedTemplate, setSelectedTemplate] = useState<string | undefined>();

  const namePlaceholder =
    texts.namePlaceholder ||
    (resourceType === "agent"
      ? "输入智能体名称"
      : resourceType === "app"
        ? "输入应用名称"
        : resourceType === "chatflow"
          ? "输入 Chatflow 名称"
          : "输入工作流名称");

  const handleSubmit = async () => {
    if (!name.trim()) {
      return;
    }
    if (mode === "template" && templates.length > 0 && !selectedTemplate) {
      return;
    }

    setSubmitting(true);
    try {
      await onSubmit({
        name: name.trim(),
        description: description.trim(),
        templateId: mode === "template" ? selectedTemplate : undefined
      });
      setName("");
      setDescription("");
      setMode("blank");
      setSelectedTemplate(undefined);
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = () => {
    setName("");
    setDescription("");
    setMode("blank");
    setSelectedTemplate(undefined);
    onCancel();
  };

  return (
    <Modal
      title={title}
      visible={visible}
      onCancel={handleCancel}
      onOk={handleSubmit}
      okButtonProps={{
        loading: submitting,
        disabled: !name.trim() || (mode === "template" && templates.length > 0 && !selectedTemplate)
      }}
      okText={texts.okText}
      cancelText={texts.cancelText}
      width={600}
    >
      <div style={{ marginBottom: 24 }}>
        <Space spacing={16}>
          <Button theme={mode === "blank" ? "solid" : "light"} type="primary" onClick={() => setMode("blank")}>
            {texts.blankMode}
          </Button>
          {templates.length > 0 && (
            <Button theme={mode === "template" ? "solid" : "light"} type="primary" onClick={() => setMode("template")}>
              {texts.templateMode}
            </Button>
          )}
        </Space>
      </div>

      <div style={fieldStyle}>
        <Typography.Text strong>{texts.nameLabel}</Typography.Text>
        <Input value={name} onChange={setName} placeholder={namePlaceholder} />
      </div>
      <div style={fieldStyle}>
        <Typography.Text strong>{texts.descriptionLabel}</Typography.Text>
        <TextArea
          value={description}
          onChange={setDescription}
          placeholder={texts.descriptionPlaceholder}
          rows={3}
        />
      </div>

      {mode === "template" && templates.length > 0 ? (
        <div style={fieldStyle}>
          <Typography.Text strong>{texts.templateSelectLabel}</Typography.Text>
          <Select
            value={selectedTemplate}
            onChange={(val) => setSelectedTemplate(String(val))}
            style={{ width: "100%" }}
            optionList={templates.map((t) => ({ label: t.name, value: t.id }))}
            placeholder={texts.templateSelectLabel}
          />
        </div>
      ) : null}
    </Modal>
  );
}
