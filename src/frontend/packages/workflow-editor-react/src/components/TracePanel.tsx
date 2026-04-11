import { Button, Table, Tag } from "antd";

export interface TraceStepItem {
  timestamp: string;
  nodeKey: string;
  status: "running" | "success" | "failed" | "skipped";
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
  skipped: "default"
};

export function TracePanel(props: TracePanelProps) {
  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-trace-panel">
      <div className="wf-react-test-header">
        <div>Trace</div>
        <Button size="small" onClick={props.onClose}>
          关闭
        </Button>
      </div>
      <Table
        size="small"
        rowKey={(item) => `${item.timestamp}-${item.nodeKey}-${item.status}`}
        dataSource={props.steps}
        pagination={false}
        columns={[
          { title: "时间", dataIndex: "timestamp", width: 90 },
          { title: "节点", dataIndex: "nodeKey", width: 180, ellipsis: true },
          {
            title: "状态",
            dataIndex: "status",
            width: 100,
            render: (value: TraceStepItem["status"]) => <Tag color={STATUS_COLOR[value]}>{value}</Tag>
          },
          { title: "详情", dataIndex: "detail", ellipsis: true }
        ]}
      />
    </div>
  );
}
