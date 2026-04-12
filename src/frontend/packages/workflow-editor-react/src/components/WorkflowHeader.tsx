import { Button, Input, Space, Tag } from "antd";
import { ArrowLeftOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";

interface WorkflowHeaderProps {
  name: string;
  dirty: boolean;
  readOnly?: boolean;
  onNameChange: (value: string) => void;
  onBack: () => void;
  onDuplicate?: () => void;
  onSave: () => void;
  onPublish: () => void;
}

export function WorkflowHeader(props: WorkflowHeaderProps) {
  const { t } = useTranslation();
  return (
    <div className="wf-react-header">
      <Space size={12}>
        <Button icon={<ArrowLeftOutlined />} onClick={props.onBack} />
        <Input
          value={props.name}
          disabled={props.readOnly}
          onChange={(event) => props.onNameChange(event.target.value)}
          className="wf-react-name"
        />
        <Tag color={props.dirty ? "orange" : "green"}>{props.dirty ? t("workflow.editorUnsaved") : t("workflow.autosaveAt", { time: "now" })}</Tag>
      </Space>
      <Space size={8}>
        <Button
          disabled={props.readOnly || !props.onDuplicate}
          onClick={props.onDuplicate}
          data-testid="workflow.detail.title.duplicate"
        >
          复制
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

