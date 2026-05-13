import { useCallback, useEffect, useRef, useState } from "react";
import type { MicroflowRuntimeOverlaySnapshot, MicroflowRuntimeWsEvent } from "../runtime/runtime-overlay";

export type RuntimeWebSocketState = "disconnected" | "connecting" | "reconnecting" | "connected" | "error";

export interface UseRuntimeWebSocketOptions {
  runId?: string;
  lastSequence?: number;
  autoReconnect?: number;
  reconnectDelayMs?: number;
  onEvent?: (event: MicroflowRuntimeWsEvent) => void;
  onSnapshot?: (snapshot: MicroflowRuntimeOverlaySnapshot) => void;
  onStatusChange?: (status: RuntimeWebSocketState) => void;
}

export interface UseRuntimeWebSocketReturn {
  status: RuntimeWebSocketState;
  connect: () => void;
  disconnect: () => void;
  refreshSnapshot: () => Promise<void>;
}

const DEFAULT_RECONNECT_DELAY_MS = 800;
const MAX_RECONNECT_ATTEMPTS = 8;

function resolveRuntimeWsUrl(runId: string, lastSequence?: number): string {
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  const host = window.location.host;
  const query = typeof lastSequence === "number" && Number.isFinite(lastSequence)
    ? `?lastSequence=${Math.max(0, Math.floor(lastSequence))}`
    : "";
  return `${protocol}//${host}/api/v1/microflows/runs/${encodeURIComponent(runId)}/runtime/events/ws${query}`;
}

function resolveRuntimeSnapshotUrl(runId: string, lastSequence?: number): string {
  const base = `/api/v1/microflows/runs/${encodeURIComponent(runId)}/runtime/events/snapshot`;
  if (typeof lastSequence === "number" && Number.isFinite(lastSequence)) {
    return `${base}?lastSequence=${Math.max(0, Math.floor(lastSequence))}`;
  }
  return base;
}

export function useRuntimeWebSocket(options: UseRuntimeWebSocketOptions): UseRuntimeWebSocketReturn {
  const {
    runId,
    lastSequence,
    autoReconnect = MAX_RECONNECT_ATTEMPTS,
    reconnectDelayMs = DEFAULT_RECONNECT_DELAY_MS,
    onEvent,
    onSnapshot,
    onStatusChange,
  } = options;

  const wsRef = useRef<WebSocket | null>(null);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const closingRef = useRef(false);
  const runIdRef = useRef(runId);
  const lastSequenceRef = useRef(lastSequence ?? 0);
  const connectRef = useRef<(() => void) | null>(null);
  const [status, setStatus] = useState<RuntimeWebSocketState>("disconnected");

  runIdRef.current = runId;
  lastSequenceRef.current = lastSequence ?? 0;

  const setRuntimeStatus = useCallback((next: RuntimeWebSocketState) => {
    setStatus(next);
    onStatusChange?.(next);
  }, [onStatusChange]);

  const clearReconnectTimer = useCallback(() => {
    if (reconnectTimerRef.current) {
      clearTimeout(reconnectTimerRef.current);
      reconnectTimerRef.current = null;
    }
  }, []);

  const refreshSnapshot = useCallback(async () => {
    const activeRunId = runIdRef.current;
    if (!activeRunId) {
      return;
    }
    try {
      const response = await fetch(resolveRuntimeSnapshotUrl(activeRunId, lastSequenceRef.current), {
        credentials: "include",
        headers: { Accept: "application/json" },
      });
      if (!response.ok) {
        return;
      }
      const body = await response.json() as { success?: boolean; data?: MicroflowRuntimeOverlaySnapshot };
      if (!body?.data) {
        return;
      }
      onSnapshot?.(body.data);
    } catch {
      // Snapshot refresh is best-effort; failures are handled by WS reconnect.
    }
  }, [onSnapshot]);

  const connectImpl = useCallback(() => {
    const activeRunId = runIdRef.current;
    if (!activeRunId) {
      return;
    }
    if (wsRef.current?.readyState === WebSocket.OPEN || wsRef.current?.readyState === WebSocket.CONNECTING) {
      return;
    }

    const isReconnect = reconnectAttemptsRef.current > 0;
    setRuntimeStatus(isReconnect ? "reconnecting" : "connecting");
    const ws = new WebSocket(resolveRuntimeWsUrl(activeRunId, lastSequenceRef.current));
    wsRef.current = ws;

    ws.onopen = () => {
      reconnectAttemptsRef.current = 0;
      setRuntimeStatus("connected");
      const lastSeq = Math.max(0, Math.floor(lastSequenceRef.current ?? 0));
      ws.send(JSON.stringify({ type: "resumeFrom", lastSequence: lastSeq }));
    };

    ws.onmessage = event => {
      try {
        const payload = JSON.parse(event.data) as MicroflowRuntimeWsEvent;
        if (!payload || typeof payload !== "object") {
          return;
        }
        if (typeof payload.sequence === "number") {
          lastSequenceRef.current = Math.max(lastSequenceRef.current, payload.sequence);
        }
        onEvent?.(payload);
      } catch {
        // Ignore malformed runtime event messages.
      }
    };

    ws.onerror = () => {
      setRuntimeStatus("error");
    };

    ws.onclose = () => {
      wsRef.current = null;
      clearReconnectTimer();
      if (closingRef.current) {
        setRuntimeStatus("disconnected");
        return;
      }
      setRuntimeStatus("disconnected");
      if (reconnectAttemptsRef.current < autoReconnect) {
        const delay = Math.min(reconnectDelayMs * (2 ** reconnectAttemptsRef.current), 15_000);
        reconnectAttemptsRef.current += 1;
        reconnectTimerRef.current = setTimeout(() => {
          void refreshSnapshot().finally(() => {
            connectRef.current?.();
          });
        }, delay);
      } else {
        setRuntimeStatus("error");
      }
    };
  }, [autoReconnect, clearReconnectTimer, reconnectDelayMs, refreshSnapshot, setRuntimeStatus, onEvent]);

  connectRef.current = connectImpl;

  const connect = useCallback(() => {
    if (!runIdRef.current) {
      return;
    }
    closingRef.current = false;
    void refreshSnapshot().finally(() => {
      connectImpl();
    });
  }, [connectImpl, refreshSnapshot]);

  const disconnect = useCallback(() => {
    closingRef.current = true;
    clearReconnectTimer();
    if (wsRef.current) {
      if (wsRef.current.readyState === WebSocket.OPEN || wsRef.current.readyState === WebSocket.CONNECTING) {
        wsRef.current.close();
      }
      wsRef.current = null;
    }
    reconnectAttemptsRef.current = 0;
    setRuntimeStatus("disconnected");
  }, [clearReconnectTimer, setRuntimeStatus]);

  useEffect(() => () => {
    disconnect();
  }, [disconnect]);

  return {
    status,
    connect,
    disconnect,
    refreshSnapshot,
  };
}

