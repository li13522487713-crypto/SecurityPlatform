import { Space, Typography } from "@douyinfe/semi-ui";
import type { MicroflowValidationIssue } from "../../schema";
import { dedupeIssues, presentIssueMessage } from "./issue-presenter";

const { Text } = Typography;

export function FieldError({ issues }: { issues?: MicroflowValidationIssue[] }) {
  const deduped = dedupeIssues(issues ?? []);
  if (!deduped.length) {
    return null;
  }
  return (
    <Space vertical align="start" spacing={2}>
      {deduped.map(issue => (
        <Text key={`${issue.id}:${issue.fieldPath ?? ""}`} type={issue.severity === "error" ? "danger" : "warning"} size="small">
          {presentIssueMessage(issue)}
        </Text>
      ))}
    </Space>
  );
}
