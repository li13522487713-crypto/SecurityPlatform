import { injectable } from "inversify";
import { message } from "antd";
import type { TraceStepItem } from "../components/TracePanel";
import type {
  NodeExecutionDetailResponse,
  RunTrace,
  WorkflowExecutionDebugViewResponse,
  WorkflowProcessResponse
} from "../types";
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

function safeJsonStringify(value: unknown): string | undefined {
  if (value === undefined) {
    return undefined;
  }

  if (typeof value === "string") {
    return value;
  }

  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

@injectable()
export class WorkflowRunService {
  private paused = false;
  private abortHandle: (() => void) | null = null;
  private currentExecutionId = "";

  constructor(
    private readonly operationService: WorkflowOperationService,
    private readonly saveService: WorkflowSaveService
  ) {}

  private buildEdgeRuntimeKey(connection: { fromNode: string; fromPort: string; toNode: string; toPort: string }): string {
    return `${connection.fromNode}:${connection.fromPort}->${connection.toNode}:${connection.toPort}`;
  }

  private setCurrentExecutionId(executionId: string): void {
    this.currentExecutionId = executionId;
    useWorkflowEditorStore.getState().setLatestExecutionId(executionId);
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

  private mapEdgeStatus(status?: number): EdgeRuntimeState {
    return status === 1
      ? "success"
      : status === 2
        ? "skipped"
        : status === 3
          ? "failed"
          : status === 4
            ? "incomplete"
            : "idle";
  }

  private mapTraceStatus(status?: number): TraceStepItem["status"] {
    return status === 1
      ? "running"
      : status === 2
        ? "success"
        : status === 3
          ? "failed"
          : status === 6
            ? "skipped"
            : "blocked";
  }

  private formatTimestamp(value?: string): string {
    if (!value) {
      return new Date().toLocaleTimeString();
    }

    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleTimeString();
  }

  private buildTraceDetail(step: {
    durationMs?: number;
    errorMessage?: string;
    branchDecision?: Record<string, unknown>;
  }): string | undefined {
    if (step.errorMessage) {
      return step.errorMessage;
    }

    const selectedBranch =
      typeof step.branchDecision?.selectedBranch === "string"
        ? step.branchDecision.selectedBranch
        : undefined;
    if (selectedBranch) {
      return `branch=${selectedBranch}`;
    }

    if (typeof step.durationMs === "number") {
      return `${step.durationMs}ms`;
    }

    return undefined;
  }

  private updateProcessNodeDetails(nodeExecutions: WorkflowProcessResponse["nodeExecutions"] = []): void {
    const details = Object.fromEntries(
      nodeExecutions.map((item) => [item.nodeKey, item])
    ) as Record<string, NodeExecutionDetailResponse>;
    useWorkflowEditorStore.getState().setRuntimeNodeDetails(details);
    this.updateExecuteState(nodeExecutions);
  }

  private syncTraceFromTraceResponse(trace?: RunTrace, process?: WorkflowProcessResponse): void {
    if (!trace) {
      return;
    }

    const detailMap = new Map(
      (process?.nodeExecutions ?? []).map((item) => [item.nodeKey, item] as const)
    );
    const steps: TraceStepItem[] = (trace.steps ?? []).map((step) => {
      const detail = detailMap.get(step.nodeKey);
      return {
        timestamp: this.formatTimestamp(step.completedAt ?? step.startedAt),
        nodeKey: step.nodeKey,
        status: this.mapTraceStatus(step.status),
        detail: this.buildTraceDetail(step),
        nodeType: step.nodeType,
        durationMs: step.durationMs,
        errorMessage: step.errorMessage ?? detail?.errorMessage,
        inputsJson: detail?.inputsJson ?? safeJsonStringify(step.inputs),
        outputsJson: detail?.outputsJson ?? safeJsonStringify(step.outputs)
      };
    });
    useWorkflowEditorStore.getState().setTraceSteps(steps);
  }

  private applyDebugView(debugView?: WorkflowExecutionDebugViewResponse): void {
    if (!debugView?.focusNode) {
      return;
    }

    const state = debugView.focusNode.status;
    const focusState =
      state === 1
        ? "running"
        : state === 2
          ? "success"
          : state === 3
            ? "failed"
            : state === 6
              ? "skipped"
              : state === 7
                ? "blocked"
                : "idle";

    this.markNodeState(debugView.focusNode.nodeKey, focusState, debugView.focusReason);
    useWorkflowEditorStore.getState().appendLog(`debug_focus ${debugView.focusNode.nodeKey} ${debugView.focusReason}`);
  }

  private async hydrateExecutionArtifacts(executionId: string): Promise<void> {
    if (!executionId) {
      return;
    }

    const [processResult, traceResult, debugViewResult] = await Promise.allSettled([
      this.operationService.getProcess(executionId),
      this.operationService.getTrace(executionId),
      this.operationService.getDebugView(executionId)
    ]);

    const process = processResult.status === "fulfilled" ? processResult.value?.data : undefined;
    if (process) {
      this.updateProcessNodeDetails(process.nodeExecutions ?? []);
    }

    const trace = traceResult.status === "fulfilled" ? traceResult.value?.data : undefined;
    if (trace) {
      this.syncTraceFromTraceResponse(trace, process);
    }

    if (debugViewResult.status === "fulfilled") {
      this.applyDebugView(debugViewResult.value?.data);
    }
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
    this.updateProcessNodeDetails(process?.data?.nodeExecutions ?? []);
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
        this.markOutgoingEdgesState(item.nodeKey, "incomplete");
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
        this.markOutgoingEdgesState(item.nodeKey, "failed");
      }
    }
  }

  async cancelTestRun(executionId?: string): Promise<void> {
    this.abortHandle?.();
    this.abortHandle = null;
    const activeExecutionId = executionId || useWorkflowEditorStore.getState().latestExecutionId;
    if (activeExecutionId) {
      await this.operationService.cancel(activeExecutionId);
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
    this.currentExecutionId = "";

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
          onExecutionStarted: (ev) => {
            this.setCurrentExecutionId(ev.executionId);
            useWorkflowEditorStore.getState().appendLog(`execution_start ${ev.executionId}`);
          },
          onNodeStarted: (ev) => {
            this.markNodeState(ev.nodeKey, "running");
            this.markOutgoingEdgesState(ev.nodeKey, "incomplete");
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
            this.markOutgoingEdgesState(ev.nodeKey, "failed");
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
      if (this.currentExecutionId) {
        await this.hydrateExecutionArtifacts(this.currentExecutionId);
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
      this.setCurrentExecutionId(executionId);
      await this.loop(executionId);
      await this.hydrateExecutionArtifacts(executionId);
      useWorkflowEditorStore.getState().appendLog("execution_complete");
    }
    useWorkflowEditorStore.getState().setTestRunning(false);
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

    const key = this.buildEdgeRuntimeKey({
      fromNode: edge.sourceNodeKey,
      fromPort: edge.sourcePort,
      toNode: edge.targetNodeKey,
      toPort: edge.targetPort
    });
    useWorkflowEditorStore.getState().setEdgeState(key, this.mapEdgeStatus(edge.status));
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
      const executionId = response?.data?.executionId ?? "";
      if (executionId) {
        this.setCurrentExecutionId(executionId);
      }

      const [detailResult, debugViewResult] = await Promise.allSettled([
        executionId ? this.operationService.getNodeDetail(executionId, nodeKey) : Promise.resolve(undefined),
        executionId ? this.operationService.getDebugView(executionId) : Promise.resolve(undefined)
      ]);

      const nodeDetail = detailResult.status === "fulfilled" ? detailResult.value?.data : undefined;
      if (nodeDetail) {
        state.setRuntimeNodeDetail(nodeDetail);
      }

      const debugView = debugViewResult.status === "fulfilled" ? debugViewResult.value?.data : undefined;
      if (debugView) {
        this.applyDebugView(debugView);
      }

      const debugOutput = {
        run: response?.data ?? {},
        nodeDetail: nodeDetail ?? null,
        debugView: debugView ?? null
      };
      state.setDebugOutput(JSON.stringify(debugOutput, null, 2));

      const finalStatus = nodeDetail?.status ?? response?.data?.status ?? 2;
      if (finalStatus === 3 || finalStatus === 7) {
        this.markNodeState(nodeKey, "failed", nodeDetail?.errorMessage ?? response?.data?.errorMessage ?? "debug failed");
      } else {
        this.markNodeState(nodeKey, "success", debugView?.focusReason ?? "debug ok");
      }
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : "调试失败";
      state.setDebugOutput(errorMessage);
      this.markNodeState(nodeKey, "failed", errorMessage);
    } finally {
      state.setDebugRunning(false);
    }
  }
}
