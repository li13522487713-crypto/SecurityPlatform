export type WsSessionStatus = "initialized" | "running" | "paused" | "completed";

export interface WsNodeStatusPayload {
  nodeId: string;
  nodeType?: string;
  timestamp?: number;
  stackLevel?: number;
}

export interface WsStateSyncPayload {
  nodeStatuses?: Record<string, string>;
  executedEdgeIds?: string[];
  variables?: Array<Record<string, unknown>>;
  breakpoints?: Array<Record<string, unknown>>;
  callStack?: Array<Record<string, unknown>>;
}

export type ServerMessage =
  | { type: "node-enter"; id?: string; timestamp?: number; data: WsNodeStatusPayload }
  | { type: "node-exit"; id?: string; timestamp?: number; data: WsNodeStatusPayload }
  | { type: "edge-taken"; id?: string; timestamp?: number; data: Record<string, unknown> }
  | { type: "breakpoint"; id?: string; timestamp?: number; data: Record<string, unknown> }
  | { type: "paused"; id?: string; timestamp?: number; data: Record<string, unknown> }
  | { type: "loop-iter"; id?: string; timestamp?: number; data: Record<string, unknown> }
  | { type: "error"; id?: string; timestamp?: number; data: Record<string, unknown> }
  | { type: "complete"; id?: string; timestamp?: number; data: Record<string, unknown> }
  | { type: "session-status"; id?: string; timestamp?: number; data: { status: WsSessionStatus; sessionId: string } }
  | { type: "ping"; id?: string; timestamp?: number; data: { sequence: number } }
  | { type: "pong"; id?: string; timestamp?: number; data: { sequence: number } }
  | { type: "variable-details"; id?: string; timestamp?: number; data: Record<string, unknown> }
  | { type: "state-sync"; id?: string; timestamp?: number; data: WsStateSyncPayload };

export type ClientMessage =
  | { type: "step-over" }
  | { type: "step-into" }
  | { type: "step-out" }
  | { type: "continue" }
  | { type: "continue-all" }
  | { type: "stop" }
  | { type: "set-breakpoint"; data: { nodeId: string; condition?: string; enabled: boolean } }
  | { type: "remove-breakpoint"; data: { nodeId: string } }
  | { type: "toggle-breakpoint"; data: { nodeId: string; enabled: boolean } }
  | { type: "get-variable-details"; data: { requestId: string; variableName: string; maxDepth?: number } }
  | { type: "set-variable"; data: { variableName: string; value: unknown } };

