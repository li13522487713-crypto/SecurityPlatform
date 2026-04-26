import { Space, Typography } from "@douyinfe/semi-ui";
import type { MicroflowValidationIssue } from "../../schema";

const { Text } = Typography;

export function FieldError({ issues }: { issues?: MicroflowValidationIssue[] }) {
  if (!issues?.length) {
    return null;
  }
  return (
    <Space vertical align="start" spacing={2}>
      {issues.map(issue => (
        <Text key={issue.id} type={issue.severity === "error" ? "danger" : "warning"} size="small">
          {issue.message}
        </Text>
      ))}
    </Space>
  );
}
