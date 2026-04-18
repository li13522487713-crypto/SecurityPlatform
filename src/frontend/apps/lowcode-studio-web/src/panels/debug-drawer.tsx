import React, { useMemo, useState } from 'react';
import { SideSheet, Form, Button, List, Tag, Typography, Spin, Empty, Space, Modal } from '@douyinfe/semi-ui';
import { DebugClient, summarizePhases, buildSpanTree, type TraceDto, type TraceSpanDto, type PhaseStats } from '@atlas/lowcode-debug-client';

/**
 * 调试台抽屉（M13 C13-1）：6 维 trace 检索 + span 时间线视图 + 性能阶段汇总。
 *
 * 注：调试台数据来自 /api/runtime/traces，由 @atlas/lowcode-debug-client 统一封装；
 * 6 维：traceId / appId+page / component / 时间范围 from-to / errorType / userId。
 */
export const DebugDrawer: React.FC<{ appId: string; visible: boolean; onClose: () => void }> = ({ appId, visible, onClose }) => {
  const [traces, setTraces] = useState<TraceDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [openTrace, setOpenTrace] = useState<TraceDto | null>(null);
  const [openLoading, setOpenLoading] = useState(false);

  const client = useMemo(() => {
    const tenantId = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_tenant_id') : null) ?? '00000000-0000-0000-0000-000000000001';
    const token = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_access_token') : null) ?? '';
    return new DebugClient({ tenantId, token });
  }, []);

  const search = async (vals: Record<string, string | undefined>) => {
    setLoading(true);
    try {
      const list = await client.queryTraces({ appId, ...vals });
      setTraces(Array.isArray(list) ? list : []);
    } finally {
      setLoading(false);
    }
  };

  const openTraceDetail = async (traceId: string) => {
    setOpenLoading(true);
    try {
      const t = await client.getTraceById(traceId);
      setOpenTrace(t ?? null);
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

const isSpanOk = (s: TraceSpanDto): boolean => String(s.status) === 'ok' || String(s.status) === 'success';
const isSpanFailed = (s: TraceSpanDto): boolean => String(s.status) === 'error' || String(s.status) === 'failed';

const SpanTimeline: React.FC<{ trace: TraceDto }> = ({ trace }) => {
  const spans = (trace.spans ?? []).slice().sort((a, b) => new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime());
  if (spans.length === 0) return <Empty title="该 trace 无 span" />;

  const t0 = new Date(spans[0]!.startedAt).getTime();
  const tEnd = Math.max(...spans.map((s) => new Date(s.endedAt ?? s.startedAt).getTime()));
  const totalMs = Math.max(1, tEnd - t0);
  const phaseStats: PhaseStats[] = summarizePhases(spans);
  const tree = buildSpanTree(spans);

  return (
    <div>
      <Typography.Paragraph type="tertiary" style={{ marginBottom: 12 }}>
        {trace.appId} · {trace.eventName ?? '-'} · 共 {spans.length} 个 span · 总耗时 {totalMs} ms · 根 {tree.length} 项
      </Typography.Paragraph>

      {/* 性能阶段汇总（按 render/event/workflow/chatflow/other 聚合） */}
      <Space wrap style={{ marginBottom: 12 }}>
        {phaseStats.map((p) => (
          <Tag key={p.phase} color="blue">
            {p.phase} · ×{p.count} · 总 {p.totalMs}ms · 平均 {p.avgMs}ms · 最大 {p.maxMs}ms
          </Tag>
        ))}
      </Space>

      <div style={{ borderLeft: '2px solid #eee', paddingLeft: 12 }}>
        {spans.map((s) => {
          const start = new Date(s.startedAt).getTime();
          const end = new Date(s.endedAt ?? s.startedAt).getTime();
          const offsetPct = ((start - t0) / totalMs) * 100;
          const widthPct = Math.max(1, ((end - start) / totalMs) * 100);
          const color = isSpanOk(s) ? '#52c41a' : isSpanFailed(s) ? '#ff4d4f' : '#1677ff';
          return (
            <div key={s.spanId} style={{ marginBottom: 10 }}>
              <Space>
                <Tag color={isSpanOk(s) ? 'green' : isSpanFailed(s) ? 'red' : 'blue'}>{s.status}</Tag>
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
