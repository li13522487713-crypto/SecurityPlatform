import { Space, Typography } from "@douyinfe/semi-ui";
import type { ExpressionDiagnostic } from "../../expressions";

const { Text } = Typography;

export function ExpressionDiagnostics({ diagnostics, onDiagnosticClick }: { diagnostics: ExpressionDiagnostic[]; onDiagnosticClick?: (diagnostic: ExpressionDiagnostic) => void }) {
  if (!diagnostics.length) {
    return null;
  }
  return (
    <Space vertical align="start" spacing={2}>
      {diagnostics.map(diagnostic => (
        <Text
          key={diagnostic.id}
          size="small"
          type={diagnostic.severity === "error" ? "danger" : diagnostic.severity === "warning" ? "warning" : "tertiary"}
          onClick={() => onDiagnosticClick?.(diagnostic)}
          style={{ cursor: onDiagnosticClick ? "pointer" : undefined }}
        >
          {diagnostic.range ? `[${diagnostic.range.start}-${diagnostic.range.end}] ` : ""}{diagnostic.message}
        </Text>
      ))}
    </Space>
  );
}
