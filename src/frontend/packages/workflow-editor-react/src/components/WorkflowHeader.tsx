import { Button, Input, Space, Tag } from "antd";
import { ArrowLeftOutlined, CopyOutlined, SaveOutlined, UploadOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";

interface WorkflowHeaderProps {
  name: string;
  dirty: boolean;
  savedAt?: number | null;
  readOnly?: boolean;
  mode?: "workflow" | "chatflow";
  onNameChange: (value: string) => void;
  onBack: () => void;
  onDuplicate?: () => void;
  onSave: () => void;
  onPublish: () => void;
}

export function WorkflowHeader(props: WorkflowHeaderProps) {
  const { t } = useTranslation();
  const isChatflow = props.mode === "chatflow";
  const saveText =
    props.dirty || !props.savedAt
      ? t("workflow.editorUnsaved")
      : t("workflow.autosaveAt", {
          time: new Intl.DateTimeFormat(undefined, {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit"
          }).format(new Date(props.savedAt))
        });

  return (
    <div className="wf-react-header">
      <div className="wf-react-header-main">
        <button type="button" className="wf-react-header-back" onClick={props.onBack} data-testid="workflow.detail.title.back">
          <ArrowLeftOutlined />
        </button>
        <div className="wf-react-header-badge">{isChatflow ? "CF" : "WF"}</div>
        <div className="wf-react-header-meta">
          <div className="wf-react-header-title-row">
            <div className="wf-react-header-title">{props.name || t("workflow.title")}</div>
            <Tag color="blue" className="wf-react-mode-tag">
              {isChatflow ? "Chatflow" : t("workflow.title")}
            </Tag>
          </div>
          <div className="wf-react-header-subtitle">
            {isChatflow ? "面向对话流的连续编排、调试与发布" : t("workflow.subtitle")}
          </div>
        </div>
        <div className="wf-react-header-name-wrap">
          <Input
            value={props.name}
            disabled={props.readOnly}
            onChange={(event) => props.onNameChange(event.target.value)}
            className="wf-react-name"
            data-testid="workflow.detail.meta.name"
          />
          <Tag color={props.dirty ? "orange" : "green"} className="wf-react-save-tag">
            {saveText}
          </Tag>
        </div>
      </div>
      <Space size={8} className="wf-react-header-actions">
        <Button
          disabled={props.readOnly || !props.onDuplicate}
          icon={<CopyOutlined />}
          onClick={props.onDuplicate}
          data-testid="workflow.detail.title.duplicate"
        >
          {t("workflow.duplicate")}
        </Button>
        <Button disabled={props.readOnly} icon={<SaveOutlined />} onClick={props.onSave} data-testid="workflow.detail.title.save-draft">
          {t("workflow.saveDraft")}
        </Button>
        <Button
          type="primary"
          disabled={props.readOnly}
          icon={<UploadOutlined />}
          onClick={props.onPublish}
          data-testid="workflow-base-publish-button"
        >
          {t("workflow.publish")}
        </Button>
      </Space>
    </div>
  );
}

