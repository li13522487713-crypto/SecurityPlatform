import { Button, Input, Space, Tag } from "antd";
import { ArrowLeftOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";

interface WorkflowHeaderProps {
  name: string;
  dirty: boolean;
  onNameChange: (value: string) => void;
  onBack: () => void;
  onSave: () => void;
  onPublish: () => void;
}

export function WorkflowHeader(props: WorkflowHeaderProps) {
  const { t } = useTranslation();
  return (
    <div className="wf-react-header">
      <Space size={12}>
        <Button icon={<ArrowLeftOutlined />} onClick={props.onBack} />
        <Input value={props.name} onChange={(event) => props.onNameChange(event.target.value)} className="wf-react-name" />
        <Tag color={props.dirty ? "orange" : "green"}>{props.dirty ? t("workflow.editorUnsaved") : t("workflow.autosaveAt", { time: "now" })}</Tag>
      </Space>
      <Space size={8}>
        <Button onClick={props.onSave}>{t("workflow.saveDraft")}</Button>
        <Button type="primary" onClick={props.onPublish}>
          {t("workflow.publish")}
        </Button>
      </Space>
    </div>
  );
}

