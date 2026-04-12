import { injectable } from "inversify";
import { toCanvasJson } from "../editor/workflow-editor-state";
import type { WorkflowEditorReactProps } from "../editor/workflow-editor-props";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";

@injectable()
export class WorkflowOperationService {
  private props: WorkflowEditorReactProps | null = null;

  bindProps(props: WorkflowEditorReactProps): void {
    this.props = props;
  }

  get workflowId(): string {
    return this.props?.workflowId ?? "";
  }

  get readonly(): boolean {
    return Boolean(this.props?.readOnly);
  }

  get apiClient() {
    return this.props?.apiClient;
  }

  get detailQuery() {
    return this.props?.detailQuery;
  }

  async publish(changeLog?: string): Promise<void> {
    if (this.readonly) {
      return;
    }
    await this.apiClient?.publish?.(this.workflowId, { changeLog });
  }

  async copy(): Promise<string> {
    const response = await this.apiClient?.copy?.(this.workflowId);
    return String(response?.data?.id ?? "");
  }

  async save(ignoreStatusTransfer: boolean): Promise<void> {
    if (this.readonly) {
      return;
    }
    const state = useWorkflowEditorStore.getState();
    const saveVersion = state.saveVersion;
    await this.apiClient?.saveDraft?.(this.workflowId, {
      canvasJson: toCanvasJson(state.canvasNodes, state.canvasConnections, state.canvasGlobals, {
        x: state.pan.x,
        y: state.pan.y,
        zoom: state.zoom
      }),
      ignoreStatusTransfer,
      saveVersion
    });
    state.addSaveVersion();
  }

  async testRun(payload: { inputsJson?: string; source?: "published" | "draft" }): Promise<{ executionId?: string }> {
    if (payload.source === "draft" || !this.apiClient?.runStream) {
      const result = await this.apiClient?.runSync?.(this.workflowId, payload);
      return { executionId: result?.data?.executionId };
    }
    const result = await this.apiClient?.runSync?.(this.workflowId, payload);
    return { executionId: result?.data?.executionId };
  }

  runStream(
    payload: { inputsJson?: string; source?: "published" | "draft" },
    callbacks: Parameters<NonNullable<WorkflowEditorReactProps["apiClient"]["runStream"]>>[2]
  ): { abort: () => void; done: Promise<void> } | null {
    if (!this.apiClient?.runStream) {
      return null;
    }
    return this.apiClient.runStream(this.workflowId, payload, callbacks);
  }

  async getProcess(executionId: string) {
    return this.apiClient?.getProcess?.(executionId);
  }

  async cancel(executionId: string): Promise<void> {
    if (!executionId) {
      return;
    }
    if (this.apiClient?.cancel) {
      await this.apiClient.cancel(executionId);
    }
  }

  async testOneNode(nodeKey: string, inputsJson?: string) {
    if (!this.apiClient?.debugNode) {
      return undefined;
    }
    return this.apiClient.debugNode(this.workflowId, nodeKey, {
      nodeKey,
      inputsJson,
      source: this.readonly ? "published" : "draft"
    });
  }
}
