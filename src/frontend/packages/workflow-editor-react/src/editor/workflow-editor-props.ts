import type { WorkflowDetailResponse, NodeTypeMetadata, NodeTemplateMetadata, WorkflowSaveRequest } from "../types";

export interface WorkflowApiClient {
  getDetail?: (id: string) => Promise<{ data?: WorkflowDetailResponse }>;
  saveDraft?: (id: string, req: WorkflowSaveRequest & { saveVersion?: number; ignoreStatusTransfer?: boolean }) => Promise<unknown>;
  publish?: (id: string, req: { changeLog?: string }) => Promise<unknown>;
  copy?: (id: string) => Promise<{ data?: { id?: string } }>;
  getNodeTypes?: () => Promise<{ data?: NodeTypeMetadata[] }>;
  getNodeTemplates?: () => Promise<{ data?: NodeTemplateMetadata[] }>;
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
  cancel?: (executionId: string) => Promise<unknown>;
  debugNode?: (
    workflowId: string,
    nodeKey: string,
    req: { nodeKey: string; inputsJson?: string; inputs?: Record<string, unknown> }
  ) => Promise<{ data?: { outputsJson?: string; status: number; executionId?: string } }>;
}

export interface WorkflowEditorReactProps {
  workflowId: string;
  locale?: string;
  readOnly?: boolean;
  apiClient: WorkflowApiClient;
  onBack?: () => void;
}
