import { injectable } from "inversify";
import { message } from "antd";
import type { EdgeRuntimeState } from "../editor/workflow-editor-state";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";
import { WorkflowOperationService } from "./workflow-operation-service";
import { WorkflowSaveService } from "./workflow-save-service";

const LOOP_GAP_TIME = 300;

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}

@injectable()
export class WorkflowRunService {
  private paused = false;
  private abortHandle: (() => void) | null = null;

  constructor(
    private readonly operationService: WorkflowOperationService,
    private readonly saveService: WorkflowSaveService
  ) {}

  private buildEdgeRuntimeKey(connection: { fromNode: string; fromPort: string; toNode: string; toPort: string }): string {
    return `${connection.fromNode}:${connection.fromPort}->${connection.toNode}:${connection.toPort}`;
  }

  private markNodeState(nodeKey: string, state: "idle" | "running" | "success" | "failed" | "skipped" | "blocked", hint?: string): void {
    const store = useWorkflowEditorStore.getState();
    store.setExecutionState(nodeKey, { state, hint });
    store.setCanvasNodes(
      store.canvasNodes.map((node) =>
        node.key === nodeKey
          ? {
              ...node,
              debugMeta: {
                ...(node.debugMeta ?? {}),
                executionState: state,
                executionHint: hint
              }
            }
          : node
      )
    );
  }

  private markOutgoingEdgesState(nodeKey: string, state: EdgeRuntimeState): void {
    const store = useWorkflowEditorStore.getState();
    for (const connection of store.canvasConnections) {
      if (connection.fromNode === nodeKey) {
        store.setEdgeState(this.buildEdgeRuntimeKey(connection), state);
      }
    }
  }

  private markEdgeStateByRuntimeEdge(edge: {
    sourceNodeKey?: string;
    sourcePort?: string;
    targetNodeKey?: string;
    targetPort?: string;
    status?: number;
  }): void {
    if (!edge.sourceNodeKey || !edge.sourcePort || !edge.targetNodeKey || !edge.targetPort) {
      return;
    }
    const mappedState: EdgeRuntimeState =
      edge.status === 1 ? "success" : edge.status === 2 ? "skipped" : edge.status === 3 ? "failed" : "idle";
    const key = this.buildEdgeRuntimeKey({
      fromNode: edge.sourceNodeKey,
      fromPort: edge.sourcePort,
      toNode: edge.targetNodeKey,
      toPort: edge.targetPort
    });
    useWorkflowEditorStore.getState().setEdgeState(key, mappedState);
  }

  pauseTestRun(): void {
    this.paused = true;
  }

  continueTestRun(): void {
    this.paused = false;
  }

  async waitContinue(): Promise<void> {
    while (this.paused) {
      await sleep(LOOP_GAP_TIME);
    }
  }

  async loop(executionId: string): Promise<number | undefined> {
    const process = await this.operationService.getProcess(executionId);
    const status = process?.data?.status;
    this.updateExecuteState(process?.data?.nodeExecutions ?? []);
    if (status !== 1) {
      return status;
    }
    await Promise.all([sleep(LOOP_GAP_TIME), this.waitContinue()]);
    return this.loop(executionId);
  }

  updateExecuteState(nodeExecutions: Array<{ nodeKey: string; status: number; errorMessage?: string }>): void {
    for (const item of nodeExecutions) {
      if (item.status === 1) {
        this.markNodeState(item.nodeKey, "running");
        this.markOutgoingEdgesState(item.nodeKey, "running");
      } else if (item.status === 2) {
        this.markNodeState(item.nodeKey, "success");
        this.markOutgoingEdgesState(item.nodeKey, "success");
      } else if (item.status === 3) {
        this.markNodeState(item.nodeKey, "failed", item.errorMessage);
        this.markOutgoingEdgesState(item.nodeKey, "failed");
      } else if (item.status === 6) {
        this.markNodeState(item.nodeKey, "skipped");
        this.markOutgoingEdgesState(item.nodeKey, "skipped");
      } else if (item.status === 7) {
        this.markNodeState(item.nodeKey, "blocked", item.errorMessage);
        this.markOutgoingEdgesState(item.nodeKey, "skipped");
      }
    }
  }

  async cancelTestRun(executionId?: string): Promise<void> {
    this.abortHandle?.();
    this.abortHandle = null;
    if (executionId) {
      await this.operationService.cancel(executionId);
    }
    useWorkflowEditorStore.getState().setTestRunning(false);
    useWorkflowEditorStore.getState().appendLog("execution_cancelled");
  }

  async testRun(): Promise<void> {
    const state = useWorkflowEditorStore.getState();
    if (state.testRunning) {
      await this.cancelTestRun();
      return;
    }

    await this.saveService.waitSaving();
    state.clearRuntimeState();
    state.setTestRunning(true);

    let parsedInputs: unknown = {};
    if (state.testInputJson.trim()) {
      try {
        parsedInputs = JSON.parse(state.testInputJson);
      } catch {
        state.setTestRunning(false);
        message.error("测试输入 JSON 不合法。");
        return;
      }
    }

    if (state.testRunMode === "stream" && this.operationService.apiClient?.runStream) {
      const handle = this.operationService.runStream(
        {
          inputsJson: JSON.stringify(parsedInputs),
          source: state.testRunSource
        },
        {
          onExecutionStarted: (ev) => useWorkflowEditorStore.getState().appendLog(`execution_start ${ev.executionId}`),
          onNodeStarted: (ev) => {
            this.markNodeState(ev.nodeKey, "running");
            this.markOutgoingEdgesState(ev.nodeKey, "running");
            useWorkflowEditorStore.getState().appendLog(`node_start ${ev.nodeKey}`);
          },
          onNodeOutput: (ev) => useWorkflowEditorStore.getState().appendLog(`node_output ${ev.nodeKey}`),
          onNodeCompleted: (ev) => {
            this.markNodeState(ev.nodeKey, "success", ev.durationMs ? `${ev.durationMs}ms` : undefined);
            this.markOutgoingEdgesState(ev.nodeKey, "success");
          },
          onNodeFailed: (ev) => {
            this.markNodeState(ev.nodeKey, "failed", ev.errorMessage);
            this.markOutgoingEdgesState(ev.nodeKey, "failed");
          },
          onNodeSkipped: (ev) => {
            this.markNodeState(ev.nodeKey, "skipped", ev.reason);
            this.markOutgoingEdgesState(ev.nodeKey, "skipped");
          },
          onNodeBlocked: (ev) => {
            this.markNodeState(ev.nodeKey, "blocked", ev.reason);
            this.markOutgoingEdgesState(ev.nodeKey, "skipped");
          },
          onEdgeStatusChanged: (ev) => {
            this.markEdgeStateByRuntimeEdge(ev.edge ?? {});
          },
          onBranchDecision: (ev) => {
            useWorkflowEditorStore.getState().appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: ev.nodeKey,
              status: "success",
              detail: `branch=${ev.selectedBranch ?? "-"}`
            });
          },
          onExecutionCompleted: () => {
            useWorkflowEditorStore.getState().appendLog("execution_complete");
            useWorkflowEditorStore.getState().setTestRunning(false);
          },
          onExecutionFailed: (ev) => {
            useWorkflowEditorStore.getState().appendLog(`execution_failed ${ev.errorMessage}`);
            useWorkflowEditorStore.getState().setTestRunning(false);
          },
          onExecutionCancelled: () => {
            useWorkflowEditorStore.getState().setTestRunning(false);
          },
          onExecutionInterrupted: () => {
            useWorkflowEditorStore.getState().setTestRunning(false);
          },
          onError: (err) => {
            useWorkflowEditorStore.getState().appendLog(`stream_error ${err instanceof Error ? err.message : "unknown"}`);
            useWorkflowEditorStore.getState().setTestRunning(false);
          }
        }
      );
      if (handle) {
        this.abortHandle = handle.abort;
        await handle.done;
        this.abortHandle = null;
      }
      useWorkflowEditorStore.getState().setTestRunning(false);
      return;
    }

    const result = await this.operationService.testRun({
      inputsJson: JSON.stringify(parsedInputs),
      source: state.testRunSource
    });
    const executionId = result.executionId;
    if (executionId) {
      await this.loop(executionId);
      useWorkflowEditorStore.getState().appendLog("execution_complete");
    }
    useWorkflowEditorStore.getState().setTestRunning(false);
  }

  async testRunOneNode(nodeKey: string, inputsJson?: string): Promise<void> {
    if (!nodeKey) {
      return;
    }
    const state = useWorkflowEditorStore.getState();
    state.setDebugRunning(true);
    this.markNodeState(nodeKey, "running", "debug");
    try {
      const response = await this.operationService.testOneNode(nodeKey, inputsJson);
      state.setDebugOutput(JSON.stringify(response?.data ?? {}, null, 2));
      this.markNodeState(nodeKey, "success", "debug ok");
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : "调试失败";
      state.setDebugOutput(errorMessage);
      this.markNodeState(nodeKey, "failed", errorMessage);
    } finally {
      state.setDebugRunning(false);
    }
  }
}
