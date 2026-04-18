import React, { useState } from 'react';
import { SideSheet, Form, Button, List, Tag, Typography, Spin, Empty, Space, Modal } from '@douyinfe/semi-ui';

interface SpanDto {
  spanId: string;
  parentSpanId?: string | null;
  name: string;
  status: string;
  errorMessage?: string | null;
  startedAt: string;
  endedAt?: string | null;
}
interface TraceDto {
  traceId: string;
  appId: string;
  pageId?: string | null;
  componentId?: string | null;
  eventName?: string | null;
  status: string;
  errorKind?: string | null;
  userId: string;
  startedAt: string;
  endedAt?: string | null;
  spans?: SpanDto[];
}

async function tracesApi(path: string): Promise<unknown> {
  const tenantId = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_tenant_id') : null) ?? '00000000-0000-0000-0000-000000000001';
  const token = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_access_token') : null) ?? '';
  const res = await fetch(path, { headers: { 'X-Tenant-Id': tenantId, Authorization: token ? `Bearer ${token}` : '' } });
  if (!res.ok) throw new Error(`查询 trace 失败：${res.status}`);
  const json = await res.json();
  return json?.data;
}

/**
 * 调试台抽屉（M13 C13-1）：6 维 trace 检索 + span 时间线视图。
 *
 * 注：调试台真实数据来自 /api/runtime/traces，与 dispatch 同源。
 * 6 维：traceId / appId+page / component / 时间范围 from-to / errorType / userId
 * 时间线：点击 trace 行 → 拉取 GET /api/runtime/traces/{traceId} 返回 spans 数组 → 按 startedAt 排序展示。
 */
export const DebugDrawer: React.FC<{ appId: string; visible: boolean; onClose: () => void }> = ({ appId, visible, onClose }) => {
  const [traces, setTraces] = useState<TraceDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [openTrace, setOpenTrace] = useState<TraceDto | null>(null);
  const [openLoading, setOpenLoading] = useState(false);

  const search = async (vals: Record<string, string | undefined>) => {
    setLoading(true);
    try {
      const sp = new URLSearchParams({ appId });
      Object.entries(vals).forEach(([k, v]) => { if (v) sp.set(k, v); });
      const data = await tracesApi(`/api/runtime/traces?${sp}`);
      setTraces(Array.isArray(data) ? (data as TraceDto[]) : []);
    } finally {
      setLoading(false);
    }
  };

  const openTraceDetail = async (traceId: string) => {
    setOpenLoading(true);
    try {
      const data = await tracesApi(`/api/runtime/traces/${encodeURIComponent(traceId)}`);
      setOpenTrace((data as TraceDto) ?? null);
    } finally {
      setOpenLoading(false);
    }
  };

  return (
    <SideSheet title="调试台 / 6 维 trace 检索" visible={visible} onCancel={onClose} placement="right" size="large">
      <Form labelPosition="top" onSubmit={(vals) => search(vals as Record<string, string | undefined>)}>
        <Form.Input field="traceId" label="Trace ID" placeholder="精确匹配单条" />
        <Form.Input field="page" label="页面" />
        <Form.Input field="component" label="组件" />
        <Form.Input field="from" label="开始时间 (ISO)" placeholder="2026-04-01T00:00:00Z" />
        <Form.Input field="to" label="结束时间 (ISO)" />
        <Form.Input field="errorType" label="错误类型" />
        <Form.Input field="userId" label="用户 ID" />
        <Form.Slot>
          <Button htmlType="submit" type="primary" loading={loading}>检索</Button>
        </Form.Slot>
      </Form>

      <Typography.Title heading={6} style={{ marginTop: 16 }}>结果（{traces.length}）</Typography.Title>
      {loading ? <Spin /> : (
        <List
          dataSource={traces}
          emptyContent={<Empty title="无 trace（请先在画布触发事件）" />}
          renderItem={(t) => (
            <List.Item
              style={{ cursor: 'pointer' }}
              onClick={() => openTraceDetail(t.traceId)}
              extra={
                <Space>
                  <Tag color={t.status === 'success' ? 'green' : t.status === 'failed' ? 'red' : 'blue'}>{t.status}</Tag>
                  {t.errorKind && <Tag color="red" size="small">{t.errorKind}</Tag>}
                </Space>
              }
            >
              <Typography.Text code style={{ fontSize: 12 }}>{t.traceId}</Typography.Text>
              <Typography.Paragraph type="tertiary" style={{ margin: 0, fontSize: 11 }}>
                {new Date(t.startedAt).toLocaleString()} {t.endedAt ? `→ ${new Date(t.endedAt).toLocaleString()}` : ''}
              </Typography.Paragraph>
            </List.Item>
          )}
        />
      )}

      <Modal title={`Trace 时间线 ${openTrace?.traceId ?? ''}`} visible={!!openTrace} onCancel={() => setOpenTrace(null)} footer={null} width={720}>
        {openLoading ? <Spin /> : openTrace ? <SpanTimeline trace={openTrace} /> : <Empty title="加载失败" />}
      </Modal>
    </SideSheet>
  );
};

const SpanTimeline: React.FC<{ trace: TraceDto }> = ({ trace }) => {
  const spans = (trace.spans ?? []).slice().sort((a, b) => new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime());
  if (spans.length === 0) return <Empty title="该 trace 无 span" />;

  const t0 = new Date(spans[0]!.startedAt).getTime();
  const tEnd = Math.max(...spans.map((s) => new Date(s.endedAt ?? s.startedAt).getTime()));
  const totalMs = Math.max(1, tEnd - t0);

  return (
    <div>
      <Typography.Paragraph type="tertiary" style={{ marginBottom: 12 }}>
        {trace.appId} · {trace.eventName ?? '-'} · 共 {spans.length} 个 span · 总耗时 {totalMs} ms
      </Typography.Paragraph>
      <div style={{ borderLeft: '2px solid #eee', paddingLeft: 12 }}>
        {spans.map((s) => {
          const start = new Date(s.startedAt).getTime();
          const end = new Date(s.endedAt ?? s.startedAt).getTime();
          const offsetPct = ((start - t0) / totalMs) * 100;
          const widthPct = Math.max(1, ((end - start) / totalMs) * 100);
          const color = s.status === 'success' ? '#52c41a' : s.status === 'failed' ? '#ff4d4f' : '#1677ff';
          return (
            <div key={s.spanId} style={{ marginBottom: 10 }}>
              <Space>
                <Tag color={s.status === 'success' ? 'green' : s.status === 'failed' ? 'red' : 'blue'}>{s.status}</Tag>
                <Typography.Text>{s.name}</Typography.Text>
                <Typography.Text type="tertiary" style={{ fontSize: 11 }}>{end - start} ms</Typography.Text>
              </Space>
              <div style={{ position: 'relative', height: 8, background: '#f5f5f7', marginTop: 4, borderRadius: 4 }}>
                <div style={{ position: 'absolute', left: `${offsetPct}%`, width: `${widthPct}%`, height: '100%', background: color, borderRadius: 4 }} />
              </div>
              {s.errorMessage && <Typography.Text type="danger" style={{ fontSize: 11 }}>{s.errorMessage}</Typography.Text>}
            </div>
          );
        })}
      </div>
    </div>
  );
};
