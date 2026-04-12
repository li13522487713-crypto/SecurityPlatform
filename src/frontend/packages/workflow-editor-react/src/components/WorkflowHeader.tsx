import { Button, Input, Space, Tag } from "antd";
import { ArrowLeftOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";

interface WorkflowHeaderProps {
  name: string;
  dirty: boolean;
  savedAt?: number | null;
  readOnly?: boolean;
  onNameChange: (value: string) => void;
  onBack: () => void;
  onDuplicate?: () => void;
  onSave: () => void;
  onPublish: () => void;
}

export function WorkflowHeader(props: WorkflowHeaderProps) {
  const { t } = useTranslation();
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
        <Space size={12}>
          <Button icon={<ArrowLeftOutlined />} onClick={props.onBack} data-testid="workflow.detail.title.back">
            {t("workflow.back")}
          </Button>
          <div className="wf-react-header-meta">
            <div className="wf-react-header-title">{t("workflow.title")}</div>
            <div className="wf-react-header-subtitle">{t("workflow.subtitle")}</div>
          </div>
        </Space>
        <div className="wf-react-header-name-wrap">
          <Input
            value={props.name}
            disabled={props.readOnly}
            onChange={(event) => props.onNameChange(event.target.value)}
            className="wf-react-name"
          />
          <Tag color={props.dirty ? "orange" : "green"} className="wf-react-save-tag">
            {saveText}
          </Tag>
        </div>
      </div>
      <Space size={8}>
        <Button
          disabled={props.readOnly || !props.onDuplicate}
          onClick={props.onDuplicate}
          data-testid="workflow.detail.title.duplicate"
        >
          {t("workflow.duplicate")}
        </Button>
        <Button disabled={props.readOnly} onClick={props.onSave} data-testid="workflow.detail.title.save-draft">
          {t("workflow.saveDraft")}
        </Button>
        <Button
          type="primary"
          disabled={props.readOnly}
          onClick={props.onPublish}
          data-testid="workflow-base-publish-button"
        >
          {t("workflow.publish")}
        </Button>
      </Space>
    </div>
  );
}

