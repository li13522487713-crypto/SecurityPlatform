import { Empty, Space, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowBreakingChange } from "../versions/microflow-version-types";

const { Text } = Typography;

function severityColor(severity: MicroflowBreakingChange["severity"]): "blue" | "orange" | "red" {
  if (severity === "high") {
    return "red";
  }
  if (severity === "medium") {
    return "orange";
  }
  return "blue";
}

export function PublishBreakingChanges({ changes }: { changes: MicroflowBreakingChange[] }) {
  if (changes.length === 0) {
    return <Empty title="无破坏性变更" description="当前版本与最新发布快照兼容。" />;
  }
  return (
    <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
      <Text strong>破坏性变更</Text>
      {changes.map(change => (
        <div key={change.id} style={{ width: "100%", border: "1px solid var(--semi-color-border)", borderRadius: 8, padding: 10 }}>
          <Space vertical align="start" spacing={4}>
            <Space wrap>
              <Tag color={severityColor(change.severity)}>{change.severity}</Tag>
              <Tag>{change.code}</Tag>
              {change.fieldPath ? <Text type="tertiary" size="small">{change.fieldPath}</Text> : null}
            </Space>
            <Text>{change.message}</Text>
            {(change.before || change.after) ? <Text type="tertiary" size="small">before: {change.before ?? "-"} · after: {change.after ?? "-"}</Text> : null}
          </Space>
        </div>
      ))}
    </Space>
  );
}
