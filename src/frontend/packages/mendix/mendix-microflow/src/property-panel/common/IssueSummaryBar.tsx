import { Button, Space, Typography } from "@douyinfe/semi-ui";
import { useMemo, useState } from "react";

import type { MicroflowValidationIssue } from "../../schema";
import { dedupeIssues, presentIssueMessage } from "./issue-presenter";

const { Text } = Typography;

export function IssueSummaryBar({
  issues,
  onLocateField,
}: {
  issues: MicroflowValidationIssue[];
  onLocateField?: (fieldPath?: string) => void;
}) {
  const deduped = useMemo(() => dedupeIssues(issues), [issues]);
  const [expanded, setExpanded] = useState(false);

  const errors = deduped.filter(issue => issue.severity === "error");
  const warnings = deduped.filter(issue => issue.severity === "warning");

  if (!errors.length && !warnings.length) {
    return null;
  }

  return (
    <div
      style={{
        margin: "12px 14px 0",
        padding: "8px 10px",
        borderRadius: 8,
        border: "1px solid rgba(249, 57, 32, 0.24)",
        background: "rgba(254, 245, 244, 0.9)",
        display: "grid",
        gap: 6,
      }}
    >
      <Space align="center" style={{ justifyContent: "space-between" }}>
        <Space align="center" spacing={8}>
          {errors.length > 0 ? <Text type="danger" size="small">✕ {errors.length} errors</Text> : null}
          {warnings.length > 0 ? <Text type="warning" size="small">⚠ {warnings.length} warnings</Text> : null}
        </Space>
        <Button theme="borderless" size="small" onClick={() => setExpanded(value => !value)}>
          {expanded ? "收起" : "展开"}
        </Button>
      </Space>
      {expanded ? (
        <div style={{ display: "grid", gap: 4 }}>
          {deduped.map(issue => (
            <button
              key={`${issue.id}:${issue.fieldPath ?? ""}`}
              type="button"
              style={{
                border: 0,
                padding: 0,
                margin: 0,
                textAlign: "left",
                background: "transparent",
                cursor: issue.fieldPath ? "pointer" : "default",
              }}
              onClick={() => onLocateField?.(issue.fieldPath)}
            >
              <Text size="small" type={issue.severity === "error" ? "danger" : issue.severity === "warning" ? "warning" : "tertiary"}>
                {presentIssueMessage(issue)}
              </Text>
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

