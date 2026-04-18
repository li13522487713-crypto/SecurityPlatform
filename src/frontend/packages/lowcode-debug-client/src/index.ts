/**
 * Atlas 低代码调试台客户端（M13 C13-1..C13-7）。
 *
 * 提供：
 *  - DebugClient.queryTraces：6 维检索（traceId / page / component / time / errorType / userId）
 *  - DebugClient.getTraceById：完整 spans 树
 *  - DebugClient.queryMessageLog：跨域消息日志（chatflow + workflow + agent + tool + dispatch）
 *  - 性能视图工具：组件渲染时长 / 事件耗时 / 工作流时长（与 lowcode-runtime-web 性能埋点对齐）
 *  - 6 维查询参数构造与 URL 编码
 *
 * 强约束：本包不依赖 React，纯协议层；React 调试抽屉在 lowcode-studio-web / lowcode-preview-web 内装配。
 */

export interface TraceSpanDto {
  spanId: string;
  parentSpanId?: string;
  name: string;
  status: 'ok' | 'error';
  attributes?: unknown;
  errorMessage?: string;
  startedAt: string;
  endedAt?: string;
}

export interface TraceDto {
  traceId: string;
  appId: string;
  pageId?: string;
  componentId?: string;
  eventName?: string;
  status: 'running' | 'success' | 'failed';
  errorKind?: string;
  userId: string;
  startedAt: string;
  endedAt?: string;
  spans: TraceSpanDto[];
}

export interface TraceQuery {
  traceId?: string;
  appId?: string;
  page?: string;
  component?: string;
  from?: string;
  to?: string;
  errorType?: string;
  userId?: string;
  pageIndex?: number;
  pageSize?: number;
}

export interface MessageLogEntryDto {
  entryId: string;
  source: string;
  kind: string;
  sessionId?: string;
  workflowId?: string;
  agentId?: string;
  traceId?: string;
  payload?: unknown;
  occurredAt: string;
}

export interface MessageLogQuery {
  sessionId?: string;
  workflowId?: string;
  agentId?: string;
  from?: string;
  to?: string;
  pageIndex?: number;
  pageSize?: number;
}

interface ApiResponse<T> { success: boolean; data: T; code?: string; message?: string }

export interface DebugClientOptions {
  tenantId: string;
  token?: string;
}

export class DebugClient {
  constructor(private readonly opts: DebugClientOptions) {}

  queryTraces(q: TraceQuery): Promise<TraceDto[]> {
    const url = `/api/runtime/traces${buildQueryString(q as unknown as Record<string, unknown>)}`;
    return this.fetchJson<TraceDto[]>(url);
  }

  getTraceById(traceId: string): Promise<TraceDto | null> {
    return this.fetchJson<TraceDto | null>(`/api/runtime/traces/${encodeURIComponent(traceId)}`);
  }

  queryMessageLog(q: MessageLogQuery): Promise<MessageLogEntryDto[]> {
    return this.fetchJson<MessageLogEntryDto[]>(`/api/runtime/message-log${buildQueryString(q as unknown as Record<string, unknown>)}`);
  }

  private async fetchJson<T>(path: string): Promise<T> {
    const res = await fetch(path, {
      method: 'GET',
      headers: { 'X-Tenant-Id': this.opts.tenantId, Authorization: this.opts.token ? `Bearer ${this.opts.token}` : '' }
    });
    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`debug ${path} ${res.status}: ${text}`);
    }
    const json = (await res.json()) as ApiResponse<T>;
    if (!json.success) throw new Error(`${json.code ?? 'DEBUG_ERROR'}: ${json.message ?? 'unknown'}`);
    return json.data;
  }
}

/** 把 6 维查询参数序列化为 URL query string。空字段忽略。*/
export function buildQueryString(query: Record<string, unknown>): string {
  const parts: string[] = [];
  for (const [k, v] of Object.entries(query)) {
    if (v === undefined || v === null || v === '') continue;
    parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  }
  return parts.length ? `?${parts.join('&')}` : '';
}

/** 将 trace spans 排成树（基于 parentSpanId）；用于时间线视图。*/
export interface SpanTreeNode {
  span: TraceSpanDto;
  children: SpanTreeNode[];
}

export function buildSpanTree(spans: ReadonlyArray<TraceSpanDto>): SpanTreeNode[] {
  const map = new Map<string, SpanTreeNode>();
  for (const s of spans) map.set(s.spanId, { span: s, children: [] });
  const roots: SpanTreeNode[] = [];
  for (const node of map.values()) {
    const parentId = node.span.parentSpanId;
    if (parentId && map.has(parentId)) {
      map.get(parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }
  // 按 startedAt 排序
  function sortRec(arr: SpanTreeNode[]) {
    arr.sort((a, b) => a.span.startedAt.localeCompare(b.span.startedAt));
    for (const n of arr) sortRec(n.children);
  }
  sortRec(roots);
  return roots;
}

/** 性能视图工具：把 spans 按 name 前缀（render/event/workflow/chatflow）聚合时长统计。*/
export interface PhaseStats {
  phase: 'render' | 'event' | 'workflow' | 'chatflow' | 'other';
  count: number;
  totalMs: number;
  avgMs: number;
  maxMs: number;
}

export function summarizePhases(spans: ReadonlyArray<TraceSpanDto>): PhaseStats[] {
  const buckets = new Map<PhaseStats['phase'], { total: number; count: number; max: number }>();
  for (const s of spans) {
    if (!s.endedAt) continue;
    const ms = new Date(s.endedAt).getTime() - new Date(s.startedAt).getTime();
    const phase: PhaseStats['phase'] = s.name.startsWith('render') ? 'render'
      : s.name.startsWith('action') || s.name.startsWith('dispatcher') ? 'event'
      : s.name.startsWith('workflow') ? 'workflow'
      : s.name.startsWith('chatflow') ? 'chatflow'
      : 'other';
    const cur = buckets.get(phase) ?? { total: 0, count: 0, max: 0 };
    cur.total += ms;
    cur.count += 1;
    cur.max = Math.max(cur.max, ms);
    buckets.set(phase, cur);
  }
  return Array.from(buckets.entries()).map(([phase, v]) => ({
    phase,
    count: v.count,
    totalMs: v.total,
    avgMs: v.count > 0 ? Math.round(v.total / v.count) : 0,
    maxMs: v.max
  }));
}

export const __ATLAS_LOWCODE_PACKAGE__ = '@atlas/lowcode-debug-client' as const;
