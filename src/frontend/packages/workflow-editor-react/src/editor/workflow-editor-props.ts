import type {
  NodeTemplateMetadata,
  NodeTypeMetadata,
  RunTrace,
  WorkflowDetailQuery,
  WorkflowDetailResponse,
  WorkflowModelCatalogItem,
  WorkflowSaveRequest,
  WorkflowValidateRequest,
  WorkflowVersionDiff,
  WorkflowVersionRollbackResult,
  WorkflowVersionItem
} from "../types";
import type { TraceStepItem } from "../components/TracePanel";
import type { CanvasValidationResult } from "./editor-validation";

export interface WorkflowApiClient {
  getDetail?: (id: string, query?: WorkflowDetailQuery) => Promise<{ data?: WorkflowDetailResponse }>;
  saveDraft?: (id: string, req: WorkflowSaveRequest & { saveVersion?: number; ignoreStatusTransfer?: boolean }) => Promise<unknown>;
  publish?: (id: string, req: { changeLog?: string }) => Promise<unknown>;
  copy?: (id: string) => Promise<{ data?: { id?: string } }>;
  getVersions?: (id: string) => Promise<{ data?: WorkflowVersionItem[] }>;
  getVersionDiff?: (id: string, fromVersionId: string, toVersionId: string) => Promise<{ data?: WorkflowVersionDiff }>;
  rollbackVersion?: (id: string, versionId: string) => Promise<{ data?: WorkflowVersionRollbackResult }>;
  getNodeTypes?: () => Promise<{ data?: NodeTypeMetadata[] }>;
  getNodeTemplates?: () => Promise<{ data?: NodeTemplateMetadata[] }>;
  getModelCatalog?: () => Promise<{ data?: WorkflowModelCatalogItem[] }>;
  validate?: (id: string, req: WorkflowValidateRequest) => Promise<{ data?: { isValid?: boolean; errors?: string[] } }>;
  runSync?: (
    id: string,
    req: { inputsJson?: string; source?: "published" | "draft" }
  ) => Promise<{ data?: { executionId: string } }>;
  runStream?: (
    id: string,
    req: { inputsJson?: string; source?: "published" | "draft" },
    callbacks: {
      onExecutionStarted?: (ev: { executionId: string }) => void;
      onNodeStarted?: (ev: { nodeKey: string; nodeType: string }) => void;
      onNodeOutput?: (ev: { nodeKey: string }) => void;
      onNodeCompleted?: (ev: { nodeKey: string; durationMs?: number }) => void;
      onNodeFailed?: (ev: { nodeKey: string; errorMessage: string }) => void;
      onNodeSkipped?: (ev: { nodeKey: string; reason?: string }) => void;
      onNodeBlocked?: (ev: { nodeKey: string; reason?: string }) => void;
      onEdgeStatusChanged?: (ev: {
        edge?: {
          sourceNodeKey?: string;
          sourcePort?: string;
          targetNodeKey?: string;
          targetPort?: string;
          status?: number;
        };
      }) => void;
      onBranchDecision?: (ev: {
        executionId?: string;
        nodeKey: string;
        nodeType?: string;
        selectedBranch?: string;
        candidates?: string[];
      }) => void;
      onExecutionCompleted?: (ev: { outputsJson?: string }) => void;
      onExecutionFailed?: (ev: { errorMessage: string }) => void;
      onExecutionCancelled?: (ev: { errorMessage?: string }) => void;
      onExecutionInterrupted?: (ev: { interruptType: string; nodeKey?: string }) => void;
      onError?: (err: Event | Error) => void;
    }
  ) => { abort: () => void; done: Promise<void> };
  getProcess?: (
    executionId: string
  ) => Promise<{ data?: { status?: number; nodeExecutions?: Array<{ nodeKey: string; status: number; errorMessage?: string }> } }>;
  getTrace?: (executionId: string) => Promise<{ data?: RunTrace }>;
  cancel?: (executionId: string) => Promise<unknown>;
  debugNode?: (
    workflowId: string,
    nodeKey: string,
    req: {
      nodeKey: string;
      inputsJson?: string;
      inputs?: Record<string, unknown>;
      source?: "published" | "draft";
      versionId?: string;
    }
  ) => Promise<{ data?: { outputsJson?: string; status?: number; executionId?: string } }>;
}

export type WorkflowPanelCommandType =
  | "openNodePanel"
  | "openTestRun"
  | "openTrace"
  | "openVariables"
  | "openRoleConfig"
  | "openDebug"
  | "openProblems";

export interface WorkflowPanelCommand {
  type: WorkflowPanelCommandType;
  nonce: number;
}

export interface WorkflowEditorReactProps {
  workflowId: string;
  locale?: string;
  readOnly?: boolean;
  mode?: "workflow" | "chatflow";
  detailQuery?: WorkflowDetailQuery;
  apiClient: WorkflowApiClient;
  onBack?: () => void;
  panelCommand?: WorkflowPanelCommand;
  onValidationChange?: (validation: CanvasValidationResult | null) => void;
  onTraceStepsChange?: (steps: TraceStepItem[]) => void;
  focusNodeKey?: string;
}

export type { CanvasValidationResult } from "./editor-validation";
