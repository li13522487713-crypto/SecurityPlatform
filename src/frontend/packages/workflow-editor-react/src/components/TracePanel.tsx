import { Button, Table, Tag } from "antd";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

export interface TraceStepItem {
  timestamp: string;
  nodeKey: string;
  status: "running" | "success" | "failed" | "skipped" | "blocked";
  detail?: string;
  nodeType?: string;
  durationMs?: number;
  errorMessage?: string;
  inputsJson?: string;
  outputsJson?: string;
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

function prettifyJson(value?: string): string {
  if (!value) {
    return "-";
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

export function TracePanel(props: TracePanelProps) {
  const { t } = useTranslation();
  const expandableRowKeys = useMemo(
    () =>
      props.steps
        .filter((step) => step.inputsJson || step.outputsJson || step.errorMessage)
        .map((step) => `${step.timestamp}-${step.nodeKey}-${step.status}`),
    [props.steps]
  );

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
        expandable={{
          rowExpandable: (record) => Boolean(record.inputsJson || record.outputsJson || record.errorMessage),
          defaultExpandedRowKeys: expandableRowKeys,
          expandedRowRender: (record) => (
            <div className="wf-react-trace-detail">
              <div className="wf-react-trace-detail-meta">
                <span><strong>{t("wfUi.trace.type")}:</strong> {record.nodeType || "-"}</span>
                <span><strong>{t("wfUi.trace.duration")}:</strong> {typeof record.durationMs === "number" ? `${record.durationMs}ms` : "-"}</span>
              </div>
              <div className="wf-react-trace-detail-grid">
                <div className="wf-react-trace-detail-block">
                  <strong>{t("wfUi.trace.inputs")}</strong>
                  <pre>{prettifyJson(record.inputsJson)}</pre>
                </div>
                <div className="wf-react-trace-detail-block">
                  <strong>{t("wfUi.trace.outputs")}</strong>
                  <pre>{prettifyJson(record.outputsJson)}</pre>
                </div>
              </div>
              {record.errorMessage ? (
                <div className="wf-react-trace-error">
                  <strong>{t("wfUi.trace.error")}:</strong> {record.errorMessage}
                </div>
              ) : null}
            </div>
          )
        }}
        columns={[
          { title: t("wfUi.trace.time"), dataIndex: "timestamp", width: 90 },
          { title: t("wfUi.trace.node"), dataIndex: "nodeKey", width: 180, ellipsis: true },
          {
            title: t("wfUi.trace.status"),
            dataIndex: "status",
            width: 110,
            render: (value: TraceStepItem["status"]) => <Tag color={STATUS_COLOR[value]}>{t(`wfUi.status.${value}`)}</Tag>
          },
          { title: t("wfUi.trace.detail"), dataIndex: "detail", ellipsis: true }
        ]}
      />
    </div>
  );
}
