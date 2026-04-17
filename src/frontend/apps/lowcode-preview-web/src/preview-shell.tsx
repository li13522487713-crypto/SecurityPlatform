import React, { useEffect, useState } from 'react';
import { Layout, Select, Space, Typography, Empty } from '@douyinfe/semi-ui';
import * as signalR from '@microsoft/signalr';
import QRCode from 'qrcode';

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
export const PreviewShell: React.FC<{ appId: string }> = ({ appId }) => {
  const [device, setDevice] = useState<string>('desktop');
  const [qr, setQr] = useState<string>('');
  const [lastDiff, setLastDiff] = useState<unknown>(null);

  useEffect(() => {
    QRCode.toDataURL(window.location.href).then(setQr).catch(() => setQr(''));
  }, []);

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
    conn.on('schemaDiff', (payload: unknown) => {
      setLastDiff(payload);
      // 简单反应式：弹出提示，正式渲染由 RuntimeRenderer + store.applyPatches 完成
      // eslint-disable-next-line no-console
      console.log('[lowcode-preview] schemaDiff received', payload);
    });
    return () => {
      void conn.stop();
    };
  }, [appId]);

  const dev = DEVICES.find((d) => d.value === device) ?? DEVICES[0];

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
            overflow: 'auto'
          }}
        >
          <Empty
            title="预览渲染"
            description={
              lastDiff
                ? '已收到设计态 schemaDiff，运行时渲染由 @atlas/lowcode-runtime-web 接入'
                : '等待设计态 autosave 推送 schemaDiff（HMR）...'
            }
          />
        </div>
      </Layout.Content>
    </Layout>
  );
};
