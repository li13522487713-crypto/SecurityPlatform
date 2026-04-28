import { Space, Tag, Typography } from "@douyinfe/semi-ui";
import type { MicroflowValidationIssue } from "../../schema";

const { Text } = Typography;

export function ValidationIssueList({ issues }: { issues: MicroflowValidationIssue[] }) {
  if (!issues.length) {
    return null;
  }
  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      {issues.map(issue => (
        <div key={issue.id} style={{ display: "grid", gridTemplateColumns: "auto minmax(0, 1fr)", gap: 8, width: "100%" }}>
          <Tag color={issue.severity === "error" ? "red" : issue.severity === "warning" ? "orange" : "blue"}>
            {issue.code}
          </Tag>
          <Text size="small" type={issue.severity === "error" ? "danger" : "secondary"}>
            {issue.message}
          </Text>
        </div>
      ))}
    </Space>
  );
}
