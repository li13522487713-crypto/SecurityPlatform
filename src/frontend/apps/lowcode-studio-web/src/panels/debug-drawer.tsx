import React, { useState } from 'react';
import { SideSheet, Form, Button, List, Tag, Typography, Spin, Empty, Space } from '@douyinfe/semi-ui';

/**
 * 调试台抽屉（M13 C13-1）：6 维 trace 检索 + 时间线视图（占位）。
 *
 * 注：调试台真实数据来自 /api/runtime/traces，与 dispatch 同源；本抽屉提供查询 UI。
 */
export const DebugDrawer: React.FC<{ appId: string; visible: boolean; onClose: () => void }> = ({ appId, visible, onClose }) => {
  const [filters, setFilters] = useState<{ traceId?: string; page?: string; component?: string; from?: string; to?: string; errorType?: string; userId?: string }>({});
  const [traces, setTraces] = useState<Array<{ traceId: string; status: string; startedAt: string; endedAt?: string; errorKind?: string }>>([]);
  const [loading, setLoading] = useState(false);

  const search = async () => {
    setLoading(true);
    try {
      const tenantId = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_tenant_id') : null) ?? '00000000-0000-0000-0000-000000000001';
      const token = (typeof localStorage !== 'undefined' ? localStorage.getItem('atlas_access_token') : null) ?? '';
      const sp = new URLSearchParams({ appId });
      Object.entries(filters).forEach(([k, v]) => { if (v) sp.set(k, v); });
      const res = await fetch(`/api/runtime/traces?${sp}`, {
        headers: { 'X-Tenant-Id': tenantId, Authorization: token ? `Bearer ${token}` : '' }
      });
      if (!res.ok) throw new Error(`查询 trace 失败：${res.status}`);
      const json = await res.json();
      setTraces(json?.data ?? []);
    } finally {
      setLoading(false);
    }
  };

  return (
    <SideSheet title="调试台 / 6 维 trace 检索" visible={visible} onCancel={onClose} placement="right" size="large">
      <Form labelPosition="top" onSubmit={(vals) => { setFilters(vals as never); search(); }}>
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
    </SideSheet>
  );
};
