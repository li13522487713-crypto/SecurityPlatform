import type { DebugWebSocketState } from "../hooks/use-debug-ws";

export interface DebugWsStatusTag {
  text: string;
  color: "green" | "blue" | "red" | "grey" | "orange";
}

export type DebugLatencyColor = "#ef4444" | "#f59e0b" | "#6b7280";

export function getDebugWsStatusTag(status: DebugWebSocketState): DebugWsStatusTag {
  if (status === "connected") {
    return { text: "已连接", color: "green" };
  }
  if (status === "connecting") {
    return { text: "连接中", color: "blue" };
  }
  if (status === "reconnecting") {
    return { text: "重连中", color: "orange" };
  }
  if (status === "error") {
    return { text: "连接失败", color: "red" };
  }
  return { text: "已断开", color: "grey" };
}

export function getDebugLatencyColor(latencyMs: number): DebugLatencyColor {
  if (latencyMs > 500) {
    return "#ef4444";
  }
  if (latencyMs > 200) {
    return "#f59e0b";
  }
  return "#6b7280";
}
