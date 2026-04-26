import { Space, Typography } from "@douyinfe/semi-ui";
import type { ExpressionDiagnostic } from "../../expressions";

const { Text } = Typography;

export function ExpressionDiagnostics({ diagnostics }: { diagnostics: ExpressionDiagnostic[] }) {
  if (!diagnostics.length) {
    return null;
  }
  return (
    <Space vertical align="start" spacing={2}>
      {diagnostics.map(diagnostic => (
        <Text key={diagnostic.id} size="small" type={diagnostic.severity === "error" ? "danger" : diagnostic.severity === "warning" ? "warning" : "tertiary"}>
          {diagnostic.message}
        </Text>
      ))}
    </Space>
  );
}
