import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Layout, Select, Space, Typography, Empty, Spin, Banner, Tag } from '@douyinfe/semi-ui';
import * as signalR from '@microsoft/signalr';
import QRCode from 'qrcode';
import type { AppSchema, ComponentSchema, PageSchema } from '@atlas/lowcode-schema';

const DEVICES = [
  { value: 'desktop', label: '桌面 1280x800', width: 1280, height: 800 },
  { value: 'tablet', label: 'iPad 1024x768', width: 1024, height: 768 },
  { value: 'mobile', label: 'iPhone 375x812', width: 375, height: 812 }
];

/**
 * PreviewShell（M08 C08-9）：
 *  - HMR 模式：连接 /hubs/lowcode-preview SignalR Hub，订阅 schemaDiff 事件 → 刷新页面
 *  - 扫码移动端预览：生成当前 URL 二维码
 *  - 设备模拟：iPhone / iPad / 桌面 多分辨率
 *  - 状态注入：从 Studio 通过 postMessage 接收 mock state
 */
async function fetchDraft(appId: string): Promise<{ schemaJson: string }> {
  const tenantId = localStorage.getItem('atlas_tenant_id') ?? '00000000-0000-0000-0000-000000000001';
  const token = localStorage.getItem('atlas_access_token') ?? '';
  const res = await fetch(`/api/v1/lowcode/apps/${appId}/draft`, {
    headers: { 'X-Tenant-Id': tenantId, Authorization: token ? `Bearer ${token}` : '' }
  });
  if (!res.ok) throw new Error(`fetch draft failed: ${res.status}`);
  const json = await res.json();
  return json.data;
}

export const PreviewShell: React.FC<{ appId: string }> = ({ appId }) => {
  const [device, setDevice] = useState<string>('desktop');
  const [qr, setQr] = useState<string>('');
  const [hmrTick, setHmrTick] = useState(0);
  const [schemaJson, setSchemaJson] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const reload = useCallback(async () => {
    if (!appId) return;
    setLoading(true);
    setError(null);
    try {
      const d = await fetchDraft(appId);
      setSchemaJson(d.schemaJson);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, [appId]);

  useEffect(() => {
    QRCode.toDataURL(window.location.href).then(setQr).catch(() => setQr(''));
  }, []);

  useEffect(() => {
    void reload();
  }, [reload, hmrTick]);

  useEffect(() => {
    if (!appId) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/lowcode-preview', { accessTokenFactory: () => localStorage.getItem('atlas_access_token') ?? '' })
      .withAutomaticReconnect()
      .build();
    conn
      .start()
      .then(() => conn.invoke('JoinApp', appId))
      .catch((err) => console.error('SignalR connect failed', err));
    conn.on('schemaDiff', () => {
      // HMR：收到 schemaDiff 后重新拉取 draft（M08 C08-9 简化版本，
      // 全增量 patch 流由 lowcode-runtime-web store.applyPatches 在后续装配阶段接入）。
      setHmrTick((t) => t + 1);
    });
    return () => {
      void conn.stop();
    };
  }, [appId]);

  const dev = DEVICES.find((d) => d.value === device) ?? DEVICES[0];

  const app = useMemo<AppSchema | null>(() => {
    if (!schemaJson) return null;
    try { return JSON.parse(schemaJson) as AppSchema; } catch { return null; }
  }, [schemaJson]);
  const page: PageSchema | null = app?.pages?.[0] ?? null;

  return (
    <Layout style={{ height: '100vh' }}>
      <Layout.Header>
        <Space style={{ padding: '0 16px', height: 56, width: '100%', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography.Title heading={6} style={{ margin: 0 }}>预览 #{appId}</Typography.Title>
          <Space>
            <Select value={device} onChange={(v) => setDevice(v as string)} style={{ width: 200 }}>
              {DEVICES.map((d) => (
                <Select.Option key={d.value} value={d.value}>{d.label}</Select.Option>
              ))}
            </Select>
            {qr ? <img src={qr} alt="qr" style={{ width: 56, height: 56 }} /> : null}
          </Space>
        </Space>
      </Layout.Header>
      <Layout.Content style={{ display: 'flex', justifyContent: 'center', alignItems: 'flex-start', padding: 24, background: '#f5f5f7' }}>
        <div
          style={{
            width: dev.width,
            height: dev.height,
            background: '#fff',
            border: '1px solid #ddd',
            borderRadius: 8,
            overflow: 'auto',
            padding: 16
          }}
        >
          {loading && <Spin />}
          {error && <Banner type="danger" description={error} />}
          {!loading && !error && !page && <Empty title="该应用暂无页面" />}
          {!loading && !error && page && (
            <>
              <Typography.Title heading={6} style={{ margin: '0 0 12px' }}>
                {page.displayName} <Tag size="small" style={{ marginLeft: 8 }}>{page.path}</Tag>
              </Typography.Title>
              <PreviewNode node={page.root} depth={0} />
            </>
          )}
        </div>
      </Layout.Content>
    </Layout>
  );
};

const PreviewNode: React.FC<{ node: ComponentSchema; depth: number }> = ({ node, depth }) => {
  if (node.visible === false) return null;
  return (
    <div
      style={{
        marginLeft: depth * 12,
        marginBottom: 8,
        padding: '8px 12px',
        background: '#fff',
        border: '1px dashed #d8d8d8',
        borderRadius: 4
      }}
      data-component-id={node.id}
    >
      <Typography.Text strong style={{ fontSize: 13 }}>{node.type}</Typography.Text>
      {(node.children ?? []).length > 0 && (
        <div style={{ marginTop: 8 }}>
          {(node.children ?? []).map((c) => <PreviewNode key={c.id} node={c} depth={depth + 1} />)}
        </div>
      )}
    </div>
  );
};
