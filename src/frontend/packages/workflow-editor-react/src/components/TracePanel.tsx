import { Button, Table, Tag } from "antd";
import { useTranslation } from "react-i18next";

export interface TraceStepItem {
  timestamp: string;
  nodeKey: string;
  status: "running" | "success" | "failed" | "skipped" | "blocked";
  detail?: string;
}

interface TracePanelProps {
  visible: boolean;
  steps: TraceStepItem[];
  onClose: () => void;
}

const STATUS_COLOR: Record<TraceStepItem["status"], string> = {
  running: "processing",
  success: "success",
  failed: "error",
  skipped: "default",
  blocked: "warning"
};

export function TracePanel(props: TracePanelProps) {
  const { t } = useTranslation();
  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-trace-panel">
      <div className="wf-react-test-header">
        <div className="wf-react-panel-title">{t("wfUi.trace.title")}</div>
        <Button size="small" onClick={props.onClose}>
          {t("wfUi.trace.close")}
        </Button>
      </div>
      <Table
        size="small"
        rowKey={(item) => `${item.timestamp}-${item.nodeKey}-${item.status}`}
        dataSource={props.steps}
        pagination={false}
        columns={[
          { title: t("wfUi.trace.time"), dataIndex: "timestamp", width: 90 },
          { title: t("wfUi.trace.node"), dataIndex: "nodeKey", width: 180, ellipsis: true },
          {
            title: t("wfUi.trace.status"),
            dataIndex: "status",
            width: 100,
            render: (value: TraceStepItem["status"]) => <Tag color={STATUS_COLOR[value]}>{t(`wfUi.status.${value}`)}</Tag>
          },
          { title: t("wfUi.trace.detail"), dataIndex: "detail", ellipsis: true }
        ]}
      />
    </div>
  );
}
