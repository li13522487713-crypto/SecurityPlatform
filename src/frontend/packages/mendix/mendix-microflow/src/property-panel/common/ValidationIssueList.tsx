import { Space, Tag, Typography } from "@douyinfe/semi-ui";
import type { MicroflowValidationIssue } from "../../schema";
import { dedupeIssues, presentIssueMessage } from "./issue-presenter";

const { Text } = Typography;

export function ValidationIssueList({ issues }: { issues: MicroflowValidationIssue[] }) {
  const deduped = dedupeIssues(issues);
  if (!deduped.length) {
    return null;
  }
  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      {deduped.map(issue => (
        <div key={`${issue.id}:${issue.fieldPath ?? ""}`} style={{ display: "grid", gridTemplateColumns: "auto minmax(0, 1fr)", gap: 8, width: "100%" }}>
          <Tag color={issue.severity === "error" ? "red" : issue.severity === "warning" ? "orange" : "blue"}>
            {issue.severity}
          </Tag>
          <Text size="small" type={issue.severity === "error" ? "danger" : "secondary"}>
            {presentIssueMessage(issue)}
          </Text>
        </div>
      ))}
    </Space>
  );
}
