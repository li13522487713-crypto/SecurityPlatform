import type { DebugWebSocketState } from "../hooks/use-debug-ws";

export interface DebugWsStatusTag {
  text: string;
  color: "green" | "blue" | "red" | "grey";
}

export function getDebugWsStatusTag(status: DebugWebSocketState): DebugWsStatusTag {
  if (status === "connected") {
    return { text: "WS connected", color: "green" };
  }
  if (status === "connecting") {
    return { text: "WS connecting", color: "blue" };
  }
  if (status === "error") {
    return { text: "WS error", color: "red" };
  }
  return { text: "WS disconnected", color: "grey" };
}
