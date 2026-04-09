export interface RuntimeAuditEvent {
  eventType: string;
  traceId?: string;
  timestamp: string;
  payload?: Record<string, unknown>;
}

export function buildRuntimeAuditEvent(
  eventType: string,
  payload?: Record<string, unknown>
): RuntimeAuditEvent {
  return {
    eventType,
    timestamp: new Date().toISOString(),
    payload
  };
}
