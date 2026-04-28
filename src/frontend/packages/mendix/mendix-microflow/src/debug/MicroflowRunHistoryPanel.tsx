import { Button, Card, Empty, Select, Space, Spin, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowRunHistoryItem, MicroflowRunHistoryStatus } from "../runtime-adapter/types";

const { Text } = Typography;

export interface MicroflowRunHistoryPanelProps {
  items: MicroflowRunHistoryItem[];
  selectedRunId?: string;
  loading?: boolean;
  error?: string;
  statusFilter: "all" | MicroflowRunHistoryStatus;
  onChangeFilter: (status: "all" | MicroflowRunHistoryStatus) => void;
  onRefresh: () => void;
  onSelectRun: (runId: string) => void;
}

function colorForStatus(status: MicroflowRunHistoryItem["status"]): "green" | "red" | "orange" | "grey" {
  if (status === "success") {
    return "green";
  }
  if (status === "unsupported") {
    return "orange";
  }
  if (status === "cancelled") {
    return "grey";
  }
  return "red";
}

export function MicroflowRunHistoryPanel({
  items,
  selectedRunId,
  loading,
  error,
  statusFilter,
  onChangeFilter,
  onRefresh,
  onSelectRun,
}: MicroflowRunHistoryPanelProps) {
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Space style={{ width: "100%", justifyContent: "space-between" }}>
        <Space>
          <Select
            size="small"
            value={statusFilter}
            style={{ width: 140 }}
            optionList={[
              { label: "all", value: "all" },
              { label: "succeeded", value: "success" },
              { label: "failed", value: "failed" },
              { label: "unsupported", value: "unsupported" },
              { label: "cancelled", value: "cancelled" },
            ]}
            onChange={value => onChangeFilter(value as "all" | MicroflowRunHistoryStatus)}
          />
          <Button size="small" onClick={onRefresh}>Refresh</Button>
        </Space>
        <Text type="tertiary" size="small">{items.length} records</Text>
      </Space>
      {loading ? <Spin spinning /> : null}
      {error ? (
        <Empty
          title="Load run history failed"
          description={error}
        >
          <Button type="primary" onClick={onRefresh}>Retry</Button>
        </Empty>
      ) : null}
      {!loading && !error && items.length === 0 ? <Empty title="No runs yet" /> : null}
      {!error && items.map(item => (
        <Card
          key={item.runId}
          style={{ width: "100%", borderColor: selectedRunId === item.runId ? "var(--semi-color-primary)" : undefined }}
          bodyStyle={{ padding: 10 }}
        >
          <button
            type="button"
            style={{ border: "none", background: "transparent", textAlign: "left", width: "100%", cursor: "pointer", padding: 0 }}
            onClick={() => onSelectRun(item.runId)}
          >
            <Space style={{ width: "100%", justifyContent: "space-between" }}>
              <Space>
                <Tag color={colorForStatus(item.status)}>{item.status}</Tag>
                <Text strong>{item.runId}</Text>
              </Space>
              <Text type="tertiary" size="small">{item.durationMs}ms</Text>
            </Space>
            <Text type="tertiary" size="small">{item.startedAt}</Text>
            {item.errorMessage ? <><br /><Text type="danger" size="small">{item.errorMessage}</Text></> : null}
          </button>
        </Card>
      ))}
    </Space>
  );
}
