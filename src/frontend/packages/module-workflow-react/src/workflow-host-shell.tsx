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
  onBack,
  spaceId,
  returnUrl,
  backPath
}: WorkflowHostShellProps) {
  const effectiveReturnUrl = typeof window === "undefined"
    ? (returnUrl ?? backPath ?? "")
    : (returnUrl ?? backPath ?? window.location.href);
  const effectiveSpaceId = useMemo(
    () => spaceId || resolveSpaceIdFromLocation() || buildAtlasWorkflowSpaceId(mode),
    [mode, spaceId]
  );

  return (
    <CozeWorkflowPage
      workflowId={workflowId}
      spaceId={effectiveSpaceId}
      mode={mode}
      returnUrl={effectiveReturnUrl}
      onAtlasBack={onBack}
    />
  );
}

function buildAtlasWorkflowSpaceId(mode: WorkflowResourceMode): string {
  return `atlas-${mode}`;
}

function resolveSpaceIdFromLocation(): string {
  if (typeof window === "undefined") {
    return "";
  }

  const match = window.location.pathname.match(/\/workspaces\/([^/]+)\//);
  return match?.[1] ?? "";
}
