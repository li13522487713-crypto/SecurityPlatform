import { Button, Select, Space, Tag, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale, WorkflowListItem } from "../types";
import { getStudioCopy } from "../copy";

export interface WorkflowBindCardProps {
  boundWorkflowId: string | undefined;
  onChange: (workflowId: string | undefined) => void;
  workflows: WorkflowListItem[];
  loading?: boolean;
  disabled?: boolean;
  onOpenWorkflow?: (workflowId: string) => void;
  locale: StudioLocale;
}

export function WorkflowBindCard({
  boundWorkflowId,
  onChange,
  workflows,
  loading,
  disabled,
  onOpenWorkflow,
  locale
}: WorkflowBindCardProps) {
  const copy = getStudioCopy(locale);
  const selected = workflows.find(w => w.id === boundWorkflowId);

  return (
    <div className="module-studio__coze-inspector-card module-studio__app-builder-panel">
      <div className="module-studio__card-head">
        <span>{copy.appBuilder.workflowBindHeader}</span>
        {selected?.latestVersionNumber != null ? (
          <Tag color="cyan">v{selected.latestVersionNumber}</Tag>
        ) : null}
      </div>
      <Typography.Text type="tertiary" size="small" style={{ display: "block", marginTop: 0 }}>
        {copy.appBuilder.workflowBindHint}
      </Typography.Text>
      <Select
        loading={loading}
        disabled={disabled}
        placeholder={copy.appBuilder.workflowBindPlaceholder}
        value={boundWorkflowId}
        style={{ width: "100%" }}
        filter
        optionList={workflows.map(w => ({
          label: w.name,
          value: w.id
        }))}
        onChange={v => onChange(typeof v === "string" && v ? v : undefined)}
        showClear
      />
      {selected ? (
        <Typography.Text type="tertiary" size="small" style={{ display: "block", marginTop: 8 }}>
          {selected.description || copy.appBuilder.workflowBindNoDescription}
        </Typography.Text>
      ) : null}
      <Space style={{ marginTop: 12 }} wrap>
        <Button
          disabled={!boundWorkflowId || disabled}
          onClick={() => {
            if (boundWorkflowId && onOpenWorkflow) {
              onOpenWorkflow(boundWorkflowId);
            }
          }}
        >
          {copy.appBuilder.workflowBindOpenInEditor}
        </Button>
      </Space>
    </div>
  );
}
