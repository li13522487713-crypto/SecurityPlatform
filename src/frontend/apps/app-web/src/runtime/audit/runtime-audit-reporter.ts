/**
 * 运行时审计事件采集与上报。
 *
 * 采集关键动作（页面进入/离开/动作执行/绑定加载/错误），
 * 批量上报后端审计 API。
 */

import type { RuntimeAuditEvent, RuntimeAuditEventType } from "../release/runtime-release-types";
import type { ValueMap } from "../types/base-types";

export type { RuntimeAuditEvent, RuntimeAuditEventType };

const eventQueue: RuntimeAuditEvent[] = [];
const FLUSH_INTERVAL_MS = 5000;
const MAX_QUEUE_SIZE = 50;
let flushTimer: ReturnType<typeof setInterval> | null = null;

export function reportAuditEvent(event: {
  executionId: string;
  eventType: RuntimeAuditEventType;
  traceId?: string;
  payload?: ValueMap;
}): void {
  eventQueue.push({
    executionId: event.executionId,
    traceId: event.traceId,
    eventType: event.eventType,
    timestamp: new Date().toISOString(),
    payload: event.payload,
  });

  if (eventQueue.length >= MAX_QUEUE_SIZE) {
    void flushEvents();
  }
}

export function startAuditReporter(): void {
  if (flushTimer) return;
  flushTimer = setInterval(() => {
    void flushEvents();
  }, FLUSH_INTERVAL_MS);
}

export function stopAuditReporter(): void {
  if (flushTimer) {
    clearInterval(flushTimer);
    flushTimer = null;
  }
  void flushEvents();
}

async function flushEvents(): Promise<void> {
  if (eventQueue.length === 0) return;

  const batch = eventQueue.splice(0, eventQueue.length);
  try {
    // Phase 1 仅做本地日志，后端审计 API 在 Phase 2 接入
    if (import.meta.env.DEV) {
      console.debug("[RuntimeAudit] flushing", batch.length, "events");
    }
  } catch {
    eventQueue.unshift(...batch);
  }
}
