/**
 * 运行时审计事件采集与上报。
 *
 * 采集关键动作（页面进入/表单提交/API 调用/错误），
 * 批量上报后端审计 API。
 */

export interface RuntimeAuditEvent {
  executionId: string;
  appKey: string;
  pageKey: string;
  eventType: "page_enter" | "page_leave" | "action_execute" | "form_submit" | "api_call" | "error";
  detail?: Record<string, unknown>;
  timestamp: string;
}

const eventQueue: RuntimeAuditEvent[] = [];
const FLUSH_INTERVAL_MS = 5000;
const MAX_QUEUE_SIZE = 50;
let flushTimer: ReturnType<typeof setInterval> | null = null;

export function reportAuditEvent(event: Omit<RuntimeAuditEvent, "timestamp">): void {
  eventQueue.push({
    ...event,
    timestamp: new Date().toISOString(),
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
    // 审计上报失败不阻断业务
    eventQueue.unshift(...batch);
  }
}
