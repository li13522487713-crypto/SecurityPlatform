import { useCallback, useEffect, useRef, useState } from "react";
import {
  DebugCommand,
  DebugConnectionStatus,
  DEBUG_WS_EVENTS,
  type DebugStore,
  type DebugWsEvent,
} from "../stores/debug-store";

export { DEBUG_WS_EVENTS } from "../stores/debug-store";

export const DEBUG_WS_COMMANDS = {
  STEP_OVER: "step-over",
  STEP_INTO: "step-into",
  STEP_OUT: "step-out",
  CONTINUE: "continue",
  PAUSE: "pause",
  RUN_TO_NODE: "run-to-node",
  RUN_TO_CURSOR: "run-to-cursor",
  STOP: "stop",
  SET_BP: "set-breakpoint",
  REMOVE_BP: "remove-breakpoint",
} as const;

export type DebugWebSocketState = "disconnected" | "connecting" | "connected" | "error";
export type DebugEventType = DebugWsEvent["type"];

export interface UseDebugWebSocketOptions {
  sessionId?: string;
  autoReconnect?: number;
  reconnectDelayMs?: number;
  pingIntervalMs?: number;
  store?: DebugStore;
  onEvent?: (event: DebugWsEvent) => void;
  onStatusChange?: (status: DebugWebSocketState) => void;
  getUrl?: (microflowId: string, sessionId?: string) => string;
}

export interface UseDebugWebSocketReturn {
  status: DebugWebSocketState;
  connect: () => void;
  disconnect: () => void;
  send: (command: DebugCommand, payload?: Record<string, unknown>) => void;
}

const DEFAULT_RECONNECT_DELAY_MS = 1_000;
const MAX_RECONNECT_ATTEMPTS = 3;
const DEFAULT_PING_INTERVAL_MS = 30_000;

function resolveDefaultDebugUrl(microflowId: string, sessionId?: string): string {
  if (typeof window === "undefined") {
    return "";
  }
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  const host = window.location.host;
  const encodedMicroflow = encodeURIComponent(microflowId);
  if (sessionId) {
    return `${protocol}//${host}/debug/microflow/${encodedMicroflow}?sessionId=${encodeURIComponent(sessionId)}`;
  }
  return `${protocol}//${host}/debug/microflow/${encodedMicroflow}`;
}

export function useDebugWebSocket(
  microflowId: string,
  options: UseDebugWebSocketOptions = {},
): UseDebugWebSocketReturn {
  const {
    sessionId,
    autoReconnect = MAX_RECONNECT_ATTEMPTS,
    reconnectDelayMs = DEFAULT_RECONNECT_DELAY_MS,
    pingIntervalMs = DEFAULT_PING_INTERVAL_MS,
    store,
    onEvent,
    onStatusChange,
    getUrl,
  } = options;

  const wsRef = useRef<WebSocket | null>(null);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pingTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const closingRef = useRef(false);
  const [status, setStatus] = useState<DebugWebSocketState>("disconnected");

  const clearTimers = useCallback(() => {
    if (reconnectTimerRef.current !== null) {
      clearTimeout(reconnectTimerRef.current);
      reconnectTimerRef.current = null;
    }
    if (pingTimerRef.current !== null) {
      clearInterval(pingTimerRef.current);
      pingTimerRef.current = null;
    }
  }, []);

  const updateStatus = useCallback((next: DebugWebSocketState) => {
    setStatus(next);
    store?.setStatus(next as DebugConnectionStatus);
    onStatusChange?.(next);
  }, [onStatusChange, store]);

  const sendNow = useCallback((command: DebugCommand, payload: Record<string, unknown> = {}) => {
    const ws = wsRef.current;
    if (ws?.readyState !== WebSocket.OPEN) {
      store?.queueCommand(command, payload);
      return;
    }
    ws.send(JSON.stringify({ type: command, ...payload }));
  }, [store]);

  const registerBreakpoints = useCallback(() => {
    const ws = wsRef.current;
    if (!ws || ws.readyState !== WebSocket.OPEN) {
      return;
    }
    for (const breakpoint of store?.breakpointItems ?? []) {
      ws.send(JSON.stringify({
        type: DEBUG_WS_COMMANDS.SET_BP,
        breakpoint,
      }));
    }
    for (const breakpoint of store?.conditionalBreakpointItems ?? []) {
      ws.send(JSON.stringify({
        type: DEBUG_WS_COMMANDS.SET_BP,
        breakpoint,
      }));
    }
  }, [store]);

  const flushQueuedCommands = useCallback(() => {
    const ws = wsRef.current;
    if (!ws || ws.readyState !== WebSocket.OPEN || !store) {
      return;
    }
    for (const queued of store.popCommands()) {
      ws.send(JSON.stringify({ type: queued.command, ...(queued.payload ?? {}) }));
    }
  }, [store]);

  const connectRef = useRef<(() => void) | null>(null);

  const connectImpl = useCallback(() => {
    const url = getUrl ? getUrl(microflowId, sessionId) : resolveDefaultDebugUrl(microflowId, sessionId);
    if (!url) {
      updateStatus("error");
      return;
    }

    if (wsRef.current?.readyState === WebSocket.OPEN || wsRef.current?.readyState === WebSocket.CONNECTING) {
      return;
    }

    updateStatus("connecting");
    const ws = new WebSocket(url);
    wsRef.current = ws;

    ws.onopen = () => {
      reconnectAttemptsRef.current = 0;
      updateStatus("connected");
      registerBreakpoints();
      flushQueuedCommands();
      if (pingTimerRef.current === null) {
        pingTimerRef.current = setInterval(() => sendNow("ping", { at: Date.now() }), pingIntervalMs);
      }
      if (sessionId) {
        store?.setSession(sessionId);
      }
      ws.send(JSON.stringify({ type: "hello", sessionId }));
    };

    ws.onmessage = event => {
      let payload: DebugWsEvent;
      try {
        payload = JSON.parse(event.data) as DebugWsEvent;
      } catch (error) {
        store?.setError(error instanceof Error ? error.message : String(error));
        return;
      }
      onEvent?.(payload);
      store?.handleEvent(payload);
    };

    ws.onerror = () => {
      updateStatus("error");
      store?.setError("WebSocket error");
    };

    ws.onclose = () => {
      clearTimers();
      if (!closingRef.current) {
        updateStatus("disconnected");
        if (reconnectAttemptsRef.current < autoReconnect) {
          const delay = Math.min(reconnectDelayMs * (2 ** reconnectAttemptsRef.current), 30_000);
          reconnectAttemptsRef.current += 1;
          reconnectTimerRef.current = setTimeout(() => {
            connectRef.current?.();
          }, delay);
        } else {
          updateStatus("error");
        }
      } else {
        updateStatus("disconnected");
      }
    };
  }, [
    autoReconnect,
    clearTimers,
    flushQueuedCommands,
    getUrl,
    microflowId,
    onEvent,
    pingIntervalMs,
    registerBreakpoints,
    sessionId,
    sendNow,
    store,
    updateStatus,
  ]);
  connectRef.current = connectImpl;

  const connect = useCallback(() => {
    closingRef.current = false;
    connectImpl();
  }, [connectImpl]);

  const disconnect = useCallback(() => {
    closingRef.current = true;
    clearTimers();
    if (wsRef.current) {
      if (wsRef.current.readyState === WebSocket.OPEN || wsRef.current.readyState === WebSocket.CONNECTING) {
        wsRef.current.close();
      }
      wsRef.current = null;
    }
    updateStatus("disconnected");
  }, [clearTimers, updateStatus]);

  const send = useCallback((command: DebugCommand, payload: Record<string, unknown> = {}) => {
    sendNow(command, payload);
  }, [sendNow]);

  useEffect(() => () => {
    disconnect();
  }, [disconnect]);

  return { status, connect, disconnect, send };
}
