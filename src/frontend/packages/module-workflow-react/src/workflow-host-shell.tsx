import { useMemo } from "react";
import { WorkflowPage as CozeWorkflowPage } from "@coze-workflow/playground-adapter";
import type { WorkflowPageProps, WorkflowWorkbenchNavigation, WorkflowResourceMode } from "./types";

interface WorkflowHostShellProps extends WorkflowPageProps, WorkflowWorkbenchNavigation {
  workflowId: string;
  onBack: () => void;
  backPath?: string;
  mode?: WorkflowResourceMode;
}

export function WorkflowHostShell({
  workflowId,
  mode = "workflow",
  onBack
}: WorkflowHostShellProps) {
  const returnUrl = typeof window === "undefined" ? "" : window.location.href;
  const spaceId = useMemo(() => buildAtlasWorkflowSpaceId(mode), [mode]);

  return <CozeWorkflowPage workflowId={workflowId} spaceId={spaceId} mode={mode} returnUrl={returnUrl} onAtlasBack={onBack} />;
}

function buildAtlasWorkflowSpaceId(mode: WorkflowResourceMode): string {
  return `atlas-${mode}`;
}
