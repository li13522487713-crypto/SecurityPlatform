import { useEffect, useMemo, useState } from "react";
import { Button } from "@douyinfe/semi-ui";
import { TracePanel, ensureWorkflowI18n, type TraceStepItem } from "@atlas/workflow-editor-react";
import type { WorkflowTraceStepSummary } from "../types";

export interface WorkflowTracePanelProps {
  locale: "zh-CN" | "en-US";
  steps: WorkflowTraceStepSummary[];
  /** True while a sync/remote run is in progress (e.g. after试运行). */
  runActive?: boolean;
  onClearSteps: () => void;
  title: string;
  clearLabel: string;
  collapseLabel: string;
  expandLabel: string;
  runningHint: string;
}

/**
 * Bottom dock that reuses {@link TracePanel} from `workflow-editor-react` for run-time execution traces.
 * Shown while a run is active or when trace steps exist.
 */
export function WorkflowTracePanel(props: WorkflowTracePanelProps) {
  const { locale, steps, runActive, onClearSteps, title, clearLabel, collapseLabel, expandLabel, runningHint } = props;

  useEffect(() => {
    ensureWorkflowI18n(locale);
  }, [locale]);

  const traceItems: TraceStepItem[] = useMemo(
    () =>
      steps.map((s) => ({
        timestamp: s.timestamp,
        nodeKey: s.nodeKey,
        status: s.status,
        detail: s.detail
      })),
    [steps]
  );

  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    if (steps.length > 0 || runActive) {
      setCollapsed(false);
    }
  }, [steps.length, runActive]);

  const showDock = runActive || steps.length > 0;
  if (!showDock) {
    return null;
  }

  const tabTitle = `${title}${steps.length > 0 ? ` (${steps.length})` : ""}`;

  return (
    <div className="module-workflow__trace-dock" data-testid="workflow-trace-dock">
      <div className="module-workflow__trace-dock-bar">
        <div className="module-workflow__trace-dock-title">{tabTitle}</div>
        <div className="module-workflow__trace-dock-actions">
          {runActive ? <span className="module-workflow__trace-hint">{runningHint}</span> : null}
          <Button size="small" theme="borderless" onClick={onClearSteps} disabled={steps.length === 0}>
            {clearLabel}
          </Button>
          <Button size="small" theme="borderless" onClick={() => setCollapsed((v) => !v)}>
            {collapsed ? expandLabel : collapseLabel}
          </Button>
        </div>
      </div>
      {!collapsed ? (
        <div className="module-workflow__trace-dock-body">
          <TracePanel visible steps={traceItems} onClose={() => setCollapsed(true)} />
        </div>
      ) : null}
    </div>
  );
}
