import { WorkflowEditorShell } from "./workflow-editor-shell";
import { WorkflowHostShell } from "./workflow-host-shell";
import type { WorkflowPageProps, WorkflowWorkbenchNavigation, WorkflowResourceMode } from "./types";

export type WorkflowDesignerEngine = "atlas-editor" | "coze-playground";
const WORKFLOW_ENGINE_QUERY_KEY = "wf_engine";
const WORKFLOW_ENGINE_STORAGE_KEY = "atlas_workflow_designer_engine";

interface WorkflowDesignerShellProps extends WorkflowPageProps, WorkflowWorkbenchNavigation {
  workflowId: string;
  onBack: () => void;
  backPath?: string;
  mode?: WorkflowResourceMode;
  engine?: WorkflowDesignerEngine;
}

/**
 * 统一工作流编辑入口。
 *
 * 当前默认仍走 Atlas editor 实现；后续切换到 Coze playground 时，
 * 只需要替换这里的底层渲染分支，而不需要再次改动路由与宿主编排。
 */
export function WorkflowDesignerShell(props: WorkflowDesignerShellProps) {
  const resolvedEngine = resolveWorkflowDesignerEngine(props.engine);
  switch (resolvedEngine) {
    case "coze-playground":
      return <WorkflowHostShell {...props} />;
    case "atlas-editor":
    default:
      return <WorkflowEditorShell {...props} />;
  }
}

function resolveWorkflowDesignerEngine(explicitEngine?: WorkflowDesignerEngine): WorkflowDesignerEngine {
  if (explicitEngine) {
    return explicitEngine;
  }

  const envEngine = import.meta.env.VITE_WORKFLOW_DESIGNER_ENGINE;
  const normalizedEnvEngine = normalizeEngine(envEngine);
  if (normalizedEnvEngine) {
    return normalizedEnvEngine;
  }

  if (typeof window === "undefined") {
    return "atlas-editor";
  }

  const queryEngine = normalizeEngine(new URLSearchParams(window.location.search).get(WORKFLOW_ENGINE_QUERY_KEY));
  if (queryEngine) {
    return queryEngine;
  }

  const storageEngine = normalizeEngine(window.localStorage.getItem(WORKFLOW_ENGINE_STORAGE_KEY));
  if (storageEngine) {
    return storageEngine;
  }

  return "atlas-editor";
}

function normalizeEngine(value: string | null | undefined): WorkflowDesignerEngine | null {
  if (value === "coze-playground") {
    return "coze-playground";
  }
  if (value === "atlas-editor") {
    return "atlas-editor";
  }
  return null;
}
