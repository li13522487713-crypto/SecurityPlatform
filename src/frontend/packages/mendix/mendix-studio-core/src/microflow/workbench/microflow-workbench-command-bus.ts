import type { MicroflowEditorHandle, MicroflowWorkbenchBottomTab } from "@atlas/microflow";

export type MicroflowWorkbenchCommandName =
  | "microflow.save"
  | "microflow.validate"
  | "microflow.run"
  | "microflow.debugRun"
  | "microflow.publish"
  | "microflow.undo"
  | "microflow.redo"
  | "microflow.openPanel"
  | "microflow.resetLayout"
  | "microflow.toggleFocusMode";

export type MicroflowWorkbenchCommandState = "idle" | "running" | "success" | "failed" | "cancelled";

export interface MicroflowWorkbenchCommandExecution {
  commandId: string;
  command: MicroflowWorkbenchCommandName;
  requestId: string;
  microflowId: string;
  tabId: string;
  startedAt: string;
  timeoutMs?: number;
  state: MicroflowWorkbenchCommandState;
  finishedAt?: string;
  errorMessage?: string;
}

export interface MicroflowWorkbenchCommandBusSnapshot {
  latestExecution?: MicroflowWorkbenchCommandExecution;
  latestExecutionByCommand: Partial<Record<MicroflowWorkbenchCommandName, MicroflowWorkbenchCommandExecution>>;
}

export interface MicroflowWorkbenchCommandPayloadMap {
  "microflow.openPanel": { panel: MicroflowWorkbenchBottomTab | "references" };
  "microflow.resetLayout": undefined;
  "microflow.toggleFocusMode": undefined;
  "microflow.save": undefined;
  "microflow.validate": undefined;
  "microflow.run": undefined;
  "microflow.debugRun": undefined;
  "microflow.publish": undefined;
  "microflow.undo": undefined;
  "microflow.redo": undefined;
}

interface MicroflowWorkbenchCommandContext {
  microflowId?: string;
  tabId?: string;
  getEditorHandle: () => MicroflowEditorHandle | null;
  openReferencesPanel?: (microflowId: string) => void;
}

type Listener = (snapshot: MicroflowWorkbenchCommandBusSnapshot) => void;

function nowIso(): string {
  return new Date().toISOString();
}

function createExecution(command: MicroflowWorkbenchCommandName, microflowId: string, tabId: string): MicroflowWorkbenchCommandExecution {
  const stamp = nowIso();
  return {
    commandId: `${command}:${microflowId}:${stamp}`,
    command,
    requestId: `${microflowId}:${Date.now()}`,
    microflowId,
    tabId,
    startedAt: stamp,
    state: "running",
  };
}

export class MicroflowWorkbenchCommandBus {
  private context: MicroflowWorkbenchCommandContext = {
    getEditorHandle: () => null,
  };

  private snapshot: MicroflowWorkbenchCommandBusSnapshot = {
    latestExecutionByCommand: {},
  };

  private readonly listeners = new Set<Listener>();

  bindContext(context: MicroflowWorkbenchCommandContext): void {
    this.context = context;
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    listener(this.snapshot);
    return () => {
      this.listeners.delete(listener);
    };
  }

  getSnapshot(): MicroflowWorkbenchCommandBusSnapshot {
    return this.snapshot;
  }

  async execute<T extends MicroflowWorkbenchCommandName>(
    command: T,
    payload?: MicroflowWorkbenchCommandPayloadMap[T],
  ): Promise<void> {
    const microflowId = this.context.microflowId;
    const tabId = this.context.tabId ?? (microflowId ? `microflow:${microflowId}` : "");
    if (!microflowId || !tabId) {
      throw new Error(`Command ${command} requires an active microflow context.`);
    }

    const execution = createExecution(command, microflowId, tabId);
    this.publish({
      ...execution,
      state: "running",
    });

    try {
      const handle = this.context.getEditorHandle();
      if (!handle && command !== "microflow.openPanel") {
        throw new Error(`Editor handle is unavailable for ${command}.`);
      }

      switch (command) {
        case "microflow.save":
          await handle!.save();
          break;
        case "microflow.validate":
          await handle!.validate();
          break;
        case "microflow.run":
          await handle!.runTest();
          break;
        case "microflow.debugRun":
          await handle!.runDebug();
          break;
        case "microflow.publish":
          await handle!.publish();
          break;
        case "microflow.undo":
          handle!.undo();
          break;
        case "microflow.redo":
          handle!.redo();
          break;
        case "microflow.openPanel":
          if (payload?.panel === "references") {
            this.context.openReferencesPanel?.(microflowId);
          } else if (payload?.panel) {
            handle?.openBottomTab(payload.panel);
          }
          break;
        case "microflow.resetLayout":
          handle?.resetLayout?.();
          break;
        case "microflow.toggleFocusMode":
          handle?.toggleFocusMode?.();
          break;
      }

      this.publish({
        ...execution,
        state: "success",
        finishedAt: nowIso(),
      });
    } catch (error) {
      this.publish({
        ...execution,
        state: "failed",
        finishedAt: nowIso(),
        errorMessage: error instanceof Error ? error.message : String(error),
      });
      throw error;
    }
  }

  private publish(execution: MicroflowWorkbenchCommandExecution): void {
    this.snapshot = {
      latestExecution: execution,
      latestExecutionByCommand: {
        ...this.snapshot.latestExecutionByCommand,
        [execution.command]: execution,
      },
    };
    for (const listener of this.listeners) {
      listener(this.snapshot);
    }
  }
}
