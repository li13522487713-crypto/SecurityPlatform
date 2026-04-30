import { useMemo, type CSSProperties, type KeyboardEvent } from "react";
import { Space, Tag, Tooltip } from "@douyinfe/semi-ui";
import { IconClock, IconRefresh } from "@douyinfe/semi-icons";
import type { MicroflowValidationIssue } from "../schema";

export interface FlowGramMicroflowStatusStripProps {
  validationIssues: MicroflowValidationIssue[];
  dirty: boolean;
  saving: boolean;
  validating: boolean;
  readonly?: boolean;
  onOpenProblemsPanel?: () => void;
}

const draftStyle: CSSProperties = {
  backgroundColor: "rgba(255, 237, 213, 0.95)",
  color: "#7c2d12",
  border: "1px solid rgba(180, 83, 9, 0.2)",
};

const errorStyle: CSSProperties = {
  backgroundColor: "rgba(254, 226, 226, 0.95)",
  color: "#991b1b",
  border: "1px solid rgba(220, 38, 38, 0.2)",
};

const warningStyle: CSSProperties = {
  backgroundColor: "rgba(254, 249, 195, 0.95)",
  color: "#854d0e",
  border: "1px solid rgba(202, 138, 4, 0.25)",
};

const validatingStyle: CSSProperties = {
  backgroundColor: "rgba(219, 234, 254, 0.95)",
  color: "#1d4ed8",
  border: "1px solid rgba(59, 130, 246, 0.25)",
};

function IssueCountTag({
  count,
  suffix,
  baseStyle,
  onOpen,
}: {
  count: number;
  suffix: string;
  baseStyle: CSSProperties;
  onOpen?: () => void;
}) {
  const handleKeyDown = (event: KeyboardEvent<HTMLSpanElement>) => {
    if (!onOpen) {
      return;
    }
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      onOpen();
    }
  };
  const tag = (
    <Tag size="small" style={onOpen ? { ...baseStyle, cursor: "pointer" } : baseStyle}>
      {count} {suffix}
    </Tag>
  );
  if (!onOpen) {
    return tag;
  }
  return (
    <Tooltip content="查看问题">
      <span
        role="button"
        tabIndex={0}
        className="microflow-flowgram-issue-tag-hitbox"
        onClick={onOpen}
        onKeyDown={handleKeyDown}
        style={{ display: "inline-flex", cursor: "pointer", borderRadius: 6 }}
      >
        {tag}
      </span>
    </Tooltip>
  );
}

export function FlowGramMicroflowStatusStrip({
  validationIssues,
  dirty,
  saving,
  validating,
  readonly: readOnly,
  onOpenProblemsPanel,
}: FlowGramMicroflowStatusStripProps) {
  const { errorCount, warningCount } = useMemo(() => {
    let errors = 0;
    let warnings = 0;
    for (const issue of validationIssues) {
      if (issue.severity === "error") {
        errors += 1;
      } else if (issue.severity === "warning") {
        warnings += 1;
      }
    }
    return { errorCount: errors, warningCount: warnings };
  }, [validationIssues]);

  const draftLabel = readOnly
    ? "只读"
    : saving
      ? "保存中"
      : dirty
        ? "草稿待保存"
        : "已保存";

  const openProblems = () => {
    onOpenProblemsPanel?.();
  };

  return (
    <div className="microflow-flowgram-status-strip" role="status" aria-live="polite">
      <Space spacing={6} wrap>
        <Tag size="small" prefixIcon={<IconClock />} style={draftStyle}>
          {draftLabel}
        </Tag>
        {errorCount > 0 ? (
          <IssueCountTag count={errorCount} suffix="错误" baseStyle={errorStyle} onOpen={onOpenProblemsPanel ? openProblems : undefined} />
        ) : null}
        {warningCount > 0 ? (
          <IssueCountTag
            count={warningCount}
            suffix="警告"
            baseStyle={warningStyle}
            onOpen={onOpenProblemsPanel ? openProblems : undefined}
          />
        ) : null}
        {validating ? (
          <Tag size="small" prefixIcon={<IconRefresh />} style={validatingStyle}>
            校验中
          </Tag>
        ) : null}
      </Space>
    </div>
  );
}
