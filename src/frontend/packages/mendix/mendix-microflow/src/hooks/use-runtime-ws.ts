import { useCallback, useEffect, useRef, useState } from "react";
import type { MicroflowRuntimeOverlaySnapshot, MicroflowRuntimeWsEvent } from "../runtime/runtime-overlay";

export type RuntimeWebSocketState = "disconnected" | "connecting" | "reconnecting" | "connected" | "error";

export interface UseRuntimeWebSocketOptions {
  runId?: string;
  lastSequence?: number;
  autoReconnect?: number;
  reconnectDelayMs?: number;
  requestHeaders?: Record<string, string> | (() => Record<string, string> | undefined);
  onEvent?: (event: MicroflowRuntimeWsEvent) => void;
  onSnapshot?: (snapshot: MicroflowRuntimeOverlaySnapshot) => void;
  onStatusChange?: (status: RuntimeWebSocketState) => void;
  onTransportError?: (error: {
    source: "snapshot" | "websocket";
    status?: number;
    message?: string;
  }) => void;
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
    requestHeaders,
    onEvent,
    onSnapshot,
    onStatusChange,
    onTransportError,
  } = options;

  const wsRef = useRef<WebSocket | null>(null);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const closingRef = useRef(false);
  const runIdRef = useRef(runId);
  const previousRunIdRef = useRef(runId);
  const snapshotAuthFailedRef = useRef(false);
  const lastSequenceRef = useRef(lastSequence ?? 0);
  const onEventRef = useRef(onEvent);
  const onSnapshotRef = useRef(onSnapshot);
  const onStatusChangeRef = useRef(onStatusChange);
  const onTransportErrorRef = useRef(onTransportError);
  const requestHeadersRef = useRef(requestHeaders);
  const connectRef = useRef<(() => void) | null>(null);
  const [status, setStatus] = useState<RuntimeWebSocketState>("disconnected");

  runIdRef.current = runId;
  if (previousRunIdRef.current !== runId) {
    previousRunIdRef.current = runId;
    snapshotAuthFailedRef.current = false;
  }
  lastSequenceRef.current = lastSequence ?? 0;
  onEventRef.current = onEvent;
  onSnapshotRef.current = onSnapshot;
  onStatusChangeRef.current = onStatusChange;
  onTransportErrorRef.current = onTransportError;
  requestHeadersRef.current = requestHeaders;

  const setRuntimeStatus = useCallback((next: RuntimeWebSocketState) => {
    setStatus(next);
    onStatusChangeRef.current?.(next);
  }, []);

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
      const runtimeHeaders = typeof requestHeadersRef.current === "function"
        ? requestHeadersRef.current()
        : requestHeadersRef.current;
      const response = await fetch(resolveRuntimeSnapshotUrl(activeRunId, lastSequenceRef.current), {
        credentials: "include",
        headers: {
          Accept: "application/json",
          ...(runtimeHeaders ?? {}),
        },
      });
      if (!response.ok) {
        if (response.status === 401) {
          snapshotAuthFailedRef.current = true;
        }
        onTransportErrorRef.current?.({
          source: "snapshot",
          status: response.status,
          message: `Snapshot request failed with status ${response.status}.`,
        });
        return;
      }
      snapshotAuthFailedRef.current = false;
      const body = await response.json() as { success?: boolean; data?: MicroflowRuntimeOverlaySnapshot };
      if (!body?.data) {
        return;
      }
      onSnapshotRef.current?.(body.data);
    } catch (error) {
      onTransportErrorRef.current?.({
        source: "snapshot",
        message: error instanceof Error ? error.message : String(error),
      });
      // Snapshot refresh is best-effort; failures are handled by WS reconnect.
    }
  }, []);

  const connectImpl = useCallback(() => {
    const activeRunId = runIdRef.current;
    if (!activeRunId) {
      return;
    }
    if (snapshotAuthFailedRef.current) {
      setRuntimeStatus("disconnected");
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
        onEventRef.current?.(payload);
      } catch {
        // Ignore malformed runtime event messages.
      }
    };

    ws.onerror = () => {
      setRuntimeStatus("error");
      onTransportErrorRef.current?.({
        source: "websocket",
        message: "Runtime websocket error.",
      });
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
  }, [autoReconnect, clearReconnectTimer, reconnectDelayMs, refreshSnapshot, setRuntimeStatus]);

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
